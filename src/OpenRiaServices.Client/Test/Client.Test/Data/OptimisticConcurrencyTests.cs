using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenRiaServices.Client;

namespace OpenRiaServices.Client.Test
{
    [TestClass]
    public class OptimisticConcurrencyTests
    {
        #region Test Entities

        public class ProductWithRowVersion : Entity
        {
            private int _productId;
            private string _name;
            private decimal _price;
            private byte[] _rowVersion;

            [Key]
            public int ProductId
            {
                get => _productId;
                set
                {
                    if (_productId != value)
                    {
                        _productId = value;
                        RaisePropertyChanged(nameof(ProductId));
                    }
                }
            }

            [Required]
            [StringLength(50)]
            public string Name
            {
                get => _name;
                set
                {
                    if (_name != value)
                    {
                        ValidateProperty(value);
                        _name = value;
                        RaisePropertyChanged(nameof(Name));
                    }
                }
            }

            public decimal Price
            {
                get => _price;
                set
                {
                    if (_price != value)
                    {
                        _price = value;
                        RaisePropertyChanged(nameof(Price));
                    }
                }
            }

            [Timestamp]
            [ConcurrencyCheck]
            public byte[] RowVersion
            {
                get => _rowVersion;
                set
                {
                    if (_rowVersion != value)
                    {
                        _rowVersion = value;
                        RaisePropertyChanged(nameof(RowVersion));
                    }
                }
            }
        }

        public class TestEntityContainer : EntityContainer
        {
            public TestEntityContainer()
            {
                CreateEntitySet<ProductWithRowVersion>(EntitySetOperations.All);
            }

            public EntitySet<ProductWithRowVersion> Products => GetEntitySet<ProductWithRowVersion>();
        }

        #endregion

        #region OperationErrorStatus Distinction Tests

        [TestMethod]
        [Description("验证 OperationErrorStatus 正确区分 Conflicts 和 ValidationFailed")]
        public void OperationErrorStatus_Distinguishes_Conflicts_And_ValidationFailed()
        {
            var conflictStatus = OperationErrorStatus.Conflicts;
            var validationStatus = OperationErrorStatus.ValidationFailed;

            Assert.AreNotEqual(conflictStatus, validationStatus, "Conflicts 和 ValidationFailed 应该是不同的枚举值");
            Assert.AreEqual(5, (int)validationStatus, "ValidationFailed 的整数值应该是 5");
            Assert.AreEqual(6, (int)conflictStatus, "Conflicts 的整数值应该是 6");
        }

        [TestMethod]
        [Description("验证 SubmitOperationException 正确携带 Conflicts 状态")]
        public void SubmitOperationException_With_Conflicts_Status()
        {
            var entities = new List<Entity>();
            var changeSet = new EntityChangeSet(
                new ReadOnlyCollection<Entity>(entities),
                new ReadOnlyCollection<Entity>(entities),
                new ReadOnlyCollection<Entity>(entities));

            var exception = new SubmitOperationException(
                changeSet,
                "检测到并发冲突",
                OperationErrorStatus.Conflicts);

            Assert.AreEqual(OperationErrorStatus.Conflicts, exception.Status);
            Assert.AreEqual("检测到并发冲突", exception.Message);
        }

        [TestMethod]
        [Description("验证 SubmitOperationException 正确携带 ValidationFailed 状态")]
        public void SubmitOperationException_With_ValidationFailed_Status()
        {
            var entities = new List<Entity>();
            var changeSet = new EntityChangeSet(
                new ReadOnlyCollection<Entity>(entities),
                new ReadOnlyCollection<Entity>(entities),
                new ReadOnlyCollection<Entity>(entities));

            var exception = new SubmitOperationException(
                changeSet,
                "验证失败",
                OperationErrorStatus.ValidationFailed);

            Assert.AreEqual(OperationErrorStatus.ValidationFailed, exception.Status);
            Assert.AreEqual("验证失败", exception.Message);
        }

        #endregion

        #region EntityConflict Tests

        [TestMethod]
        [Description("验证 EntityConflict 正确存储冲突信息")]
        public void EntityConflict_Stores_Conflict_Information()
        {
            var current = new ProductWithRowVersion
            {
                ProductId = 1,
                Name = "Client Updated Name",
                Price = 12.0m,
                RowVersion = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }
            };

            var store = new ProductWithRowVersion
            {
                ProductId = 1,
                Name = "Server Updated Name",
                Price = 15.0m,
                RowVersion = new byte[] { 9, 10, 11, 12, 13, 14, 15, 16 }
            };

            var conflict = new EntityConflict(current, store, new[] { "Name", "Price", "RowVersion" }, false);

            Assert.AreSame(current, conflict.CurrentEntity);
            Assert.AreSame(store, conflict.StoreEntity);
            CollectionAssert.AreEqual(new[] { "Name", "Price", "RowVersion" }, conflict.PropertyNames.ToArray());
            Assert.IsFalse(conflict.IsDeleted);
        }

        [TestMethod]
        [Description("验证 EntityConflict 正确表示删除冲突")]
        public void EntityConflict_For_Delete_Conflict()
        {
            var current = new ProductWithRowVersion
            {
                ProductId = 1,
                Name = "Product",
                RowVersion = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }
            };

            var conflict = new EntityConflict(current, null, null, true);

            Assert.IsTrue(conflict.IsDeleted);
            Assert.AreSame(current, conflict.CurrentEntity);
            Assert.IsNull(conflict.StoreEntity);
        }

        [TestMethod]
        [Description("验证 EntityConflict.Resolve 正确更新原始值")]
        public void EntityConflict_Resolve_Updates_Original_Values()
        {
            var container = new TestEntityContainer();
            var set = container.Products;

            var original = new ProductWithRowVersion
            {
                ProductId = 1,
                Name = "Original Name",
                Price = 10.0m,
                RowVersion = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }
            };
            set.LoadEntity(original);

            original.Name = "Client Modified Name";
            original.Price = 12.0m;

            var store = new ProductWithRowVersion
            {
                ProductId = 1,
                Name = "Server Updated Name",
                Price = 15.0m,
                RowVersion = new byte[] { 9, 10, 11, 12, 13, 14, 15, 16 }
            };

            var conflict = new EntityConflict(original, store, new[] { "Name", "Price", "RowVersion" }, false);
            original.EntityConflict = conflict;

            Assert.IsNotNull(original.EntityConflict);
            Assert.AreEqual(EntityState.Modified, original.EntityState);

            conflict.Resolve();

            Assert.IsNull(original.EntityConflict, "Resolve 后应该清除冲突");

            var originalValues = original.GetOriginal();
            Assert.AreEqual("Server Updated Name", originalValues.ExtractState()["Name"]);
            Assert.AreEqual(15.0m, originalValues.ExtractState()["Price"]);
            CollectionAssert.AreEqual(new byte[] { 9, 10, 11, 12, 13, 14, 15, 16 }, (byte[])originalValues.ExtractState()["RowVersion"]);

            Assert.AreEqual("Client Modified Name", original.Name, "当前值不应该被修改");
            Assert.AreEqual(12.0m, original.Price, "当前价格不应该被修改");
        }

        #endregion

        #region Entity Conflict vs Validation Error Distinction

        [TestMethod]
        [Description("验证 Entity 正确区分并发冲突和验证错误")]
        public void Entity_Distinguishes_Conflict_And_ValidationErrors()
        {
            var container = new TestEntityContainer();
            var set = container.Products;

            var entity = new ProductWithRowVersion
            {
                ProductId = 1,
                Name = "Test",
                Price = 10.0m
            };
            set.LoadEntity(entity);

            Assert.IsNull(entity.EntityConflict);
            Assert.IsFalse(entity.HasValidationErrors);

            var validationResults = new List<ValidationResult>
            {
                new ValidationResult("Price 必须大于 0", new[] { "Price" })
            };
            entity.ValidationErrors.AddRange(validationResults);

            Assert.IsTrue(entity.HasValidationErrors, "添加验证错误后 HasValidationErrors 应为 true");
            Assert.IsNull(entity.EntityConflict, "EntityConflict 应该为 null");

            var store = new ProductWithRowVersion
            {
                ProductId = 1,
                Name = "Updated",
                Price = 15.0m
            };
            var conflict = new EntityConflict(entity, store, new[] { "Price" }, false);
            entity.EntityConflict = conflict;

            Assert.IsNotNull(entity.EntityConflict, "EntityConflict 不应该为 null");
            Assert.IsTrue(entity.HasValidationErrors, "HasValidationErrors 仍应为 true");
        }

        [TestMethod]
        [Description("验证 SubmitOperationException.EntitiesInError 正确包含冲突和验证错误的实体")]
        public void SubmitOperationException_EntitiesInError_Includes_Conflicts_And_ValidationErrors()
        {
            var container = new TestEntityContainer();
            var set = container.Products;

            var entityWithConflict = new ProductWithRowVersion
            {
                ProductId = 1,
                Name = "Conflict Product",
                Price = 10.0m
            };
            set.LoadEntity(entityWithConflict);

            var entityWithValidationError = new ProductWithRowVersion
            {
                ProductId = 2,
                Name = "Validation Product",
                Price = 20.0m
            };
            set.LoadEntity(entityWithValidationError);

            var store = new ProductWithRowVersion
            {
                ProductId = 1,
                Name = "Updated",
                Price = 15.0m
            };
            var conflict = new EntityConflict(entityWithConflict, store, new[] { "Price" }, false);
            entityWithConflict.EntityConflict = conflict;

            var validationResults = new List<ValidationResult>
            {
                new ValidationResult("Name 无效", new[] { "Name" })
            };
            entityWithValidationError.ValidationErrors.AddRange(validationResults);

            var entities = new List<Entity> { entityWithConflict, entityWithValidationError };
            var changeSet = new EntityChangeSet(
                new ReadOnlyCollection<Entity>(entities),
                new ReadOnlyCollection<Entity>(entities),
                new ReadOnlyCollection<Entity>(entities));

            var exception = new SubmitOperationException(
                changeSet,
                "提交操作失败",
                OperationErrorStatus.Conflicts);

            Assert.AreEqual(2, exception.EntitiesInError.Count);
            Assert.IsTrue(exception.EntitiesInError.Contains(entityWithConflict));
            Assert.IsTrue(exception.EntitiesInError.Contains(entityWithValidationError));
        }

        #endregion

        #region Mock Submit Scenario Tests

        [TestMethod]
        [Description("测试正常更新流程 - 没有冲突或验证错误")]
        public async Task Submit_Without_Conflicts_Or_Errors()
        {
            var mockDomainClient = new OptimisticConcurrencyMockDomainClient();
            var context = new TestDomainContext(mockDomainClient);

            var product = new ProductWithRowVersion
            {
                ProductId = 1,
                Name = "Test Product",
                Price = 10.0m,
                RowVersion = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }
            };
            context.Products.Add(product);

            var submitOp = context.SubmitChanges();
            await submitOp;

            Assert.IsNull(submitOp.Error, "正常提交不应该有错误");
            Assert.IsTrue(submitOp.IsComplete);
            Assert.IsFalse(submitOp.HasError);
            Assert.AreEqual(1, context.Products.Count);
        }

        [TestMethod]
        [Description("测试并发冲突更新流程")]
        public async Task Submit_With_Concurrency_Conflict()
        {
            var mockDomainClient = new OptimisticConcurrencyMockDomainClient();
            var context = new TestDomainContext(mockDomainClient);

            var product = new ProductWithRowVersion
            {
                ProductId = 1,
                Name = "Old Name",
                Price = 10.0m,
                RowVersion = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }
            };
            context.Products.Load(product);

            product.Name = "Client Updated Name";
            product.Price = 12.0m;

            mockDomainClient.SimulateConcurrencyConflict = true;

            var submitOp = context.SubmitChanges();
            await submitOp;

            Assert.IsNotNull(submitOp.Error, "并发冲突应该导致错误");
            Assert.IsTrue(submitOp.HasError);

            var ex = submitOp.Error as SubmitOperationException;
            Assert.IsNotNull(ex);
            Assert.AreEqual(OperationErrorStatus.Conflicts, ex.Status, "状态应该是 Conflicts");

            var entitiesInError = ex.EntitiesInError.ToList();
            Assert.AreEqual(1, entitiesInError.Count);
            Assert.AreSame(product, entitiesInError[0]);
            Assert.IsNotNull(product.EntityConflict, "实体应该有冲突信息");
            Assert.IsFalse(product.EntityConflict.IsDeleted);

            var conflict = product.EntityConflict;
            var storeEntity = conflict.StoreEntity as ProductWithRowVersion;
            Assert.IsNotNull(storeEntity);
            Assert.AreEqual("Server Updated Name", storeEntity.Name);
            Assert.AreEqual(15.0m, storeEntity.Price);
            CollectionAssert.AreEqual(new byte[] { 9, 10, 11, 12, 13, 14, 15, 16 }, storeEntity.RowVersion);
        }

        [TestMethod]
        [Description("测试验证失败更新流程")]
        public async Task Submit_With_Validation_Failure()
        {
            var mockDomainClient = new OptimisticConcurrencyMockDomainClient();
            var context = new TestDomainContext(mockDomainClient);

            var product = new ProductWithRowVersion
            {
                ProductId = 1,
                Name = null,
                Price = -1.0m
            };
            context.Products.Add(product);

            mockDomainClient.SimulateValidationError = true;

            var submitOp = context.SubmitChanges();
            await submitOp;

            Assert.IsNotNull(submitOp.Error, "验证失败应该导致错误");
            Assert.IsTrue(submitOp.HasError);

            var ex = submitOp.Error as SubmitOperationException;
            Assert.IsNotNull(ex);
            Assert.AreEqual(OperationErrorStatus.ValidationFailed, ex.Status, "状态应该是 ValidationFailed");

            var entitiesInError = ex.EntitiesInError.ToList();
            Assert.AreEqual(1, entitiesInError.Count);
            Assert.AreSame(product, entitiesInError[0]);
            Assert.IsTrue(product.HasValidationErrors, "实体应该有验证错误");
            Assert.IsNull(product.EntityConflict, "不应该有并发冲突");

            var validationErrors = product.ValidationErrors.ToList();
            Assert.IsTrue(validationErrors.Count >= 1);
            Assert.IsTrue(validationErrors.Any(v => v.MemberNames.Contains("Name")));
        }

        [TestMethod]
        [Description("测试冲突后的重试流程")]
        public async Task Submit_Conflict_Then_Retry()
        {
            var mockDomainClient = new OptimisticConcurrencyMockDomainClient();
            var context = new TestDomainContext(mockDomainClient);

            var product = new ProductWithRowVersion
            {
                ProductId = 1,
                Name = "Old Name",
                Price = 10.0m,
                RowVersion = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }
            };
            context.Products.Load(product);

            product.Name = "Client Updated Name";

            mockDomainClient.SimulateConcurrencyConflict = true;

            var submitOp = context.SubmitChanges();
            await submitOp;

            Assert.IsNotNull(submitOp.Error);
            var ex = submitOp.Error as SubmitOperationException;
            Assert.AreEqual(OperationErrorStatus.Conflicts, ex.Status);

            var conflict = product.EntityConflict;
            Assert.IsNotNull(conflict);

            conflict.Resolve();
            Assert.IsNull(product.EntityConflict);

            mockDomainClient.SimulateConcurrencyConflict = false;

            product.Name = "Final Updated Name";

            var retrySubmitOp = context.SubmitChanges();
            await retrySubmitOp;

            Assert.IsNull(retrySubmitOp.Error, "重试应该成功");
            Assert.IsTrue(retrySubmitOp.IsComplete);
            Assert.IsFalse(retrySubmitOp.HasError);
        }

        [TestMethod]
        [Description("测试删除冲突流程")]
        public async Task Submit_With_Delete_Conflict()
        {
            var mockDomainClient = new OptimisticConcurrencyMockDomainClient();
            var context = new TestDomainContext(mockDomainClient);

            var product = new ProductWithRowVersion
            {
                ProductId = 1,
                Name = "Test Product",
                Price = 10.0m,
                RowVersion = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }
            };
            context.Products.Load(product);

            context.Products.Remove(product);

            mockDomainClient.SimulateDeleteConflict = true;

            var submitOp = context.SubmitChanges();
            await submitOp;

            Assert.IsNotNull(submitOp.Error, "删除冲突应该导致错误");
            Assert.IsTrue(submitOp.HasError);

            var ex = submitOp.Error as SubmitOperationException;
            Assert.IsNotNull(ex);
            Assert.AreEqual(OperationErrorStatus.Conflicts, ex.Status);

            var entitiesInError = ex.EntitiesInError.ToList();
            Assert.AreEqual(1, entitiesInError.Count);
            Assert.AreSame(product, entitiesInError[0]);
            Assert.IsNotNull(product.EntityConflict);
            Assert.IsTrue(product.EntityConflict.IsDeleted, "应该是删除冲突");
            Assert.IsNull(product.EntityConflict.StoreEntity);

            ExceptionHelper.ExpectInvalidOperationException(
                () => product.EntityConflict.Resolve(),
                "Resolve cannot be called for delete conflicts.");
        }

        #endregion
    }

    #region Helper Classes

    public class TestDomainContext : DomainContext
    {
        public TestDomainContext(DomainClient domainClient)
            : base(domainClient)
        {
        }

        public EntitySet<OptimisticConcurrencyTests.ProductWithRowVersion> Products { get; private set; }

        protected override EntityContainer CreateEntityContainer()
        {
            var container = new OptimisticConcurrencyTests.TestEntityContainer();
            Products = container.Products;
            return container;
        }
    }

    public class OptimisticConcurrencyMockDomainClient : DomainClient
    {
        public bool SimulateConcurrencyConflict { get; set; }
        public bool SimulateValidationError { get; set; }
        public bool SimulateDeleteConflict { get; set; }

        public override bool SupportsCancellation => true;

        protected override Task<QueryCompletedResult> QueryAsyncCore(EntityQuery query, System.Threading.CancellationToken cancellationToken)
        {
            return Task.FromResult(new QueryCompletedResult(Enumerable.Empty<Entity>(), Enumerable.Empty<Entity>(), 0, Enumerable.Empty<ValidationResult>()));
        }

        protected override Task<SubmitCompletedResult> SubmitAsyncCore(EntityChangeSet changeSet, System.Threading.CancellationToken cancellationToken)
        {
            var entries = changeSet.GetChangeSetEntries().ToList();

            if (SimulateValidationError)
            {
                foreach (var entry in entries)
                {
                    var validationErrors = new List<ValidationResultInfo>
                    {
                        new ValidationResultInfo("Name 不能为空", new[] { "Name" }),
                        new ValidationResultInfo("Price 必须大于 0", new[] { "Price" })
                    };
                    entry.ValidationErrors = validationErrors;
                }

                var result = new SubmitCompletedResult(changeSet, entries);
                return Task.FromResult(result);
            }

            if (SimulateDeleteConflict)
            {
                foreach (var entry in entries)
                {
                    entry.IsDeleteConflict = true;
                }

                var result = new SubmitCompletedResult(changeSet, entries);
                return Task.FromResult(result);
            }

            if (SimulateConcurrencyConflict)
            {
                foreach (var entry in entries)
                {
                    var currentEntity = entry.Entity as OptimisticConcurrencyTests.ProductWithRowVersion;
                    if (currentEntity != null)
                    {
                        var storeEntity = new OptimisticConcurrencyTests.ProductWithRowVersion
                        {
                            ProductId = currentEntity.ProductId,
                            Name = "Server Updated Name",
                            Price = 15.0m,
                            RowVersion = new byte[] { 9, 10, 11, 12, 13, 14, 15, 16 }
                        };

                        entry.StoreEntity = storeEntity;
                        entry.ConflictMembers = new List<string> { "Name", "Price", "RowVersion" };
                    }
                }

                var result = new SubmitCompletedResult(changeSet, entries);
                return Task.FromResult(result);
            }

            foreach (var entry in entries)
            {
                var entity = entry.Entity as OptimisticConcurrencyTests.ProductWithRowVersion;
                if (entity != null)
                {
                    var currentVersion = entity.RowVersion ?? new byte[8];
                    var newVersion = new byte[8];
                    currentVersion.CopyTo(newVersion, 0);
                    if (newVersion.Length > 0)
                    {
                        newVersion[newVersion.Length - 1]++;
                    }
                    entity.RowVersion = newVersion;
                }
            }

            var successResult = new SubmitCompletedResult(changeSet, entries);
            return Task.FromResult(successResult);
        }

        protected override Task<InvokeCompletedResult> InvokeAsyncCore(InvokeArgs invokeArgs, System.Threading.CancellationToken cancellationToken)
        {
            return Task.FromResult(new InvokeCompletedResult(null));
        }
    }

    #endregion
}
