using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using OpenRiaServices.Client.Internal;

#nullable enable

namespace OpenRiaServices.Client.Auditing
{
    /// <summary>
    /// 变更审计日志生成事件参数
    /// </summary>
    public class ChangeAuditLogGeneratedEventArgs : EventArgs
    {
        /// <summary>
        /// 生成的审计日志
        /// </summary>
        public ChangeAuditLog AuditLog { get; }
        
        /// <summary>
        /// 关联的变更集
        /// </summary>
        public EntityChangeSet ChangeSet { get; }
        
        /// <summary>
        /// 构造函数
        /// </summary>
        public ChangeAuditLogGeneratedEventArgs(ChangeAuditLog auditLog, EntityChangeSet changeSet)
        {
            AuditLog = auditLog;
            ChangeSet = changeSet;
        }
    }

    /// <summary>
    /// 支持变更审计日志的 DomainContext 基类
    /// 继承此类可以在每次 SubmitChanges 前自动收集变更实体的审计日志
    /// </summary>
    /// <example>
    /// 使用示例：
    /// <code>
    /// // 1. 让生成的 DomainContext 继承此类（或创建部分类）
    /// public partial class MyDomainContext : AuditableDomainContext
    /// {
    ///     // 构造函数
    ///     public MyDomainContext(Uri serviceUri) : base(serviceUri) { }
    ///     
    ///     // 如果有其他构造函数，确保调用基类构造函数
    /// }
    /// 
    /// // 2. 订阅审计日志事件
    /// var context = new MyDomainContext(serviceUri);
    /// context.ChangeAuditLogGenerated += (sender, e) =>
    /// {
    ///     // 获取结构化的审计日志
    ///     var log = e.AuditLog;
    ///     
    ///     // 可以设置用户名等上下文信息
    ///     log.UserName = "currentUser";
    ///     log.OperationDescription = "保存用户修改";
    ///     
    ///     // 序列化为 JSON
    ///     string json = log.ToJson();
    ///     
    ///     // 记录日志或发送到服务器
    ///     Console.WriteLine(json);
    /// };
    /// 
    /// // 3. 正常使用 SubmitChanges
    /// var entity = context.EntitySets.First();
    /// entity.Name = "新名称";
    /// context.SubmitChanges(); // 此时会自动触发审计日志事件
    /// </code>
    /// </example>
    public abstract class AuditableDomainContext : DomainContext
    {
        /// <summary>
        /// 当生成变更审计日志时触发的事件
        /// </summary>
        public event EventHandler<ChangeAuditLogGeneratedEventArgs>? ChangeAuditLogGenerated;

        /// <summary>
        /// 获取或设置当前用户名，会自动设置到生成的审计日志中
        /// </summary>
        public string? CurrentUserName { get; set; }

        /// <summary>
        /// 获取或设置操作描述，会自动设置到生成的审计日志中
        /// </summary>
        public string? OperationDescription { get; set; }

        /// <summary>
        /// 获取最后一次生成的审计日志
        /// </summary>
        public ChangeAuditLog? LastAuditLog { get; private set; }

        /// <summary>
        /// 获取或设置一个值，指示是否启用审计日志功能
        /// </summary>
        public bool IsAuditingEnabled { get; set; } = true;

        /// <summary>
        /// 受保护的构造函数
        /// </summary>
        protected AuditableDomainContext(DomainClient domainClient) : base(domainClient)
        {
        }

        /// <summary>
        /// 提交指定的挂起更改到 DomainService
        /// 所有 SubmitChanges 重载最终都会调用此方法，这是扩展的核心点
        /// </summary>
        protected override Task<SubmitResult> SubmitChangesAsync(EntityChangeSet changeSet, CancellationToken cancellationToken)
        {
            if (IsAuditingEnabled && !changeSet.IsEmpty)
            {
                GenerateAndPublishAuditLog(changeSet);
            }
            return base.SubmitChangesAsync(changeSet, cancellationToken);
        }

        /// <summary>
        /// 生成并发布审计日志
        /// </summary>
        private void GenerateAndPublishAuditLog(EntityChangeSet changeSet)
        {
            try
            {
                var auditLog = GenerateAuditLog(changeSet);
                
                // 自动设置上下文信息
                if (!string.IsNullOrEmpty(CurrentUserName))
                {
                    auditLog.UserName = CurrentUserName;
                }
                if (!string.IsNullOrEmpty(OperationDescription))
                {
                    auditLog.OperationDescription = OperationDescription;
                }

                LastAuditLog = auditLog;

                // 触发事件
                OnChangeAuditLogGenerated(auditLog, changeSet);
            }
            catch (Exception ex)
            {
                // 审计日志生成失败不应影响正常的提交操作
                // 可以在这里记录内部错误
                System.Diagnostics.Debug.WriteLine($"审计日志生成失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 生成审计日志的核心方法
        /// 可重写此方法以自定义审计日志的生成逻辑
        /// </summary>
        protected virtual ChangeAuditLog GenerateAuditLog(EntityChangeSet changeSet)
        {
            var auditLog = new ChangeAuditLog();

            // 处理新增的实体
            foreach (var entity in changeSet.AddedEntities)
            {
                var entry = CreateEntityChangeEntry(entity, EntityChangeOperation.Added);
                auditLog.EntityChanges.Add(entry);
            }

            // 处理修改的实体
            foreach (var entity in changeSet.ModifiedEntities)
            {
                var entry = CreateEntityChangeEntry(entity, EntityChangeOperation.Modified);
                auditLog.EntityChanges.Add(entry);
            }

            // 处理删除的实体
            foreach (var entity in changeSet.RemovedEntities)
            {
                var entry = CreateEntityChangeEntry(entity, EntityChangeOperation.Removed);
                auditLog.EntityChanges.Add(entry);
            }

            return auditLog;
        }

        /// <summary>
        /// 为单个实体创建变更记录
        /// </summary>
        protected virtual EntityChangeEntry CreateEntityChangeEntry(Entity entity, EntityChangeOperation operation)
        {
            var entry = new EntityChangeEntry
            {
                EntityTypeName = entity.GetType().FullName ?? entity.GetType().Name,
                EntityName = entity.GetType().Name,
                Operation = operation
            };

            var metaType = MetaType.GetMetaType(entity.GetType());

            // 收集主键值
            foreach (var keyMember in metaType.KeyMembers)
            {
                var value = keyMember.GetValue(entity);
                entry.KeyValues[keyMember.Name] = value;
            }

            // 根据操作类型收集属性变更
            switch (operation)
            {
                case EntityChangeOperation.Added:
                    CollectAddedEntityChanges(entity, metaType, entry);
                    break;
                case EntityChangeOperation.Modified:
                    CollectModifiedEntityChanges(entity, metaType, entry);
                    break;
                case EntityChangeOperation.Removed:
                    CollectRemovedEntityChanges(entity, metaType, entry);
                    break;
            }

            return entry;
        }

        /// <summary>
        /// 收集新增实体的属性变更
        /// </summary>
        private static void CollectAddedEntityChanges(Entity entity, MetaType metaType, EntityChangeEntry entry)
        {
            entry.CurrentValues = new Dictionary<string, object?>();
            
            foreach (var member in metaType.DataMembers)
            {
                // 跳过关联成员，只收集简单属性
                if (member.IsAssociationMember)
                    continue;

                var value = member.GetValue(entity);
                entry.CurrentValues[member.Name] = value;

                entry.PropertyChanges.Add(new PropertyChangeEntry
                {
                    PropertyName = member.Name,
                    OriginalValue = null,
                    NewValue = value,
                    PropertyType = member.PropertyType.FullName
                });
            }
        }

        /// <summary>
        /// 收集修改实体的属性变更
        /// </summary>
        private static void CollectModifiedEntityChanges(Entity entity, MetaType metaType, EntityChangeEntry entry)
        {
            var originalValues = GetOriginalValues(entity);
            entry.OriginalValues = new Dictionary<string, object?>();
            entry.CurrentValues = new Dictionary<string, object?>();

            if (originalValues != null)
            {
                foreach (var kvp in originalValues)
                {
                    var member = metaType[kvp.Key];
                    if (member == null || member.IsAssociationMember)
                        continue;

                    var currentValue = member.GetValue(entity);
                    var originalValue = kvp.Value;

                    // 记录原始值和当前值
                    entry.OriginalValues[kvp.Key] = originalValue;
                    entry.CurrentValues[kvp.Key] = currentValue;

                    // 检查值是否真的发生了变化
                    if (!Equals(originalValue, currentValue))
                    {
                        entry.PropertyChanges.Add(new PropertyChangeEntry
                        {
                            PropertyName = kvp.Key,
                            OriginalValue = originalValue,
                            NewValue = currentValue,
                            PropertyType = member.PropertyType.FullName
                        });
                    }
                }
            }

            // 如果没有通过 OriginalValues 找到变更，尝试检查所有数据成员
            if (entry.PropertyChanges.Count == 0)
            {
                foreach (var member in metaType.DataMembers)
                {
                    if (member.IsAssociationMember)
                        continue;

                    var currentValue = member.GetValue(entity);
                    
                    // 对于 Modified 状态但没有 OriginalValues 的情况，
                    // 我们只记录当前值（可能是复杂对象的变更）
                    entry.PropertyChanges.Add(new PropertyChangeEntry
                    {
                        PropertyName = member.Name,
                        OriginalValue = null,
                        NewValue = currentValue,
                        PropertyType = member.PropertyType.FullName
                    });
                }
            }
        }

        /// <summary>
        /// 收集删除实体的属性变更
        /// </summary>
        private static void CollectRemovedEntityChanges(Entity entity, MetaType metaType, EntityChangeEntry entry)
        {
            var originalValues = GetOriginalValues(entity);
            entry.OriginalValues = new Dictionary<string, object?>();

            // 优先使用 OriginalValues
            if (originalValues != null)
            {
                foreach (var kvp in originalValues)
                {
                    var member = metaType[kvp.Key];
                    if (member == null || member.IsAssociationMember)
                        continue;

                    entry.OriginalValues[kvp.Key] = kvp.Value;

                    entry.PropertyChanges.Add(new PropertyChangeEntry
                    {
                        PropertyName = kvp.Key,
                        OriginalValue = kvp.Value,
                        NewValue = null,
                        PropertyType = member.PropertyType.FullName
                    });
                }
            }

            // 如果 OriginalValues 为空，则收集所有当前属性
            if (entry.PropertyChanges.Count == 0)
            {
                foreach (var member in metaType.DataMembers)
                {
                    if (member.IsAssociationMember)
                        continue;

                    var value = member.GetValue(entity);
                    entry.OriginalValues[member.Name] = value;

                    entry.PropertyChanges.Add(new PropertyChangeEntry
                    {
                        PropertyName = member.Name,
                        OriginalValue = value,
                        NewValue = null,
                        PropertyType = member.PropertyType.FullName
                    });
                }
            }
        }

        /// <summary>
        /// 获取实体的原始值字典
        /// </summary>
        private static IDictionary<string, object?>? GetOriginalValues(Entity entity)
        {
            // 使用反射获取 internal 的 OriginalValues 属性
            var property = typeof(Entity).GetProperty("OriginalValues", 
                BindingFlags.Instance | BindingFlags.NonPublic);
            
            return property?.GetValue(entity) as IDictionary<string, object?>;
        }

        /// <summary>
        /// 触发 ChangeAuditLogGenerated 事件
        /// 可重写此方法以自定义事件触发逻辑
        /// </summary>
        protected virtual void OnChangeAuditLogGenerated(ChangeAuditLog auditLog, EntityChangeSet changeSet)
        {
            ChangeAuditLogGenerated?.Invoke(this, new ChangeAuditLogGeneratedEventArgs(auditLog, changeSet));
        }

        /// <summary>
        /// 手动生成审计日志（不触发提交）
        /// 可用于预览当前的变更情况
        /// </summary>
        public ChangeAuditLog? GenerateAuditLog()
        {
            if (!this.HasChanges)
                return null;

            var changeSet = this.EntityContainer.GetChanges();
            return GenerateAuditLog(changeSet);
        }
    }
}
