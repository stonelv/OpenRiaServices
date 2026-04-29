using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenRiaServices.Server;

namespace OpenRiaServices.Server.EntityFrameworkCore
{
    /// <summary>
    /// Extension methods for applying soft delete query filters to EF Core models.
    /// </summary>
    public static class SoftDeleteQueryFilterExtensions
    {
        /// <summary>
        /// Applies global query filters for all entities marked with <see cref="SoftDeleteAttribute"/>.
        /// This should be called in <see cref="DbContext.OnModelCreating(ModelBuilder)"/>.
        /// </summary>
        /// <param name="modelBuilder">The model builder.</param>
        /// <returns>The model builder for method chaining.</returns>
        public static ModelBuilder ApplySoftDeleteFilters(this ModelBuilder modelBuilder)
        {
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                Type clrType = entityType.ClrType;
                if (clrType == null)
                {
                    continue;
                }

                MetaType metaType = MetaType.GetMetaType(clrType);
                if (!metaType.IsSoftDeleteEnabled)
                {
                    continue;
                }

                SoftDeleteConfiguration? softDeleteConfig = metaType.SoftDeleteConfiguration;
                if (softDeleteConfig == null)
                {
                    continue;
                }

                if (!softDeleteConfig.AutoFilterEnabled)
                {
                    continue;
                }

                MethodInfo applyFilterMethod = typeof(SoftDeleteQueryFilterExtensions)
                    .GetMethod(nameof(ApplySoftDeleteFilterForEntity), BindingFlags.NonPublic | BindingFlags.Static)!
                    .MakeGenericMethod(clrType);

                applyFilterMethod.Invoke(null, new object[] { modelBuilder, softDeleteConfig });
            }

            return modelBuilder;
        }

        /// <summary>
        /// Applies a soft delete query filter for a specific entity type.
        /// </summary>
        /// <typeparam name="TEntity">The entity type.</typeparam>
        /// <param name="modelBuilder">The model builder.</param>
        /// <param name="softDeleteConfig">The soft delete configuration.</param>
        private static void ApplySoftDeleteFilterForEntity<TEntity>(ModelBuilder modelBuilder, SoftDeleteConfiguration softDeleteConfig)
            where TEntity : class
        {
            EntityTypeBuilder<TEntity> entityBuilder = modelBuilder.Entity<TEntity>();
            LambdaExpression filterExpression = CreateSoftDeleteFilterExpression<TEntity>(softDeleteConfig);
            entityBuilder.HasQueryFilter(filterExpression);
        }

        /// <summary>
        /// Creates a lambda expression for the soft delete filter.
        /// For example: e => !e.IsDeleted (for bool property)
        /// or e => e.DeletedAt == null (for DateTime property)
        /// </summary>
        /// <typeparam name="TEntity">The entity type.</typeparam>
        /// <param name="softDeleteConfig">The soft delete configuration.</param>
        /// <returns>A lambda expression representing the soft delete filter.</returns>
        private static Expression<Func<TEntity, bool>> CreateSoftDeleteFilterExpression<TEntity>(SoftDeleteConfiguration softDeleteConfig)
            where TEntity : class
        {
            ParameterExpression parameter = Expression.Parameter(typeof(TEntity), "e");
            MemberExpression propertyAccess = Expression.Property(parameter, softDeleteConfig.PropertyName);

            Expression filterBody;

            if (softDeleteConfig.PropertyType == typeof(bool))
            {
                filterBody = Expression.Not(propertyAccess);
            }
            else if (softDeleteConfig.PropertyType == typeof(bool?))
            {
                ConstantExpression nullConstant = Expression.Constant(null, typeof(bool?));
                BinaryExpression equalToNull = Expression.Equal(propertyAccess, nullConstant);
                BinaryExpression equalToFalse = Expression.Equal(propertyAccess, Expression.Constant(false, typeof(bool?)));
                filterBody = Expression.OrElse(equalToNull, equalToFalse);
            }
            else if (softDeleteConfig.PropertyType == typeof(int))
            {
                filterBody = Expression.Equal(propertyAccess, Expression.Constant(0, typeof(int)));
            }
            else if (softDeleteConfig.PropertyType == typeof(int?))
            {
                ConstantExpression nullConstant = Expression.Constant(null, typeof(int?));
                BinaryExpression equalToNull = Expression.Equal(propertyAccess, nullConstant);
                BinaryExpression equalToZero = Expression.Equal(propertyAccess, Expression.Constant(0, typeof(int?)));
                filterBody = Expression.OrElse(equalToNull, equalToZero);
            }
            else if (softDeleteConfig.PropertyType == typeof(DateTime))
            {
                ConstantExpression defaultValue = Expression.Constant(default(DateTime), typeof(DateTime));
                filterBody = Expression.Equal(propertyAccess, defaultValue);
            }
            else if (softDeleteConfig.PropertyType == typeof(DateTime?))
            {
                ConstantExpression nullConstant = Expression.Constant(null, typeof(DateTime?));
                filterBody = Expression.Equal(propertyAccess, nullConstant);
            }
            else if (softDeleteConfig.PropertyType == typeof(Guid))
            {
                ConstantExpression emptyGuid = Expression.Constant(Guid.Empty, typeof(Guid));
                filterBody = Expression.Equal(propertyAccess, emptyGuid);
            }
            else if (softDeleteConfig.PropertyType == typeof(Guid?))
            {
                ConstantExpression nullConstant = Expression.Constant(null, typeof(Guid?));
                BinaryExpression equalToNull = Expression.Equal(propertyAccess, nullConstant);
                ConstantExpression emptyGuid = Expression.Constant(Guid.Empty, typeof(Guid?));
                BinaryExpression equalToEmpty = Expression.Equal(propertyAccess, emptyGuid);
                filterBody = Expression.OrElse(equalToNull, equalToEmpty);
            }
            else
            {
                object? deletedValue = softDeleteConfig.DeletedValue;
                if (deletedValue == null)
                {
                    filterBody = Expression.Equal(propertyAccess, Expression.Constant(null, softDeleteConfig.PropertyType));
                }
                else
                {
                    object notDeletedValue = GetNotDeletedValue(softDeleteConfig.PropertyType, deletedValue);
                    filterBody = Expression.Equal(propertyAccess, Expression.Constant(notDeletedValue, softDeleteConfig.PropertyType));
                }
            }

            return Expression.Lambda<Func<TEntity, bool>>(filterBody, parameter);
        }

        /// <summary>
        /// Gets the "not deleted" value for a given property type and deleted value.
        /// </summary>
        private static object GetNotDeletedValue(Type propertyType, object deletedValue)
        {
            if (propertyType == typeof(bool))
            {
                return false;
            }
            if (propertyType == typeof(int))
            {
                return 0;
            }
            if (propertyType == typeof(DateTime))
            {
                return default(DateTime);
            }
            if (propertyType == typeof(Guid))
            {
                return Guid.Empty;
            }

            if (propertyType.IsValueType)
            {
                return Activator.CreateInstance(propertyType)!;
            }

            return null!;
        }

        /// <summary>
        /// Returns a query that includes soft-deleted entities by ignoring the query filter.
        /// </summary>
        /// <typeparam name="TEntity">The entity type.</typeparam>
        /// <param name="query">The source query.</param>
        /// <returns>A query that includes soft-deleted entities.</returns>
        public static IQueryable<TEntity> IncludeDeleted<TEntity>(this IQueryable<TEntity> query)
            where TEntity : class
        {
            return query.IgnoreQueryFilters();
        }
    }
}
