using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using Dapper;

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
        /// 生成 INSERT 语句（使用传统的 SCOPE_IDENTITY() 方式）
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

            var identityInfo = new IdentityColumnInfo();
            var identityKeyProp = _metadata.KeyProperties.FirstOrDefault(p => p.IsDatabaseGenerated);
            
            if (identityKeyProp != null)
            {
                identityInfo.HasIdentityColumn = true;
                identityInfo.ColumnName = identityKeyProp.ColumnName;
                identityInfo.PropertyName = identityKeyProp.PropertyName;
                identityInfo.PropertyType = identityKeyProp.PropertyType;
                sql.AppendLine(";SELECT CAST(SCOPE_IDENTITY() AS DECIMAL(18,0)) AS IdentityValue");
            }

            return new SqlStatement(sql.ToString(), parameters, identityInfo);
        }

        /// <summary>
        /// 生成 INSERT 语句（使用 OUTPUT 参数方式）
        /// </summary>
        public SqlStatement GenerateInsertWithOutput(object entity)
        {
            var insertColumns = new List<string>();
            var insertParams = new List<string>();
            var parameters = new DynamicParameters();
            var identityInfo = new IdentityColumnInfo();

            var identityKeyProp = _metadata.KeyProperties.FirstOrDefault(p => p.IsDatabaseGenerated);

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
            
            if (identityKeyProp != null)
            {
                identityInfo.HasIdentityColumn = true;
                identityInfo.ColumnName = identityKeyProp.ColumnName;
                identityInfo.PropertyName = identityKeyProp.PropertyName;
                identityInfo.PropertyType = identityKeyProp.PropertyType;
                identityInfo.UseOutputParameter = true;

                sql.AppendLine($"INSERT INTO {_metadata.TableName}");
                sql.AppendLine($"({string.Join(", ", insertColumns)})");
                sql.AppendLine($"OUTPUT INSERTED.[{identityKeyProp.ColumnName}]");
                sql.AppendLine($"VALUES ({string.Join(", ", insertParams)})");
            }
            else
            {
                sql.AppendLine($"INSERT INTO {_metadata.TableName}");
                sql.AppendLine($"({string.Join(", ", insertColumns)})");
                sql.AppendLine($"VALUES ({string.Join(", ", insertParams)})");
            }

            return new SqlStatement(sql.ToString(), parameters, identityInfo);
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

            return new SqlStatement(sql.ToString(), parameters, IdentityColumnInfo.None);
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

            return new SqlStatement(sql.ToString(), parameters, IdentityColumnInfo.None);
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

            return new SqlStatement(sql, parameters, IdentityColumnInfo.None);
        }

        /// <summary>
        /// 生成根据主键查询的 SELECT 语句
        /// </summary>
        public SqlStatement GenerateSelectById(object keyValues)
        {
            var whereClauses = new List<string>();
            var parameters = new DynamicParameters();

            if (keyValues is IDictionary<string, object?> dict)
            {
                foreach (var kvp in dict)
                {
                    whereClauses.Add($"[{kvp.Key}] = @{kvp.Key}");
                    parameters.Add(kvp.Key, kvp.Value);
                }
            }
            else
            {
                var props = keyValues.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
                foreach (var prop in props)
                {
                    whereClauses.Add($"[{prop.Name}] = @{prop.Name}");
                    parameters.Add(prop.Name, prop.GetValue(keyValues));
                }
            }

            var columns = string.Join(", ", _metadata.AllProperties.Select(p => $"[{p.ColumnName}]"));
            var sql = $"SELECT {columns} FROM {_metadata.TableName} WHERE {string.Join(" AND ", whereClauses)}";

            return new SqlStatement(sql, parameters, IdentityColumnInfo.None);
        }

        /// <summary>
        /// 生成查询所有记录的 SELECT 语句
        /// </summary>
        public SqlStatement GenerateSelectAll()
        {
            var columns = string.Join(", ", _metadata.AllProperties.Select(p => $"[{p.ColumnName}]"));
            var sql = $"SELECT {columns} FROM {_metadata.TableName}";
            return new SqlStatement(sql, new DynamicParameters(), IdentityColumnInfo.None);
        }
    }

    /// <summary>
    /// SQL 语句和参数
    /// </summary>
    public class SqlStatement
    {
        public static readonly SqlStatement Empty = new SqlStatement(string.Empty, new DynamicParameters(), IdentityColumnInfo.None);

        public string Sql { get; }
        public DynamicParameters Parameters { get; }
        public IdentityColumnInfo IdentityInfo { get; }

        public bool IsEmpty => string.IsNullOrEmpty(Sql);

        public SqlStatement(string sql, DynamicParameters parameters, IdentityColumnInfo identityInfo)
        {
            Sql = sql;
            Parameters = parameters;
            IdentityInfo = identityInfo;
        }
    }

    /// <summary>
    /// 自增列信息
    /// </summary>
    public class IdentityColumnInfo
    {
        public static readonly IdentityColumnInfo None = new IdentityColumnInfo();

        public bool HasIdentityColumn { get; set; }
        public string? ColumnName { get; set; }
        public string? PropertyName { get; set; }
        public Type? PropertyType { get; set; }
        public bool UseOutputParameter { get; set; }
    }
}
