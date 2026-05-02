using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OpenRiaServices.Dapper
{
    /// <summary>
    /// SQL 语句生成器
    /// </summary>
    public class SqlGenerator
    {
        private readonly EntityMetadata _metadata;

        public SqlGenerator(EntityMetadata metadata)
        {
            _metadata = metadata;
        }

        /// <summary>
        /// 生成 INSERT 语句
        /// </summary>
        public SqlStatement GenerateInsert(object entity)
        {
            var insertColumns = new List<string>();
            var insertParams = new List<string>();
            var parameters = new DynamicParameters();

            foreach (var prop in _metadata.KeyProperties)
            {
                if (!prop.IsDatabaseGenerated)
                {
                    insertColumns.Add($"[{prop.ColumnName}]");
                    insertParams.Add($"@{prop.PropertyName}");
                    parameters.Add(prop.PropertyName, prop.GetValue(entity));
                }
            }

            foreach (var prop in _metadata.WritableProperties)
            {
                insertColumns.Add($"[{prop.ColumnName}]");
                insertParams.Add($"@{prop.PropertyName}");
                parameters.Add(prop.PropertyName, prop.GetValue(entity));
            }

            var sql = new StringBuilder();
            sql.AppendLine($"INSERT INTO {_metadata.TableName}");
            sql.AppendLine($"({string.Join(", ", insertColumns)})");
            sql.AppendLine($"VALUES ({string.Join(", ", insertParams)})");

            if (_metadata.DatabaseGeneratedProperties.Any(p => p.IsKey))
            {
                sql.AppendLine(";SELECT SCOPE_IDENTITY() AS Id");
            }

            return new SqlStatement(sql.ToString(), parameters);
        }

        /// <summary>
        /// 生成 UPDATE 语句（全量更新）
        /// </summary>
        public SqlStatement GenerateUpdate(object entity)
        {
            var setClauses = new List<string>();
            var parameters = new DynamicParameters();

            foreach (var prop in _metadata.WritableProperties)
            {
                setClauses.Add($"[{prop.ColumnName}] = @{prop.PropertyName}");
                parameters.Add(prop.PropertyName, prop.GetValue(entity));
            }

            var whereClauses = new List<string>();
            foreach (var keyProp in _metadata.KeyProperties)
            {
                whereClauses.Add($"[{keyProp.ColumnName}] = @{keyProp.PropertyName}_Key");
                parameters.Add($"{keyProp.PropertyName}_Key", keyProp.GetValue(entity));
            }

            if (_metadata.HasTimestamp && _metadata.TimestampProperty != null)
            {
                whereClauses.Add($"[{_metadata.TimestampProperty.ColumnName}] = @{_metadata.TimestampProperty.PropertyName}_Original");
            }

            var sql = new StringBuilder();
            sql.AppendLine($"UPDATE {_metadata.TableName}");
            sql.AppendLine($"SET {string.Join(", ", setClauses)}");
            sql.AppendLine($"WHERE {string.Join(" AND ", whereClauses)}");

            return new SqlStatement(sql.ToString(), parameters);
        }

        /// <summary>
        /// 生成 UPDATE 语句（仅更新变更的属性）
        /// </summary>
        public SqlStatement GenerateUpdate(object entity, object? originalEntity)
        {
            if (originalEntity == null)
            {
                return GenerateUpdate(entity);
            }

            var setClauses = new List<string>();
            var parameters = new DynamicParameters();

            foreach (var prop in _metadata.WritableProperties)
            {
                var currentValue = prop.GetValue(entity);
                var originalValue = prop.GetValue(originalEntity);

                if (!Equals(currentValue, originalValue))
                {
                    setClauses.Add($"[{prop.ColumnName}] = @{prop.PropertyName}");
                    parameters.Add(prop.PropertyName, currentValue);
                }
            }

            if (setClauses.Count == 0)
            {
                return SqlStatement.Empty;
            }

            var whereClauses = new List<string>();
            foreach (var keyProp in _metadata.KeyProperties)
            {
                whereClauses.Add($"[{keyProp.ColumnName}] = @{keyProp.PropertyName}_Key");
                parameters.Add($"{keyProp.PropertyName}_Key", keyProp.GetValue(originalEntity));
            }

            if (_metadata.HasTimestamp && _metadata.TimestampProperty != null)
            {
                var originalTimestamp = _metadata.TimestampProperty.GetValue(originalEntity);
                whereClauses.Add($"[{_metadata.TimestampProperty.ColumnName}] = @{_metadata.TimestampProperty.PropertyName}_Original");
                parameters.Add($"{_metadata.TimestampProperty.PropertyName}_Original", originalTimestamp);
            }

            var sql = new StringBuilder();
            sql.AppendLine($"UPDATE {_metadata.TableName}");
            sql.AppendLine($"SET {string.Join(", ", setClauses)}");
            sql.AppendLine($"WHERE {string.Join(" AND ", whereClauses)}");

            return new SqlStatement(sql.ToString(), parameters);
        }

        /// <summary>
        /// 生成 DELETE 语句
        /// </summary>
        public SqlStatement GenerateDelete(object entity)
        {
            var whereClauses = new List<string>();
            var parameters = new DynamicParameters();

            foreach (var keyProp in _metadata.KeyProperties)
            {
                whereClauses.Add($"[{keyProp.ColumnName}] = @{keyProp.PropertyName}");
                parameters.Add(keyProp.PropertyName, keyProp.GetValue(entity));
            }

            if (_metadata.HasTimestamp && _metadata.TimestampProperty != null)
            {
                var timestampValue = _metadata.TimestampProperty.GetValue(entity);
                whereClauses.Add($"[{_metadata.TimestampProperty.ColumnName}] = @{_metadata.TimestampProperty.PropertyName}");
                parameters.Add(_metadata.TimestampProperty.PropertyName, timestampValue);
            }

            var sql = $"DELETE FROM {_metadata.TableName} WHERE {string.Join(" AND ", whereClauses)}";

            return new SqlStatement(sql, parameters);
        }

        /// <summary>
        /// 生成根据主键查询的 SELECT 语句
        /// </summary>
        public SqlStatement GenerateSelectById(object keyValues)
        {
            var whereClauses = new List<string>();
            var parameters = new DynamicParameters();

            foreach (var keyProp in _metadata.KeyProperties)
            {
                whereClauses.Add($"[{keyProp.ColumnName}] = @{keyProp.PropertyName}");
            }

            var columns = string.Join(", ", _metadata.AllProperties.Select(p => $"[{p.ColumnName}]"));
            var sql = $"SELECT {columns} FROM {_metadata.TableName} WHERE {string.Join(" AND ", whereClauses)}";

            return new SqlStatement(sql, parameters);
        }

        /// <summary>
        /// 生成查询所有记录的 SELECT 语句
        /// </summary>
        public SqlStatement GenerateSelectAll()
        {
            var columns = string.Join(", ", _metadata.AllProperties.Select(p => $"[{p.ColumnName}]"));
            var sql = $"SELECT {columns} FROM {_metadata.TableName}";
            return new SqlStatement(sql, DynamicParameters.Empty);
        }
    }

    /// <summary>
    /// SQL 语句和参数
    /// </summary>
    public class SqlStatement
    {
        public static readonly SqlStatement Empty = new SqlStatement(string.Empty, DynamicParameters.Empty);

        public string Sql { get; }
        public DynamicParameters Parameters { get; }

        public bool IsEmpty => string.IsNullOrEmpty(Sql);

        public SqlStatement(string sql, DynamicParameters parameters)
        {
            Sql = sql;
            Parameters = parameters;
        }
    }

    /// <summary>
    /// 动态参数集合
    /// </summary>
    public class DynamicParameters
    {
        public static readonly DynamicParameters Empty = new DynamicParameters();

        private readonly Dictionary<string, object?> _parameters = new Dictionary<string, object?>();

        public IReadOnlyDictionary<string, object?> Parameters => _parameters;

        public DynamicParameters()
        {
        }

        public DynamicParameters(object obj)
        {
            if (obj != null)
            {
                var props = obj.GetType().GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                foreach (var prop in props)
                {
                    _parameters[prop.Name] = prop.GetValue(obj);
                }
            }
        }

        public void Add(string name, object? value)
        {
            _parameters[name] = value ?? DBNull.Value;
        }

        public T? Get<T>(string name)
        {
            if (_parameters.TryGetValue(name, out var value))
            {
                if (value == null || value == DBNull.Value)
                {
                    return default;
                }
                return (T)Convert.ChangeType(value, typeof(T));
            }
            return default;
        }

        public object? ToDynamicParameters()
        {
            return this;
        }
    }
}
