using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace OpenRiaServices.Client.Auditing
{
    /// <summary>
    /// 表示实体变更的操作类型
    /// </summary>
    public enum EntityChangeOperation
    {
        /// <summary>
        /// 实体被添加
        /// </summary>
        Added,
        
        /// <summary>
        /// 实体被修改
        /// </summary>
        Modified,
        
        /// <summary>
        /// 实体被删除
        /// </summary>
        Removed
    }

    /// <summary>
    /// 表示单个属性的变更记录
    /// </summary>
    public class PropertyChangeEntry
    {
        /// <summary>
        /// 属性名称
        /// </summary>
        public string PropertyName { get; set; } = string.Empty;
        
        /// <summary>
        /// 原始值（修改前）
        /// </summary>
        public object? OriginalValue { get; set; }
        
        /// <summary>
        /// 新值（修改后）
        /// </summary>
        public object? NewValue { get; set; }
        
        /// <summary>
        /// 属性类型的全名
        /// </summary>
        public string? PropertyType { get; set; }
    }

    /// <summary>
    /// 表示单个实体的变更记录
    /// </summary>
    public class EntityChangeEntry
    {
        /// <summary>
        /// 实体类型的全名
        /// </summary>
        public string EntityTypeName { get; set; } = string.Empty;
        
        /// <summary>
        /// 实体类型的名称（短名称）
        /// </summary>
        public string EntityName { get; set; } = string.Empty;
        
        /// <summary>
        /// 变更操作类型
        /// </summary>
        public EntityChangeOperation Operation { get; set; }
        
        /// <summary>
        /// 实体的主键值字典（键为属性名，值为主键值）
        /// </summary>
        public Dictionary<string, object?> KeyValues { get; set; } = new Dictionary<string, object?>();
        
        /// <summary>
        /// 属性变更列表（对于 Added 操作，包含所有属性；对于 Modified 操作，只包含变更的属性；对于 Removed 操作，包含所有属性）
        /// </summary>
        public List<PropertyChangeEntry> PropertyChanges { get; set; } = new List<PropertyChangeEntry>();
        
        /// <summary>
        /// 实体的完整当前值（对于 Added 操作）
        /// </summary>
        public Dictionary<string, object?>? CurrentValues { get; set; }
        
        /// <summary>
        /// 实体的完整原始值（对于 Modified 和 Removed 操作）
        /// </summary>
        public Dictionary<string, object?>? OriginalValues { get; set; }
    }

    /// <summary>
    /// 表示一次 SubmitChanges 操作的完整审计日志
    /// </summary>
    public class ChangeAuditLog
    {
        /// <summary>
        /// 日志的唯一标识符
        /// </summary>
        public Guid LogId { get; set; } = Guid.NewGuid();
        
        /// <summary>
        /// 审计日志创建的时间戳（UTC）
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// 操作的描述（可选）
        /// </summary>
        public string? OperationDescription { get; set; }
        
        /// <summary>
        /// 执行操作的用户名（可选，由调用方设置）
        /// </summary>
        public string? UserName { get; set; }
        
        /// <summary>
        /// 新增的实体数量
        /// </summary>
        public int AddedCount => EntityChanges.Count(c => c.Operation == EntityChangeOperation.Added);
        
        /// <summary>
        /// 修改的实体数量
        /// </summary>
        public int ModifiedCount => EntityChanges.Count(c => c.Operation == EntityChangeOperation.Modified);
        
        /// <summary>
        /// 删除的实体数量
        /// </summary>
        public int RemovedCount => EntityChanges.Count(c => c.Operation == EntityChangeOperation.Removed);
        
        /// <summary>
        /// 变更的实体总数
        /// </summary>
        public int TotalChanges => EntityChanges.Count;
        
        /// <summary>
        /// 实体变更记录列表
        /// </summary>
        public List<EntityChangeEntry> EntityChanges { get; set; } = new List<EntityChangeEntry>();
        
        /// <summary>
        /// 自定义数据字典，可用于存储额外的上下文信息
        /// </summary>
        public Dictionary<string, object?> CustomData { get; set; } = new Dictionary<string, object?>();
    }
}
