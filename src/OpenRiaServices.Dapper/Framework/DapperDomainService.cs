using System;
using System.Collections;
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
    /// 用于 Dapper 数据访问的 DomainService 基类
    /// </summary>
    /// <typeparam name="TConnection">数据库连接类型</typeparam>
    public abstract class DapperDomainService<TConnection> : DomainService
        where TConnection : DbConnection, new()
    {
        private TConnection? _connection;
        private DbTransaction? _transaction;
        private readonly string _connectionString;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="connectionString">数据库连接字符串</param>
        protected DapperDomainService(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        /// <summary>
        /// 初始化 DomainService
        /// </summary>
        /// <param name="context">DomainService 上下文</param>
        public override void Initialize(DomainServiceContext context)
        {
            base.Initialize(context);
        }

        /// <summary>
        /// 获取数据库连接
        /// </summary>
        protected internal TConnection Connection
        {
            get
            {
                if (_connection == null)
                {
                    _connection = CreateConnection();
                    _connection.Open();
                }
                return _connection;
            }
        }

        /// <summary>
        /// 获取当前事务（如果存在）
        /// </summary>
        protected internal DbTransaction? Transaction => _transaction;

        /// <summary>
        /// 创建数据库连接
        /// </summary>
        /// <returns>数据库连接实例</returns>
        protected virtual TConnection CreateConnection()
        {
            var connection = new TConnection();
            connection.ConnectionString = _connectionString;
            return connection;
        }

        /// <summary>
        /// 开始事务
        /// </summary>
        protected virtual void BeginTransaction()
        {
            if (_transaction == null)
            {
                _transaction = Connection.BeginTransaction();
            }
        }

        /// <summary>
        /// 提交事务
        /// </summary>
        protected virtual void CommitTransaction()
        {
            if (_transaction != null)
            {
                _transaction.Commit();
                _transaction.Dispose();
                _transaction = null;
            }
        }

        /// <summary>
        /// 回滚事务
        /// </summary>
        protected virtual void RollbackTransaction()
        {
            if (_transaction != null)
            {
                _transaction.Rollback();
                _transaction.Dispose();
                _transaction = null;
            }
        }

        /// <summary>
        /// 获取查询结果总数
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="query">查询</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>总数</returns>
        protected override ValueTask<int> CountAsync<T>(IQueryable<T> query, CancellationToken cancellationToken)
        {
            return new ValueTask<int>(query.Count());
        }

        /// <summary>
        /// 枚举查询结果以确保执行
        /// </summary>
        /// <typeparam name="T">元素类型</typeparam>
        /// <param name="enumerable">可枚举对象</param>
        /// <param name="estimatedResultCount">预估结果数量</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>结果集合</returns>
        protected override ValueTask<IReadOnlyCollection<T>> EnumerateAsync<T>(IEnumerable enumerable, int estimatedResultCount, CancellationToken cancellationToken)
        {
            if (enumerable is T[] array)
            {
                return new ValueTask<IReadOnlyCollection<T>>(array);
            }

            if (enumerable is ICollection<T> collection)
            {
                if (collection is IReadOnlyCollection<T> readonlyCollection)
                {
                    return new ValueTask<IReadOnlyCollection<T>>(readonlyCollection);
                }
                return new ValueTask<IReadOnlyCollection<T>>(new List<T>(collection));
            }

            List<T> result = new List<T>(capacity: estimatedResultCount);
            foreach (var item in enumerable)
            {
                if (item is T typedItem)
                {
                    result.Add(typedItem);
                }
            }
            return new ValueTask<IReadOnlyCollection<T>>(result);
        }

        /// <summary>
        /// 持久化 ChangeSet 中的更改
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>如果成功返回 true</returns>
        protected override async ValueTask<bool> PersistChangeSetAsync(CancellationToken cancellationToken)
        {
            return await new ValueTask<bool>(InvokeSaveChanges(true, cancellationToken));
        }

        /// <summary>
        /// 执行保存更改
        /// </summary>
        /// <param name="retryOnConflict">是否在冲突后重试</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>如果成功返回 true</returns>
        private bool InvokeSaveChanges(bool retryOnConflict, CancellationToken cancellationToken)
        {
            try
            {
                BeginTransaction();

                foreach (var entry in ChangeSet!.ChangeSetEntries)
                {
                    switch (entry.Operation)
                    {
                        case DomainOperation.Insert:
                            ProcessInsert(entry);
                            break;
                        case DomainOperation.Update:
                            ProcessUpdate(entry);
                            break;
                        case DomainOperation.Delete:
                            ProcessDelete(entry);
                            break;
                    }
                }

                CommitTransaction();
            }
            catch (DBConcurrencyException ex)
            {
                RollbackTransaction();
                HandleConcurrencyException(ex);

                if (retryOnConflict && ResolveConflicts())
                {
                    foreach (ChangeSetEntry entry in this.ChangeSet.ChangeSetEntries)
                    {
                        entry.StoreEntity = null;
                        entry.ConflictMembers = null;
                        entry.IsDeleteConflict = false;
                    }
                    return InvokeSaveChanges(false, cancellationToken);
                }

                this.OnError(new DomainServiceErrorInfo(ex));

                if (!this.ChangeSet.HasError)
                {
                    throw;
                }
                return false;
            }
            catch
            {
                RollbackTransaction();
                throw;
            }

            return true;
        }

        /// <summary>
        /// 处理插入操作
        /// </summary>
        /// <param name="entry">ChangeSet 条目</param>
        private void ProcessInsert(ChangeSetEntry entry)
        {
            OnInserting(entry.Entity);
            InsertEntity(entry.Entity);
            OnInserted(entry.Entity);
        }

        /// <summary>
        /// 处理更新操作
        /// </summary>
        /// <param name="entry">ChangeSet 条目</param>
        private void ProcessUpdate(ChangeSetEntry entry)
        {
            OnUpdating(entry.Entity);
            UpdateEntity(entry.Entity, entry.OriginalEntity);
            OnUpdated(entry.Entity);
        }

        /// <summary>
        /// 处理删除操作
        /// </summary>
        /// <param name="entry">ChangeSet 条目</param>
        private void ProcessDelete(ChangeSetEntry entry)
        {
            OnDeleting(entry.Entity);
            DeleteEntity(entry.Entity);
            OnDeleted(entry.Entity);
        }

        /// <summary>
        /// 处理并发异常
        /// </summary>
        /// <param name="ex">并发异常</param>
        private void HandleConcurrencyException(DBConcurrencyException ex)
        {
            foreach (DataRow row in ex.Row)
            {
                var entity = FindEntityByDataRow(row);
                if (entity != null)
                {
                    var entry = this.ChangeSet!.ChangeSetEntries.FirstOrDefault(e => object.ReferenceEquals(e.Entity, entity));
                    if (entry != null)
                    {
                        var dbValues = row[DataRowVersion.Original];
                        entry.IsDeleteConflict = dbValues == null;
                        if (!entry.IsDeleteConflict)
                        {
                            entry.StoreEntity = CreateStoreEntityFromDataRow(row);
                            entry.ConflictMembers = GetConflictMembers(row);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 解析并发冲突
        /// </summary>
        /// <returns>如果所有冲突已解析返回 true</returns>
        protected virtual bool ResolveConflicts()
        {
            return false;
        }

        /// <summary>
        /// 插入实体时调用
        /// </summary>
        /// <param name="entity">实体</param>
        protected virtual void OnInserting(object entity) { }

        /// <summary>
        /// 插入实体后调用
        /// </summary>
        /// <param name="entity">实体</param>
        protected virtual void OnInserted(object entity) { }

        /// <summary>
        /// 更新实体时调用
        /// </summary>
        /// <param name="entity">实体</param>
        protected virtual void OnUpdating(object entity) { }

        /// <summary>
        /// 更新实体后调用
        /// </summary>
        /// <param name="entity">实体</param>
        protected virtual void OnUpdated(object entity) { }

        /// <summary>
        /// 删除实体时调用
        /// </summary>
        /// <param name="entity">实体</param>
        protected virtual void OnDeleting(object entity) { }

        /// <summary>
        /// 删除实体后调用
        /// </summary>
        /// <param name="entity">实体</param>
        protected virtual void OnDeleted(object entity) { }

        /// <summary>
        /// 插入实体到数据库
        /// </summary>
        /// <param name="entity">实体</param>
        protected abstract void InsertEntity(object entity);

        /// <summary>
        /// 更新数据库中的实体
        /// </summary>
        /// <param name="entity">当前实体</param>
        /// <param name="originalEntity">原始实体</param>
        protected abstract void UpdateEntity(object entity, object? originalEntity);

        /// <summary>
        /// 从数据库删除实体
        /// </summary>
        /// <param name="entity">实体</param>
        protected abstract void DeleteEntity(object entity);

        /// <summary>
        /// 通过 DataRow 查找实体（用于并发冲突处理）
        /// </summary>
        /// <param name="row">数据行</param>
        /// <returns>实体对象</returns>
        protected virtual object? FindEntityByDataRow(DataRow row)
        {
            return null;
        }

        /// <summary>
        /// 从 DataRow 创建存储实体（用于并发冲突处理）
        /// </summary>
        /// <param name="row">数据行</param>
        /// <returns>存储实体</returns>
        protected virtual object? CreateStoreEntityFromDataRow(DataRow row)
        {
            return null;
        }

        /// <summary>
        /// 获取冲突的成员列表（用于并发冲突处理）
        /// </summary>
        /// <param name="row">数据行</param>
        /// <returns>冲突成员名称列表</returns>
        protected virtual List<string> GetConflictMembers(DataRow row)
        {
            return new List<string>();
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        /// <param name="disposing">是否正在释放</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_transaction != null)
                {
                    _transaction.Dispose();
                    _transaction = null;
                }
                if (_connection != null)
                {
                    _connection.Dispose();
                    _connection = null;
                }
            }
            base.Dispose(disposing);
        }
    }
}
