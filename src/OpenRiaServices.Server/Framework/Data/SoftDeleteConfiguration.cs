using System;
using System.ComponentModel;
using System.Reflection;

namespace OpenRiaServices.Server
{
    /// <summary>
    /// Provides information about soft delete configuration for an entity type.
    /// </summary>
    public sealed class SoftDeleteConfiguration
    {
        private readonly PropertyInfo _propertyInfo;
        private readonly PropertyDescriptor _propertyDescriptor;

        /// <summary>
        /// Initializes a new instance of the <see cref="SoftDeleteConfiguration"/> class.
        /// </summary>
        /// <param name="entityType">The entity type.</param>
        /// <param name="attribute">The <see cref="SoftDeleteAttribute"/> instance.</param>
        /// <param name="propertyInfo">The <see cref="PropertyInfo"/> for the soft delete property.</param>
        /// <param name="propertyDescriptor">The <see cref="PropertyDescriptor"/> for the soft delete property.</param>
        internal SoftDeleteConfiguration(Type entityType, SoftDeleteAttribute attribute, PropertyInfo propertyInfo, PropertyDescriptor propertyDescriptor)
        {
            this.EntityType = entityType ?? throw new ArgumentNullException(nameof(entityType));
            this.Attribute = attribute ?? throw new ArgumentNullException(nameof(attribute));
            this._propertyInfo = propertyInfo ?? throw new ArgumentNullException(nameof(propertyInfo));
            this._propertyDescriptor = propertyDescriptor ?? throw new ArgumentNullException(nameof(propertyDescriptor));

            this.PropertyName = attribute.PropertyName;
            this.PropertyType = propertyInfo.PropertyType;
            this.AutoFilterEnabled = attribute.AutoFilterEnabled;

            this.DeletedValue = GetDefaultDeletedValue(this.PropertyType);
        }

        /// <summary>
        /// Gets the entity type that this configuration applies to.
        /// </summary>
        public Type EntityType { get; }

        /// <summary>
        /// Gets the <see cref="SoftDeleteAttribute"/> that was used to create this configuration.
        /// </summary>
        public SoftDeleteAttribute Attribute { get; }

        /// <summary>
        /// Gets the name of the soft delete flag property.
        /// </summary>
        public string PropertyName { get; }

        /// <summary>
        /// Gets the type of the soft delete flag property.
        /// </summary>
        public Type PropertyType { get; }

        /// <summary>
        /// Gets the value that indicates the entity is soft-deleted.
        /// </summary>
        public object DeletedValue { get; }

        /// <summary>
        /// Gets a value indicating whether automatic query filtering is enabled.
        /// </summary>
        public bool AutoFilterEnabled { get; }

        /// <summary>
        /// Gets the <see cref="PropertyInfo"/> for the soft delete property.
        /// </summary>
        public PropertyInfo PropertyInfo => _propertyInfo;

        /// <summary>
        /// Gets the <see cref="PropertyDescriptor"/> for the soft delete property.
        /// </summary>
        public PropertyDescriptor PropertyDescriptor => _propertyDescriptor;

        /// <summary>
        /// Sets the soft delete flag on the specified entity to the deleted state.
        /// </summary>
        /// <param name="entity">The entity to mark as deleted.</param>
        public void MarkAsDeleted(object entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            _propertyInfo.SetValue(entity, this.DeletedValue);
        }

        /// <summary>
        /// Gets the default value that represents a "deleted" state for a given type.
        /// </summary>
        /// <param name="propertyType">The type of the soft delete property.</param>
        /// <returns>The default deleted value.</returns>
        private static object GetDefaultDeletedValue(Type propertyType)
        {
            if (propertyType == typeof(bool))
            {
                return true;
            }
            if (propertyType == typeof(bool?))
            {
                return true;
            }
            if (propertyType == typeof(int))
            {
                return 1;
            }
            if (propertyType == typeof(int?))
            {
                return 1;
            }
            if (propertyType == typeof(DateTime))
            {
                return DateTime.UtcNow;
            }
            if (propertyType == typeof(DateTime?))
            {
                return DateTime.UtcNow;
            }
            if (propertyType == typeof(DateTimeOffset))
            {
                return DateTimeOffset.UtcNow;
            }
            if (propertyType == typeof(DateTimeOffset?))
            {
                return DateTimeOffset.UtcNow;
            }
            if (propertyType == typeof(Guid))
            {
                return Guid.NewGuid();
            }
            if (propertyType == typeof(Guid?))
            {
                return Guid.NewGuid();
            }

            if (propertyType.IsValueType)
            {
                return Activator.CreateInstance(propertyType);
            }

            return null;
        }

        /// <summary>
        /// Tries to create a <see cref="SoftDeleteConfiguration"/> instance for the specified type.
        /// </summary>
        /// <param name="entityType">The entity type to check for soft delete configuration.</param>
        /// <param name="configuration">When this method returns, contains the configuration
        /// if the type has a <see cref="SoftDeleteAttribute"/>, otherwise null.</param>
        /// <returns>True if a valid configuration was found; otherwise, false.</returns>
        internal static bool TryCreate(Type entityType, out SoftDeleteConfiguration configuration)
        {
            configuration = null;

            var attribute = TypeDescriptor.GetAttributes(entityType)[typeof(SoftDeleteAttribute)] as SoftDeleteAttribute;
            if (attribute == null)
            {
                return false;
            }

            var propertyInfo = entityType.GetProperty(attribute.PropertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
            if (propertyInfo == null)
            {
                return false;
            }

            var propertyDescriptor = TypeDescriptor.GetProperties(entityType)[attribute.PropertyName];
            if (propertyDescriptor == null)
            {
                return false;
            }

            configuration = new SoftDeleteConfiguration(entityType, attribute, propertyInfo, propertyDescriptor);
            return true;
        }
    }
}
