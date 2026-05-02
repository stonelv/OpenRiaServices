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

        /// <summary>
        /// 是否使用 OUTPUT 子句获取自增 ID（默认 true，比 SCOPE_IDENTITY() 更可靠）
        /// </summary>
        protected bool UseOutputClauseForIdentity { get; set; } = true;

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
            var metadata = EntityMetadata.GetMetadata(entity.GetType());
            
            SqlStatement statement;
            if (UseOutputClauseForIdentity && metadata.DatabaseGeneratedProperties.Any(p => p.IsKey))
            {
                statement = generator.GenerateInsertWithOutput(entity);
            }
            else
            {
                statement = generator.GenerateInsert(entity);
            }

            if (statement.IdentityInfo.HasIdentityColumn)
            {
                object? identityValue;
                
                if (UseOutputClauseForIdentity && statement.IdentityInfo.UseOutputParameter)
                {
                    identityValue = Connection.ExecuteScalar<object>(
                        statement.Sql,
                        statement.Parameters,
                        transaction: Transaction);
                }
                else
                {
                    var result = Connection.QueryFirstOrDefault<dynamic>(
                        statement.Sql,
                        statement.Parameters,
                        transaction: Transaction);
                    
                    identityValue = GetIdentityValue(result, statement.IdentityInfo);
                }

                if (identityValue != null && identityValue != DBNull.Value)
                {
                    var keyProp = metadata.KeyProperties.First(p => p.IsDatabaseGenerated);
                    SetIdentityValue(entity, keyProp, identityValue);
                }
            }
            else
            {
                Connection.Execute(
                    statement.Sql,
                    statement.Parameters,
                    transaction: Transaction);
            }
        }

        /// <summary>
        /// 获取自增 ID 值
        /// </summary>
        /// <param name="result">查询结果（dynamic 对象）</param>
        /// <param name="identityInfo">自增列信息</param>
        /// <returns>自增 ID 值</returns>
        protected virtual object? GetIdentityValue(dynamic result, IdentityColumnInfo identityInfo)
        {
            if (result == null)
            {
                return null;
            }

            if (result is IDictionary<string, object> dict)
            {
                if (dict.TryGetValue("IdentityValue", out var value))
                {
                    return value;
                }
                
                if (identityInfo.ColumnName != null && dict.TryGetValue(identityInfo.ColumnName, out value))
                {
                    return value;
                }
                
                if (identityInfo.PropertyName != null && dict.TryGetValue(identityInfo.PropertyName, out value))
                {
                    return value;
                }
                
                if (dict.Count > 0)
                {
                    return dict.Values.First();
                }
            }

            try
            {
                var type = result.GetType();
                
                if (identityInfo.PropertyName != null)
                {
                    var prop = type.GetProperty(identityInfo.PropertyName, BindingFlags.Public | BindingFlags.Instance);
                    if (prop != null)
                    {
                        return prop.GetValue(result);
                    }
                }
                
                if (identityInfo.ColumnName != null)
                {
                    var prop = type.GetProperty(identityInfo.ColumnName, BindingFlags.Public | BindingFlags.Instance);
                    if (prop != null)
                    {
                        return prop.GetValue(result);
                    }
                }

                var identityProp = type.GetProperty("IdentityValue", BindingFlags.Public | BindingFlags.Instance);
                if (identityProp != null)
                {
                    return identityProp.GetValue(result);
                }
                
                var idProp = type.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
                if (idProp != null)
                {
                    return idProp.GetValue(result);
                }
                
                var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                if (props.Length > 0)
                {
                    return props[0].GetValue(result);
                }
            }
            catch
            {
            }

            return result;
        }

        /// <summary>
        /// 设置自增 ID 值到实体
        /// </summary>
        /// <param name="entity">实体对象</param>
        /// <param name="keyProp">主键属性元数据</param>
        /// <param name="identityValue">自增 ID 值</param>
        protected virtual void SetIdentityValue(object entity, PropertyMetadata keyProp, object identityValue)
        {
            if (identityValue == null || identityValue == DBNull.Value)
            {
                return;
            }

            var targetType = Nullable.GetUnderlyingType(keyProp.PropertyType) ?? keyProp.PropertyType;
            
            if (identityValue.GetType() == targetType)
            {
                keyProp.SetValue(entity, identityValue);
                return;
            }

            if (identityValue is IConvertible convertible)
            {
                try
                {
                    var convertedValue = Convert.ChangeType(convertible, targetType);
                    keyProp.SetValue(entity, convertedValue);
                    return;
                }
                catch (InvalidCastException)
                {
                }
            }

            if (targetType == typeof(int) || targetType == typeof(int?))
            {
                if (decimal.TryParse(identityValue.ToString(), out var decValue))
                {
                    keyProp.SetValue(entity, (int)decValue);
                    return;
                }
            }

            if (targetType == typeof(long) || targetType == typeof(long?))
            {
                if (decimal.TryParse(identityValue.ToString(), out var decValue))
                {
                    keyProp.SetValue(entity, (long)decValue);
                    return;
                }
            }

            keyProp.SetValue(entity, identityValue);
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
                statement.Parameters,
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
                statement.Parameters,
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

        /// <summary>
        /// 使用 Dapper DynamicParameters 执行查询
        /// </summary>
        protected IEnumerable<TEntity> QueryWithDynamicParams<TEntity>(string sql, DynamicParameters parameters)
            where TEntity : class
        {
            return Connection.Query<TEntity>(sql, parameters, transaction: Transaction);
        }

        /// <summary>
        /// 使用 Dapper DynamicParameters 执行命令
        /// </summary>
        protected int ExecuteWithDynamicParams(string sql, DynamicParameters parameters)
        {
            return Connection.Execute(sql, parameters, transaction: Transaction);
        }

        #endregion
    }
}
