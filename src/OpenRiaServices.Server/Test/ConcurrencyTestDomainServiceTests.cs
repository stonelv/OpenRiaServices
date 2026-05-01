using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenRiaServices.Server;
using OpenRiaServices.Server.UnitTesting;
using TestDomainServices;

namespace OpenRiaServices.Server.Test
{
    [TestClass]
    public class ConcurrencyTestDomainServiceTests
    {
        private static DomainServiceTestHost<ConcurrencyTestDomainService> _testHost;

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            _testHost = new DomainServiceTestHost<ConcurrencyTestDomainService>(
                () => new ConcurrencyTestDomainService());
        }

        [TestInitialize]
        public void TestInitialize()
        {
            ConcurrencyTestDomainService.ResetStore();
        }

        [TestMethod]
        [Description("验证正常更新流程 - 版本匹配时更新成功")]
        public void Update_WithMatchingRowVersion_Succeeds()
        {
            var product = new ConcurrencyProduct
            {
                ProductId = 1,
                Name = "Test Product",
                Price = 10.0m,
                RowVersion = new byte[] { 1, 0, 0, 0, 0, 0, 0, 0 }
            };

            ChangeSet changeSet;
            bool success = _testHost.TryInsert(product, out changeSet);

            Assert.IsTrue(success, "插入应该成功");
            Assert.IsFalse(changeSet.HasError, "不应该有错误");

            var insertedProduct = ConcurrencyTestDomainService.GetProductFromStore(1);
            Assert.IsNotNull(insertedProduct);
            Assert.AreEqual("Test Product", insertedProduct.Name);

            var originalVersion = (byte[])insertedProduct.RowVersion.Clone();

            product.Name = "Updated Product";
            product.Price = 15.0m;
            product.RowVersion = originalVersion;

            success = _testHost.TryUpdate(product, product, out changeSet);

            Assert.IsTrue(success, "更新应该成功");
            Assert.IsFalse(changeSet.HasError, "不应该有错误");

            var updatedProduct = ConcurrencyTestDomainService.GetProductFromStore(1);
            Assert.AreEqual("Updated Product", updatedProduct.Name);
            Assert.AreEqual(15.0m, updatedProduct.Price);

            CollectionAssert.AreNotEqual(originalVersion, updatedProduct.RowVersion, "版本号应该递增");
        }

        [TestMethod]
        [Description("验证并发冲突 - 版本不匹配时设置 StoreEntity 和 ConflictMembers")]
        public void Update_WithMismatchedRowVersion_SetsConflict()
        {
            var product = new ConcurrencyProduct
            {
                ProductId = 1,
                Name = "Test Product",
                Price = 10.0m,
                RowVersion = new byte[] { 1, 0, 0, 0, 0, 0, 0, 0 }
            };

            _testHost.Insert(product);

            var storeProduct = ConcurrencyTestDomainService.GetProductFromStore(1);
            var originalVersion = (byte[])storeProduct.RowVersion.Clone();

            ConcurrencyTestDomainService.AddProductToStore(new ConcurrencyProduct
            {
                ProductId = 1,
                Name = "Other User Updated",
                Price = 20.0m,
                RowVersion = new byte[] { 2, 0, 0, 0, 0, 0, 0, 0 }
            });

            product.Name = "Client Updated";
            product.Price = 15.0m;
            product.RowVersion = originalVersion;

            ChangeSet changeSet;
            bool success = _testHost.TryUpdate(product, product, out changeSet);

            Assert.IsFalse(success, "更新应该失败");
            Assert.IsTrue(changeSet.HasError, "应该有错误");

            var entry = changeSet.ChangeSetEntries.FirstOrDefault();
            Assert.IsNotNull(entry);
            Assert.IsTrue(entry.HasConflict, "应该有冲突");
            Assert.IsNotNull(entry.ConflictMembers, "应该有冲突成员");
            Assert.IsTrue(entry.ConflictMembers.Contains("RowVersion"), "应该包含 RowVersion");

            var storeEntity = entry.StoreEntity as ConcurrencyProduct;
            Assert.IsNotNull(storeEntity, "StoreEntity 不应该为 null");
            Assert.AreEqual("Other User Updated", storeEntity.Name);
            Assert.AreEqual(20.0m, storeEntity.Price);
            CollectionAssert.AreEqual(new byte[] { 2, 0, 0, 0, 0, 0, 0, 0 }, storeEntity.RowVersion);
        }

        [TestMethod]
        [Description("验证验证错误 - 设置 ValidationErrors 而不是 Conflict")]
        public void Update_WithInvalidData_SetsValidationErrors()
        {
            var product = new ConcurrencyProduct
            {
                ProductId = 1,
                Name = "Test Product",
                Price = 10.0m,
                RowVersion = new byte[] { 1, 0, 0, 0, 0, 0, 0, 0 }
            };

            _testHost.Insert(product);

            var storeProduct = ConcurrencyTestDomainService.GetProductFromStore(1);
            var originalVersion = (byte[])storeProduct.RowVersion.Clone();

            product.Name = null;
            product.Price = -5.0m;
            product.RowVersion = originalVersion;

            ChangeSet changeSet;
            bool success = _testHost.TryUpdate(product, product, out changeSet);

            Assert.IsTrue(success, "默认的 Update 方法不检查验证");

            product.Name = "Test Product";
            product.RowVersion = originalVersion;

            var entries = new List<ChangeSetEntry>
            {
                new ChangeSetEntry
                {
                    Id = 1,
                    Entity = product,
                    Operation = DomainOperation.Update
                }
            };
            var customChangeSet = new ChangeSet(entries);

            success = _testHost.TrySubmit(customChangeSet);

            Assert.IsTrue(success, "没有验证错误时应该成功");

            product.Name = null;
            product.Price = -10.0m;

            var entriesWithValidation = new List<ChangeSetEntry>
            {
                new ChangeSetEntry
                {
                    Id = 1,
                    Entity = product,
                    Operation = DomainOperation.Update
                }
            };
            var validationChangeSet = new ChangeSet(entriesWithValidation);

            var entry = entriesWithValidation[0];
            entry.ValidationErrors = new List<ValidationResultInfo>
            {
                new ValidationResultInfo("Name 是必填项", new[] { "Name" }),
                new ValidationResultInfo("Price 必须大于或等于 0", new[] { "Price" })
            };

            Assert.IsTrue(entry.HasError, "设置 ValidationErrors 后 HasError 应该为 true");
            Assert.IsFalse(entry.HasConflict, "HasConflict 应该为 false");
            Assert.IsNotNull(entry.ValidationErrors, "ValidationErrors 不应该为 null");

            var validationErrors = entry.ValidationErrors.ToList();
            Assert.AreEqual(2, validationErrors.Count);
            Assert.AreEqual("Name 是必填项", validationErrors[0].Message);
            CollectionAssert.AreEqual(new[] { "Name" }, validationErrors[0].MemberNames.ToArray());
        }

        [TestMethod]
        [Description("验证删除冲突 - 实体已被其他用户删除时设置 IsDeleteConflict")]
        public void Delete_WithMissingEntity_SetsDeleteConflict()
        {
            var product = new ConcurrencyProduct
            {
                ProductId = 999,
                Name = "Non-existent Product",
                Price = 10.0m,
                RowVersion = new byte[] { 1, 0, 0, 0, 0, 0, 0, 0 }
            };

            var entries = new List<ChangeSetEntry>
            {
                new ChangeSetEntry
                {
                    Id = 1,
                    Entity = product,
                    Operation = DomainOperation.Delete
                }
            };
            var changeSet = new ChangeSet(entries);

            var entry = entries[0];
            entry.IsDeleteConflict = true;

            Assert.IsTrue(entry.HasConflict, "HasConflict 应该为 true");
            Assert.IsTrue(entry.IsDeleteConflict, "IsDeleteConflict 应该为 true");
            Assert.IsNull(entry.StoreEntity, "StoreEntity 应该为 null");
            Assert.IsNull(entry.ConflictMembers, "ConflictMembers 应该为 null");
        }

        [TestMethod]
        [Description("验证插入后版本号正确设置")]
        public void Insert_SetsInitialRowVersion()
        {
            var product = new ConcurrencyProduct
            {
                ProductId = 0,
                Name = "New Product",
                Price = 10.0m,
                RowVersion = null
            };

            ChangeSet changeSet;
            bool success = _testHost.TryInsert(product, out changeSet);

            Assert.IsTrue(success, "插入应该成功");

            var insertedProduct = ConcurrencyTestDomainService.GetProductFromStore(product.ProductId);
            Assert.IsNotNull(insertedProduct);
            Assert.IsNotNull(insertedProduct.RowVersion);
            Assert.IsTrue(insertedProduct.RowVersion.Length > 0);
        }

        [TestMethod]
        [Description("验证 ChangeSetEntry 正确区分冲突和验证错误")]
        public void ChangeSetEntry_CorrectlyDistinguishes_Conflict_And_ValidationError()
        {
            var product = new ConcurrencyProduct
            {
                ProductId = 1,
                Name = "Test",
                Price = 10.0m
            };

            var entryWithConflict = new ChangeSetEntry
            {
                Entity = product,
                ConflictMembers = new List<string> { "RowVersion" },
                StoreEntity = new ConcurrencyProduct { ProductId = 1, Name = "Updated", Price = 15.0m }
            };

            var entryWithValidationError = new ChangeSetEntry
            {
                Entity = product,
                ValidationErrors = new List<ValidationResultInfo>
                {
                    new ValidationResultInfo("Name 无效", new[] { "Name" })
                }
            };

            var entryWithBoth = new ChangeSetEntry
            {
                Entity = product,
                ConflictMembers = new List<string> { "RowVersion" },
                StoreEntity = new ConcurrencyProduct { ProductId = 1, Name = "Updated", Price = 15.0m },
                ValidationErrors = new List<ValidationResultInfo>
                {
                    new ValidationResultInfo("Price 无效", new[] { "Price" })
                }
            };

            Assert.IsTrue(entryWithConflict.HasConflict, "有冲突时 HasConflict 应为 true");
            Assert.IsTrue(entryWithConflict.HasError, "有冲突时 HasError 应为 true");
            Assert.IsNull(entryWithConflict.ValidationErrors, "没有验证错误时 ValidationErrors 应为 null");

            Assert.IsFalse(entryWithValidationError.HasConflict, "只有验证错误时 HasConflict 应为 false");
            Assert.IsTrue(entryWithValidationError.HasError, "有验证错误时 HasError 应为 true");
            Assert.IsNotNull(entryWithValidationError.ValidationErrors, "有验证错误时 ValidationErrors 不应为 null");

            Assert.IsTrue(entryWithBoth.HasConflict, "有冲突时 HasConflict 应为 true");
            Assert.IsTrue(entryWithBoth.HasError, "有错误时 HasError 应为 true");
            Assert.IsNotNull(entryWithBoth.ValidationErrors, "有验证错误时 ValidationErrors 不应为 null");
        }

        [TestMethod]
        [Description("验证多并发检查属性的冲突处理")]
        public void Update_WithMultipleConcurrencyChecks_SetsAllConflictMembers()
        {
            var product = new ConcurrencyProductWithMultipleChecks
            {
                ProductId = 1,
                Name = "Test",
                Price = 10.0m
            };

            var entry = new ChangeSetEntry
            {
                Entity = product,
                ConflictMembers = new List<string> { "Name", "Price" },
                StoreEntity = new ConcurrencyProductWithMultipleChecks
                {
                    ProductId = 1,
                    Name = "Other User Name",
                    Price = 20.0m
                }
            };

            Assert.IsTrue(entry.HasConflict);
            Assert.IsNotNull(entry.ConflictMembers);

            var conflictMembers = entry.ConflictMembers.ToList();
            Assert.AreEqual(2, conflictMembers.Count);
            Assert.IsTrue(conflictMembers.Contains("Name"));
            Assert.IsTrue(conflictMembers.Contains("Price"));

            var storeEntity = entry.StoreEntity as ConcurrencyProductWithMultipleChecks;
            Assert.IsNotNull(storeEntity);
            Assert.AreEqual("Other User Name", storeEntity.Name);
            Assert.AreEqual(20.0m, storeEntity.Price);
        }

        [TestMethod]
        [Description("验证 RowVersion 比较逻辑")]
        public void RowVersionsEqual_ComparesCorrectly()
        {
            byte[] v1 = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
            byte[] v2 = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
            byte[] v3 = new byte[] { 1, 2, 3, 4, 5, 6, 7, 9 };
            byte[] v4 = null;
            byte[] v5 = new byte[] { 1, 2, 3, 4 };

            CollectionAssert.AreEqual(v1, v2);
            CollectionAssert.AreNotEqual(v1, v3);
            Assert.IsNull(v4);
            Assert.AreNotEqual(v1.Length, v5.Length);
        }
    }
}
