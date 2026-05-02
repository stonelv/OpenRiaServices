using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using OpenRiaServices.Server;

namespace OpenRiaServices.Dapper
{
    /// <summary>
    /// 自动处理 CRUD 操作的 Dapper DomainService
    /// 使用实体元数据自动生成 SQL 语句
    /// </summary>
    /// <typeparam name="TConnection">数据库连接类型</typeparam>
    public abstract class AutoDapperDomainService<TConnection> : DapperDomainService<TConnection>
        where TConnection : DbConnection, new()
    {
        private readonly ConcurrentDictionary<Type, SqlGenerator> _generatorCache = new ConcurrentDictionary<Type, SqlGenerator>();

        protected AutoDapperDomainService(string connectionString)
            : base(connectionString)
        {
        }

        /// <summary>
        /// 获取 SQL 生成器
        /// </summary>
        protected SqlGenerator GetSqlGenerator(Type entityType)
        {
            return _generatorCache.GetOrAdd(entityType, t =>
            {
                var metadata = EntityMetadata.GetMetadata(t);
                return new SqlGenerator(metadata);
            });
        }

        /// <summary>
        /// 插入实体
        /// </summary>
        protected override void InsertEntity(object entity)
        {
            var generator = GetSqlGenerator(entity.GetType());
            var statement = generator.GenerateInsert(entity);
            var metadata = EntityMetadata.GetMetadata(entity.GetType());

            if (metadata.DatabaseGeneratedProperties.Any(p => p.IsKey))
            {
                var result = Connection.QueryFirstOrDefault<dynamic>(
                    statement.Sql,
                    statement.Parameters.Parameters,
                    transaction: Transaction);

                if (result != null)
                {
                    var identityValue = GetIdentityValue(result);
                    if (identityValue != null)
                    {
                        var keyProp = metadata.KeyProperties.First(p => p.IsDatabaseGenerated);
                        SetIdentityValue(entity, keyProp, identityValue);
                    }
                }
            }
            else
            {
                Connection.Execute(
                    statement.Sql,
                    statement.Parameters.Parameters,
                    transaction: Transaction);
            }
        }

        /// <summary>
        /// 更新实体
        /// </summary>
        protected override void UpdateEntity(object entity, object? originalEntity)
        {
            var generator = GetSqlGenerator(entity.GetType());
            var statement = generator.GenerateUpdate(entity, originalEntity);

            if (statement.IsEmpty)
            {
                return;
            }

            var rowsAffected = Connection.Execute(
                statement.Sql,
                statement.Parameters.Parameters,
                transaction: Transaction);

            if (rowsAffected == 0)
            {
                var metadata = EntityMetadata.GetMetadata(entity.GetType());
                if (metadata.HasTimestamp)
                {
                    throw CreateConcurrencyException(entity);
                }
            }
        }

        /// <summary>
        /// 删除实体
        /// </summary>
        protected override void DeleteEntity(object entity)
        {
            var generator = GetSqlGenerator(entity.GetType());
            var statement = generator.GenerateDelete(entity);

            var rowsAffected = Connection.Execute(
                statement.Sql,
                statement.Parameters.Parameters,
                transaction: Transaction);

            if (rowsAffected == 0)
            {
                var metadata = EntityMetadata.GetMetadata(entity.GetType());
                if (metadata.HasTimestamp)
                {
                    throw CreateConcurrencyException(entity);
                }
            }
        }

        /// <summary>
        /// 创建并发异常
        /// </summary>
        private DBConcurrencyException CreateConcurrencyException(object entity)
        {
            var table = new DataTable();
            var row = table.NewRow();
            var metadata = EntityMetadata.GetMetadata(entity.GetType());

            foreach (var keyProp in metadata.KeyProperties)
            {
                var col = table.Columns.Add(keyProp.ColumnName, keyProp.PropertyType);
                row[col] = keyProp.GetValue(entity) ?? DBNull.Value;
            }

            table.Rows.Add(row);
            table.AcceptChanges();

            return new DBConcurrencyException("Concurrency conflict detected.", null, new DataRow[] { row });
        }

        /// <summary>
        /// 获取自增 ID 值
        /// </summary>
        private object? GetIdentityValue(dynamic result)
        {
            if (result is IDictionary<string, object> dict)
            {
                if (dict.TryGetValue("Id", out var value))
                {
                    return value;
                }
            }
            return result;
        }

        /// <summary>
        /// 设置自增 ID 值到实体
        /// </summary>
        private void SetIdentityValue(object entity, PropertyMetadata keyProp, object identityValue)
        {
            var targetType = Nullable.GetUnderlyingType(keyProp.PropertyType) ?? keyProp.PropertyType;
            
            if (identityValue is IConvertible convertible)
            {
                var convertedValue = Convert.ChangeType(convertible, targetType);
                keyProp.SetValue(entity, convertedValue);
            }
            else
            {
                keyProp.SetValue(entity, identityValue);
            }
        }

        #region 查询辅助方法

        /// <summary>
        /// 查询所有实体
        /// </summary>
        protected IEnumerable<TEntity> QueryAll<TEntity>()
            where TEntity : class
        {
            var generator = GetSqlGenerator(typeof(TEntity));
            var statement = generator.GenerateSelectAll();
            return Connection.Query<TEntity>(statement.Sql, transaction: Transaction);
        }

        /// <summary>
        /// 根据 ID 查询单个实体
        /// </summary>
        protected TEntity? QueryById<TEntity>(object keyValues)
            where TEntity : class
        {
            var generator = GetSqlGenerator(typeof(TEntity));
            var statement = generator.GenerateSelectById(keyValues);
            return Connection.QueryFirstOrDefault<TEntity>(
                statement.Sql, 
                keyValues, 
                transaction: Transaction);
        }

        /// <summary>
        /// 执行自定义 SQL 查询
        /// </summary>
        protected IEnumerable<TEntity> Query<TEntity>(string sql, object? param = null)
            where TEntity : class
        {
            return Connection.Query<TEntity>(sql, param, transaction: Transaction);
        }

        /// <summary>
        /// 执行自定义 SQL 查询（异步）
        /// </summary>
        protected Task<IEnumerable<TEntity>> QueryAsync<TEntity>(string sql, object? param = null, CancellationToken cancellationToken = default)
            where TEntity : class
        {
            return Connection.QueryAsync<TEntity>(sql, param, transaction: Transaction);
        }

        /// <summary>
        /// 执行 SQL 并返回单个结果
        /// </summary>
        protected TEntity? QueryFirstOrDefault<TEntity>(string sql, object? param = null)
            where TEntity : class
        {
            return Connection.QueryFirstOrDefault<TEntity>(sql, param, transaction: Transaction);
        }

        /// <summary>
        /// 执行 SQL 并返回受影响行数
        /// </summary>
        protected int Execute(string sql, object? param = null)
        {
            return Connection.Execute(sql, param, transaction: Transaction);
        }

        /// <summary>
        /// 执行 SQL 并返回受影响行数（异步）
        /// </summary>
        protected Task<int> ExecuteAsync(string sql, object? param = null, CancellationToken cancellationToken = default)
        {
            return Connection.ExecuteAsync(sql, param, transaction: Transaction);
        }

        #endregion
    }
}
