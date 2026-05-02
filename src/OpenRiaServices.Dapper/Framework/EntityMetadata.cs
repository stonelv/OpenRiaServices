using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;

namespace OpenRiaServices.Dapper
{
    /// <summary>
    /// 实体元数据信息
    /// </summary>
    public class EntityMetadata
    {
        private static readonly ConcurrentDictionary<Type, EntityMetadata> _metadataCache = new ConcurrentDictionary<Type, EntityMetadata>();

        /// <summary>
        /// 实体类型
        /// </summary>
        public Type EntityType { get; }

        /// <summary>
        /// 表名
        /// </summary>
        public string TableName { get; }

        /// <summary>
        /// 主键属性列表
        /// </summary>
        public List<PropertyMetadata> KeyProperties { get; } = new List<PropertyMetadata>();

        /// <summary>
        /// 所有可写属性（非主键且非计算列）
        /// </summary>
        public List<PropertyMetadata> WritableProperties { get; } = new List<PropertyMetadata>();

        /// <summary>
        /// 所有属性
        /// </summary>
        public List<PropertyMetadata> AllProperties { get; } = new List<PropertyMetadata>();

        /// <summary>
        /// 时间戳/并发列属性
        /// </summary>
        public PropertyMetadata? TimestampProperty { get; private set; }

        /// <summary>
        /// 数据库生成的属性（如自增 ID）
        /// </summary>
        public List<PropertyMetadata> DatabaseGeneratedProperties { get; } = new List<PropertyMetadata>();

        private EntityMetadata(Type entityType)
        {
            EntityType = entityType;
            TableName = GetTableName(entityType);
            AnalyzeProperties(entityType);
        }

        /// <summary>
        /// 获取实体类型的元数据
        /// </summary>
        public static EntityMetadata GetMetadata<TEntity>()
        {
            return GetMetadata(typeof(TEntity));
        }

        /// <summary>
        /// 获取实体类型的元数据
        /// </summary>
        public static EntityMetadata GetMetadata(Type entityType)
        {
            return _metadataCache.GetOrAdd(entityType, t => new EntityMetadata(t));
        }

        /// <summary>
        /// 获取表名
        /// </summary>
        private static string GetTableName(Type entityType)
        {
            var tableAttr = entityType.GetCustomAttribute<TableAttribute>();
            if (tableAttr != null)
            {
                if (!string.IsNullOrEmpty(tableAttr.Schema))
                {
                    return $"[{tableAttr.Schema}].[{tableAttr.Name}]";
                }
                return $"[{tableAttr.Name}]";
            }
            return $"[{entityType.Name}]";
        }

        /// <summary>
        /// 分析属性
        /// </summary>
        private void AnalyzeProperties(Type entityType)
        {
            var properties = entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.GetGetMethod() != null);

            foreach (var prop in properties)
            {
                var propMetadata = new PropertyMetadata(prop);
                AllProperties.Add(propMetadata);

                if (propMetadata.IsKey)
                {
                    KeyProperties.Add(propMetadata);
                }

                if (propMetadata.IsTimestamp)
                {
                    TimestampProperty = propMetadata;
                }

                if (propMetadata.IsDatabaseGenerated)
                {
                    DatabaseGeneratedProperties.Add(propMetadata);
                }

                if (!propMetadata.IsKey && !propMetadata.IsDatabaseGenerated && !propMetadata.IsNotMapped)
                {
                    WritableProperties.Add(propMetadata);
                }
            }
        }

        /// <summary>
        /// 获取主键值字典
        /// </summary>
        public Dictionary<string, object?> GetKeyValues(object entity)
        {
            var result = new Dictionary<string, object?>();
            foreach (var keyProp in KeyProperties)
            {
                result[keyProp.ColumnName] = keyProp.GetValue(entity);
            }
            return result;
        }

        /// <summary>
        /// 检查是否有复合主键
        /// </summary>
        public bool HasCompositeKey => KeyProperties.Count > 1;

        /// <summary>
        /// 检查是否有时间戳列
        /// </summary>
        public bool HasTimestamp => TimestampProperty != null;
    }

    /// <summary>
    /// 属性元数据信息
    /// </summary>
    public class PropertyMetadata
    {
        /// <summary>
        /// 属性信息
        /// </summary>
        public PropertyInfo Property { get; }

        /// <summary>
        /// 属性名称
        /// </summary>
        public string PropertyName { get; }

        /// <summary>
        /// 列名
        /// </summary>
        public string ColumnName { get; }

        /// <summary>
        /// 属性类型
        /// </summary>
        public Type PropertyType { get; }

        /// <summary>
        /// 是否为主键
        /// </summary>
        public bool IsKey { get; }

        /// <summary>
        /// 是否为时间戳/并发列
        /// </summary>
        public bool IsTimestamp { get; }

        /// <summary>
        /// 是否为数据库生成（如自增）
        /// </summary>
        public bool IsDatabaseGenerated { get; }

        /// <summary>
        /// 是否不映射到数据库
        /// </summary>
        public bool IsNotMapped { get; }

        /// <summary>
        /// 是否可写
        /// </summary>
        public bool CanWrite => Property.CanWrite && Property.GetSetMethod() != null;

        public PropertyMetadata(PropertyInfo property)
        {
            Property = property;
            PropertyName = property.Name;
            PropertyType = property.PropertyType;

            var columnAttr = property.GetCustomAttribute<ColumnAttribute>();
            ColumnName = columnAttr?.Name ?? property.Name;

            IsKey = property.GetCustomAttribute<KeyAttribute>() != null;

            var timestampAttr = property.GetCustomAttribute<TimestampAttribute>();
            IsTimestamp = timestampAttr != null || 
                          (property.PropertyType == typeof(byte[]) && 
                           property.GetCustomAttribute<ConcurrencyCheckAttribute>() != null);

            var databaseGeneratedAttr = property.GetCustomAttribute<DatabaseGeneratedAttribute>();
            IsDatabaseGenerated = databaseGeneratedAttr != null && 
                                   databaseGeneratedAttr.DatabaseGeneratedOption != DatabaseGeneratedOption.None;

            IsNotMapped = property.GetCustomAttribute<NotMappedAttribute>() != null;
        }

        /// <summary>
        /// 获取属性值
        /// </summary>
        public object? GetValue(object entity)
        {
            return Property.GetValue(entity);
        }

        /// <summary>
        /// 设置属性值
        /// </summary>
        public void SetValue(object entity, object? value)
        {
            if (CanWrite)
            {
                Property.SetValue(entity, value);
            }
        }
    }
}
