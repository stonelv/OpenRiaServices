using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using OpenRiaServices.Server;

namespace TestDomainServices
{
    public class ConcurrencyProduct
    {
        [Key]
        public int ProductId { get; set; }

        [Required]
        [StringLength(50)]
        public string Name { get; set; }

        public decimal Price { get; set; }

        [Timestamp]
        [ConcurrencyCheck]
        public byte[] RowVersion { get; set; }
    }

    public class ConcurrencyProductWithMultipleChecks
    {
        [Key]
        public int ProductId { get; set; }

        [ConcurrencyCheck]
        public string Name { get; set; }

        [ConcurrencyCheck]
        public decimal Price { get; set; }
    }

    public class ConcurrencyTestDomainService : DomainService
    {
        private static readonly object _lock = new object();
        private static Dictionary<int, ConcurrencyProduct> _productStore = new Dictionary<int, ConcurrencyProduct>();
        private static int _nextId = 1;

        public static void ResetStore()
        {
            lock (_lock)
            {
                _productStore.Clear();
                _nextId = 1;
            }
        }

        public static void AddProductToStore(ConcurrencyProduct product)
        {
            lock (_lock)
            {
                _productStore[product.ProductId] = product;
                if (product.ProductId >= _nextId)
                {
                    _nextId = product.ProductId + 1;
                }
            }
        }

        public static ConcurrencyProduct GetProductFromStore(int productId)
        {
            lock (_lock)
            {
                if (_productStore.TryGetValue(productId, out var product))
                {
                    return new ConcurrencyProduct
                    {
                        ProductId = product.ProductId,
                        Name = product.Name,
                        Price = product.Price,
                        RowVersion = (byte[])product.RowVersion?.Clone()
                    };
                }
                return null;
            }
        }

        public IEnumerable<ConcurrencyProduct> GetConcurrencyProducts()
        {
            lock (_lock)
            {
                return _productStore.Values.Select(p => new ConcurrencyProduct
                {
                    ProductId = p.ProductId,
                    Name = p.Name,
                    Price = p.Price,
                    RowVersion = (byte[])p.RowVersion?.Clone()
                }).ToList();
            }
        }

        public ConcurrencyProduct GetConcurrencyProductById(int productId)
        {
            return GetProductFromStore(productId);
        }

        public void InsertConcurrencyProduct(ConcurrencyProduct product)
        {
            if (product == null)
                throw new ArgumentNullException(nameof(product));

            lock (_lock)
            {
                if (product.ProductId == 0)
                {
                    product.ProductId = _nextId++;
                }

                if (product.RowVersion == null || product.RowVersion.Length == 0)
                {
                    product.RowVersion = new byte[] { 1, 0, 0, 0, 0, 0, 0, 0 };
                }

                _productStore[product.ProductId] = new ConcurrencyProduct
                {
                    ProductId = product.ProductId,
                    Name = product.Name,
                    Price = product.Price,
                    RowVersion = (byte[])product.RowVersion.Clone()
                };
            }
        }

        public void UpdateConcurrencyProduct(ConcurrencyProduct entity)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            lock (_lock)
            {
                ConcurrencyProduct storeEntity;
                if (!_productStore.TryGetValue(entity.ProductId, out storeEntity))
                {
                    var deleteEntry = this.ChangeSet.ChangeSetEntries
                        .FirstOrDefault(e => object.ReferenceEquals(e.Entity, entity));

                    if (deleteEntry != null)
                    {
                        deleteEntry.IsDeleteConflict = true;
                    }
                    return;
                }

                if (!RowVersionsEqual(entity.RowVersion, storeEntity.RowVersion))
                {
                    var entry = this.ChangeSet.ChangeSetEntries
                        .FirstOrDefault(e => object.ReferenceEquals(e.Entity, entity));

                    if (entry != null)
                    {
                        var conflictStoreEntity = new ConcurrencyProduct
                        {
                            ProductId = storeEntity.ProductId,
                            Name = storeEntity.Name,
                            Price = storeEntity.Price,
                            RowVersion = (byte[])storeEntity.RowVersion.Clone()
                        };

                        entry.StoreEntity = conflictStoreEntity;
                        entry.ConflictMembers = new List<string> { "Name", "Price", "RowVersion" };
                    }
                    return;
                }

                storeEntity.Name = entity.Name;
                storeEntity.Price = entity.Price;
                IncrementRowVersion(storeEntity.RowVersion);

                entity.RowVersion = (byte[])storeEntity.RowVersion.Clone();
            }
        }

        public void DeleteConcurrencyProduct(ConcurrencyProduct entity)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            lock (_lock)
            {
                ConcurrencyProduct storeEntity;
                if (!_productStore.TryGetValue(entity.ProductId, out storeEntity))
                {
                    var entry = this.ChangeSet.ChangeSetEntries
                        .FirstOrDefault(e => object.ReferenceEquals(e.Entity, entity));

                    if (entry != null)
                    {
                        entry.IsDeleteConflict = true;
                    }
                    return;
                }

                if (!RowVersionsEqual(entity.RowVersion, storeEntity.RowVersion))
                {
                    var entry = this.ChangeSet.ChangeSetEntries
                        .FirstOrDefault(e => object.ReferenceEquals(e.Entity, entity));

                    if (entry != null)
                    {
                        var conflictStoreEntity = new ConcurrencyProduct
                        {
                            ProductId = storeEntity.ProductId,
                            Name = storeEntity.Name,
                            Price = storeEntity.Price,
                            RowVersion = (byte[])storeEntity.RowVersion.Clone()
                        };

                        entry.StoreEntity = conflictStoreEntity;
                        entry.ConflictMembers = new List<string> { "RowVersion" };
                    }
                    return;
                }

                _productStore.Remove(entity.ProductId);
            }
        }

        public void UpdateConcurrencyProductWithValidationError(ConcurrencyProduct entity)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            if (string.IsNullOrEmpty(entity.Name))
            {
                var entry = this.ChangeSet.ChangeSetEntries
                    .FirstOrDefault(e => object.ReferenceEquals(e.Entity, entity));

                if (entry != null)
                {
                    entry.ValidationErrors = new List<ValidationResultInfo>
                    {
                        new ValidationResultInfo("Name 是必填项", new[] { "Name" }),
                        new ValidationResultInfo("Name 不能超过 50 个字符", new[] { "Name" })
                    };
                }
                return;
            }

            if (entity.Price < 0)
            {
                var entry = this.ChangeSet.ChangeSetEntries
                    .FirstOrDefault(e => object.ReferenceEquals(e.Entity, entity));

                if (entry != null)
                {
                    entry.ValidationErrors = new List<ValidationResultInfo>
                    {
                        new ValidationResultInfo("Price 必须大于或等于 0", new[] { "Price" })
                    };
                }
                return;
            }

            UpdateConcurrencyProduct(entity);
        }

        public void SimulateConcurrencyConflict(int productId, string updatedName, decimal updatedPrice)
        {
            lock (_lock)
            {
                ConcurrencyProduct storeEntity;
                if (_productStore.TryGetValue(productId, out storeEntity))
                {
                    storeEntity.Name = updatedName;
                    storeEntity.Price = updatedPrice;
                    IncrementRowVersion(storeEntity.RowVersion);
                }
            }
        }

        private static bool RowVersionsEqual(byte[] v1, byte[] v2)
        {
            if (v1 == null && v2 == null)
                return true;
            if (v1 == null || v2 == null)
                return false;
            if (v1.Length != v2.Length)
                return false;

            for (int i = 0; i < v1.Length; i++)
            {
                if (v1[i] != v2[i])
                    return false;
            }
            return true;
        }

        private static void IncrementRowVersion(byte[] rowVersion)
        {
            if (rowVersion == null || rowVersion.Length == 0)
                return;

            for (int i = rowVersion.Length - 1; i >= 0; i--)
            {
                if (rowVersion[i] < 255)
                {
                    rowVersion[i]++;
                    break;
                }
                rowVersion[i] = 0;
            }
        }
    }
}
