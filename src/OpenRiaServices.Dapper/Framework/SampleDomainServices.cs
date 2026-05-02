using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Linq;
using OpenRiaServices.Server;

namespace OpenRiaServices.Dapper.Sample
{
    /// <summary>
    /// 产品 DomainService 示例
    /// 使用 AutoDapperDomainService 自动处理 CRUD
    /// </summary>
    [EnableClientAccess]
    public class ProductDomainService : AutoDapperDomainService<SqlConnection>
    {
        private const string ConnectionStringName = "DefaultConnection";

        public ProductDomainService()
            : base(GetConnectionString())
        {
        }

        private static string GetConnectionString()
        {
            return "Server=localhost;Database=SampleDb;Integrated Security=True;";
        }

        #region 查询方法

        /// <summary>
        /// 获取所有产品
        /// </summary>
        public IQueryable<Product> GetProducts()
        {
            return QueryAll<Product>().AsQueryable();
        }

        /// <summary>
        /// 根据 ID 获取单个产品
        /// </summary>
        public Product? GetProductById(int productId)
        {
            return QueryById<Product>(new { ProductId = productId });
        }

        /// <summary>
        /// 使用自定义 SQL 查询产品
        /// </summary>
        public IEnumerable<Product> GetProductsByCategory(int categoryId)
        {
            var sql = @"
                SELECT p.* 
                FROM dbo.Products p 
                WHERE p.CategoryId = @CategoryId
                ORDER BY p.ProductName";
            
            return Query<Product>(sql, new { CategoryId = categoryId });
        }

        /// <summary>
        /// 使用自定义 SQL 执行复杂查询
        /// </summary>
        public IEnumerable<Product> GetExpensiveProducts(decimal minPrice)
        {
            var sql = @"
                SELECT * FROM dbo.Products 
                WHERE Price >= @MinPrice
                ORDER BY Price DESC";
            
            return Query<Product>(sql, new { MinPrice = minPrice });
        }

        #endregion

        #region 自定义操作

        /// <summary>
        /// 自定义操作：更新产品价格
        /// </summary>
        [EntityAction]
        public void UpdatePrice(Product product, decimal newPrice)
        {
            var sql = @"
                UPDATE dbo.Products 
                SET Price = @NewPrice 
                WHERE ProductId = @ProductId";
            
            Execute(sql, new { NewPrice = newPrice, ProductId = product.ProductId });
        }

        /// <summary>
        /// 自定义调用方法
        /// </summary>
        [Invoke]
        public int GetProductCount()
        {
            var sql = "SELECT COUNT(*) FROM dbo.Products";
            return Connection.ExecuteScalar<int>(sql, transaction: Transaction);
        }

        #endregion
    }

    /// <summary>
    /// 订单 DomainService 示例
    /// 展示更复杂的场景
    /// </summary>
    [EnableClientAccess]
    public class OrderDomainService : AutoDapperDomainService<SqlConnection>
    {
        public OrderDomainService()
            : base("Server=localhost;Database=SampleDb;Integrated Security=True;")
        {
        }

        #region 查询方法

        public IQueryable<Order> GetOrders()
        {
            return QueryAll<Order>().AsQueryable();
        }

        public IEnumerable<Order> GetOrdersByCustomer(string customerName)
        {
            var sql = @"
                SELECT * FROM dbo.Orders 
                WHERE CustomerName LIKE @CustomerName
                ORDER BY OrderDate DESC";
            
            return Query<Order>(sql, new { CustomerName = $"%{customerName}%" });
        }

        #endregion

        #region 重写 CRUD 方法（自定义逻辑）

        /// <summary>
        /// 重写插入实体，添加自定义逻辑
        /// </summary>
        protected override void InsertEntity(object entity)
        {
            if (entity is Order order)
            {
                if (order.OrderDate == default)
                {
                    order.OrderDate = DateTime.UtcNow;
                }
            }
            base.InsertEntity(entity);
        }

        /// <summary>
        /// 重写更新实体，添加自定义逻辑
        /// </summary>
        protected override void UpdateEntity(object entity, object? originalEntity)
        {
            if (entity is Order order)
            {
                order.TotalAmount = CalculateTotal(order);
            }
            base.UpdateEntity(entity, originalEntity);
        }

        /// <summary>
        /// 重写删除实体，添加自定义逻辑
        /// </summary>
        protected override void DeleteEntity(object entity)
        {
            if (entity is Order order)
            {
                if (order.Status != 0)
                {
                    throw new InvalidOperationException("只能删除未处理的订单");
                }
            }
            base.DeleteEntity(entity);
        }

        private decimal CalculateTotal(Order order)
        {
            return order.TotalAmount;
        }

        #endregion

        #region 并发冲突处理

        /// <summary>
        /// 重写并发冲突解决方法
        /// </summary>
        protected override bool ResolveConflicts()
        {
            foreach (var entry in ChangeSet!.ChangeSetEntries)
            {
                if (entry.HasConflict)
                {
                    var storeEntity = entry.StoreEntity;
                    var clientEntity = entry.Entity;
                    
                    if (entry.IsDeleteConflict)
                    {
                        throw new DomainException("该记录已被其他用户删除");
                    }

                    foreach (var conflictMember in entry.ConflictMembers ?? Enumerable.Empty<string>())
                    {
                        System.Diagnostics.Debug.WriteLine($"冲突字段: {conflictMember}");
                    }
                }
            }
            return base.ResolveConflicts();
        }

        #endregion
    }

    /// <summary>
    /// 手动实现 CRUD 的示例
    /// 展示如何直接使用 DapperDomainService 基类
    /// </summary>
    [EnableClientAccess]
    public class ManualProductDomainService : DapperDomainService<SqlConnection>
    {
        public ManualProductDomainService()
            : base("Server=localhost;Database=SampleDb;Integrated Security=True;")
        {
        }

        #region 查询方法

        public IQueryable<Product> GetProducts()
        {
            var sql = "SELECT * FROM dbo.Products";
            return Connection.Query<Product>(sql, transaction: Transaction).AsQueryable();
        }

        #endregion

        #region 手动实现 CRUD

        protected override void InsertEntity(object entity)
        {
            if (entity is Product product)
            {
                var sql = @"
                    INSERT INTO dbo.Products (ProductName, Price, CategoryId)
                    VALUES (@Name, @Price, @CategoryId);
                    SELECT CAST(SCOPE_IDENTITY() AS int)";
                
                var newId = Connection.ExecuteScalar<int>(sql, product, transaction: Transaction);
                product.ProductId = newId;
            }
        }

        protected override void UpdateEntity(object entity, object? originalEntity)
        {
            if (entity is Product product)
            {
                var sql = @"
                    UPDATE dbo.Products 
                    SET ProductName = @Name, 
                        Price = @Price, 
                        CategoryId = @CategoryId
                    WHERE ProductId = @ProductId
                      AND RowVersion = @RowVersion";
                
                var rowsAffected = Connection.Execute(sql, product, transaction: Transaction);
                if (rowsAffected == 0)
                {
                    throw new DBConcurrencyException("并发冲突");
                }
            }
        }

        protected override void DeleteEntity(object entity)
        {
            if (entity is Product product)
            {
                var sql = @"
                    DELETE FROM dbo.Products 
                    WHERE ProductId = @ProductId
                      AND RowVersion = @RowVersion";
                
                var rowsAffected = Connection.Execute(sql, product, transaction: Transaction);
                if (rowsAffected == 0)
                {
                    throw new DBConcurrencyException("并发冲突");
                }
            }
        }

        #endregion
    }
}
