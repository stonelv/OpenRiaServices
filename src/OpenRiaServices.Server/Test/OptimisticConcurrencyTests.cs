using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenRiaServices.Server.Test
{
    [TestClass]
    public class OptimisticConcurrencyTests
    {
        #region Test Entities

        public class ProductWithRowVersion
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

        public class ProductWithMultipleConcurrencyChecks
        {
            [Key]
            public int ProductId { get; set; }

            [ConcurrencyCheck]
            public string Name { get; set; }

            [ConcurrencyCheck]
            public decimal Price { get; set; }
        }

        #endregion

        #region ChangeSetEntry Conflict Tests

        [TestMethod]
        [Description("验证 ChangeSetEntry 正确识别冲突状态")]
        public void ChangeSetEntry_HasConflict_WithConflictMembers()
        {
            var entry = new ChangeSetEntry
            {
                Entity = new ProductWithRowVersion { ProductId = 1, Name = "Test" },
                ConflictMembers = new List<string> { "Name", "Price" }
            };

            Assert.IsTrue(entry.HasConflict, "当设置 ConflictMembers 时，HasConflict 应为 true");
        }

        [TestMethod]
        [Description("验证 ChangeSetEntry 正确识别删除冲突")]
        public void ChangeSetEntry_HasConflict_WithIsDeleteConflict()
        {
            var entry = new ChangeSetEntry
            {
                Entity = new ProductWithRowVersion { ProductId = 1 },
                IsDeleteConflict = true
            };

            Assert.IsTrue(entry.HasConflict, "当设置 IsDeleteConflict 时，HasConflict 应为 true");
        }

        [TestMethod]
        [Description("验证 ChangeSetEntry 在没有冲突时返回 false")]
        public void ChangeSetEntry_HasConflict_NoConflict()
        {
            var entry = new ChangeSetEntry
            {
                Entity = new ProductWithRowVersion { ProductId = 1, Name = "Test" }
            };

            Assert.IsFalse(entry.HasConflict, "当没有冲突时，HasConflict 应为 false");
        }

        [TestMethod]
        [Description("验证 ChangeSetEntry 正确区分验证错误和冲突")]
        public void ChangeSetEntry_HasError_ValidationErrorVsConflict()
        {
            var entryWithValidationError = new ChangeSetEntry
            {
                Entity = new ProductWithRowVersion { ProductId = 1 },
                ValidationErrors = new List<ValidationResultInfo>
                {
                    new ValidationResultInfo("Name 是必填项", new[] { "Name" })
                }
            };

            var entryWithConflict = new ChangeSetEntry
            {
                Entity = new ProductWithRowVersion { ProductId = 1 },
                ConflictMembers = new List<string> { "RowVersion" }
            };

            var entryWithBoth = new ChangeSetEntry
            {
                Entity = new ProductWithRowVersion { ProductId = 1 },
                ValidationErrors = new List<ValidationResultInfo>
                {
                    new ValidationResultInfo("Name 是必填项", new[] { "Name" })
                },
                ConflictMembers = new List<string> { "RowVersion" }
            };

            Assert.IsTrue(entryWithValidationError.HasError, "有验证错误时 HasError 应为 true");
            Assert.IsFalse(entryWithValidationError.HasConflict, "只有验证错误时 HasConflict 应为 false");

            Assert.IsTrue(entryWithConflict.HasError, "有冲突时 HasError 应为 true");
            Assert.IsTrue(entryWithConflict.HasConflict, "有冲突时 HasConflict 应为 true");
            Assert.IsNull(entryWithConflict.ValidationErrors, "只有冲突时 ValidationErrors 应为 null");

            Assert.IsTrue(entryWithBoth.HasError, "有验证错误和冲突时 HasError 应为 true");
            Assert.IsTrue(entryWithBoth.HasConflict, "有冲突时 HasConflict 应为 true");
            Assert.IsNotNull(entryWithBoth.ValidationErrors, "有验证错误时 ValidationErrors 不应为 null");
        }

        #endregion

        #region ChangeSet HasError Tests

        [TestMethod]
        [Description("验证 ChangeSet 正确识别包含验证错误的条目")]
        public void ChangeSet_HasError_WithValidationErrors()
        {
            var entries = new List<ChangeSetEntry>
            {
                new ChangeSetEntry
                {
                    Id = 1,
                    Entity = new ProductWithRowVersion { ProductId = 1 },
                    ValidationErrors = new List<ValidationResultInfo>
                    {
                        new ValidationResultInfo("Name 是必填项", new[] { "Name" })
                    }
                },
                new ChangeSetEntry
                {
                    Id = 2,
                    Entity = new ProductWithRowVersion { ProductId = 2, Name = "Valid" }
                }
            };

            var changeSet = new ChangeSet(entries);

            Assert.IsTrue(changeSet.HasError, "有验证错误时 ChangeSet.HasError 应为 true");
        }

        [TestMethod]
        [Description("验证 ChangeSet 正确识别包含冲突的条目")]
        public void ChangeSet_HasError_WithConflicts()
        {
            var entries = new List<ChangeSetEntry>
            {
                new ChangeSetEntry
                {
                    Id = 1,
                    Entity = new ProductWithRowVersion { ProductId = 1, Name = "Test1" }
                },
                new ChangeSetEntry
                {
                    Id = 2,
                    Entity = new ProductWithRowVersion { ProductId = 2, Name = "Test2" },
                    ConflictMembers = new List<string> { "RowVersion" },
                    StoreEntity = new ProductWithRowVersion { ProductId = 2, Name = "Updated" }
                }
            };

            var changeSet = new ChangeSet(entries);

            Assert.IsTrue(changeSet.HasError, "有冲突时 ChangeSet.HasError 应为 true");
        }

        [TestMethod]
        [Description("验证 ChangeSet 在没有错误时返回 false")]
        public void ChangeSet_HasError_NoErrors()
        {
            var entries = new List<ChangeSetEntry>
            {
                new ChangeSetEntry
                {
                    Id = 1,
                    Entity = new ProductWithRowVersion { ProductId = 1, Name = "Test1" }
                },
                new ChangeSetEntry
                {
                    Id = 2,
                    Entity = new ProductWithRowVersion { ProductId = 2, Name = "Test2" }
                }
            };

            var changeSet = new ChangeSet(entries);

            Assert.IsFalse(changeSet.HasError, "没有错误时 ChangeSet.HasError 应为 false");
        }

        #endregion

        #region StoreEntity Tests

        [TestMethod]
        [Description("验证 StoreEntity 正确存储数据库中的当前值")]
        public void ChangeSetEntry_StoreEntity_StoresDatabaseValues()
        {
            var originalEntity = new ProductWithRowVersion
            {
                ProductId = 1,
                Name = "Original Name",
                Price = 10.0m,
                RowVersion = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }
            };

            var storeEntity = new ProductWithRowVersion
            {
                ProductId = 1,
                Name = "Updated Name",
                Price = 15.0m,
                RowVersion = new byte[] { 9, 10, 11, 12, 13, 14, 15, 16 }
            };

            var entry = new ChangeSetEntry
            {
                Id = 1,
                Entity = new ProductWithRowVersion
                {
                    ProductId = 1,
                    Name = "Client Modified Name",
                    Price = 12.0m,
                    RowVersion = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }
                },
                OriginalEntity = originalEntity,
                StoreEntity = storeEntity,
                ConflictMembers = new List<string> { "Name", "Price", "RowVersion" }
            };

            var storedProduct = entry.StoreEntity as ProductWithRowVersion;
            Assert.IsNotNull(storedProduct);
            Assert.AreEqual("Updated Name", storedProduct.Name);
            Assert.AreEqual(15.0m, storedProduct.Price);
            CollectionAssert.AreEqual(new byte[] { 9, 10, 11, 12, 13, 14, 15, 16 }, storedProduct.RowVersion);
        }

        [TestMethod]
        [Description("验证 IsDeleteConflict 在实体已被删除时正确设置")]
        public void ChangeSetEntry_IsDeleteConflict_WhenEntityDeletedInStore()
        {
            var entry = new ChangeSetEntry
            {
                Id = 1,
                Entity = new ProductWithRowVersion { ProductId = 1, Name = "Test" },
                OriginalEntity = new ProductWithRowVersion { ProductId = 1, Name = "Original" },
                IsDeleteConflict = true,
                StoreEntity = null
            };

            Assert.IsTrue(entry.IsDeleteConflict);
            Assert.IsTrue(entry.HasConflict);
            Assert.IsNull(entry.StoreEntity);
        }

        #endregion

        #region ValidationResultInfo Tests

        [TestMethod]
        [Description("验证 ValidationResultInfo 正确存储验证错误信息")]
        public void ValidationResultInfo_StoresErrorInformation()
        {
            var errorMessage = "Price 必须大于 0";
            var memberNames = new[] { "Price" };
            var stackTrace = "at TestMethod() in Test.cs:line 42";

            var validationInfo = new ValidationResultInfo(errorMessage, memberNames)
            {
                StackTrace = stackTrace,
                ErrorCode = 1001
            };

            Assert.AreEqual(errorMessage, validationInfo.Message);
            CollectionAssert.AreEqual(memberNames, validationInfo.MemberNames.ToArray());
            Assert.AreEqual(stackTrace, validationInfo.StackTrace);
            Assert.AreEqual(1001, validationInfo.ErrorCode);
        }

        [TestMethod]
        [Description("验证 ValidationResultInfo 与冲突信息的区别")]
        public void ValidationResultInfo_VsConflictInformation()
        {
            var entry = new ChangeSetEntry
            {
                Id = 1,
                Entity = new ProductWithRowVersion { ProductId = 1 },
                ValidationErrors = new List<ValidationResultInfo>
                {
                    new ValidationResultInfo("Name 不能为空", new[] { "Name" }),
                    new ValidationResultInfo("Price 必须大于 0", new[] { "Price" })
                },
                ConflictMembers = new List<string> { "RowVersion" },
                StoreEntity = new ProductWithRowVersion
                {
                    ProductId = 1,
                    Name = "Other Name",
                    RowVersion = new byte[] { 2, 0, 0, 0, 0, 0, 0, 0 }
                }
            };

            var validationErrors = entry.ValidationErrors.ToList();
            Assert.AreEqual(2, validationErrors.Count);
            Assert.AreEqual("Name 不能为空", validationErrors[0].Message);
            CollectionAssert.AreEqual(new[] { "Name" }, validationErrors[0].MemberNames.ToArray());

            var conflictMembers = entry.ConflictMembers.ToList();
            Assert.AreEqual(1, conflictMembers.Count);
            Assert.AreEqual("RowVersion", conflictMembers[0]);

            var storeProduct = entry.StoreEntity as ProductWithRowVersion;
            Assert.IsNotNull(storeProduct);
            CollectionAssert.AreEqual(new byte[] { 2, 0, 0, 0, 0, 0, 0, 0 }, storeProduct.RowVersion);
        }

        #endregion

        #region Timestamp Attribute Recognition Tests

        [TestMethod]
        [Description("验证带有 Timestamp 属性的实体会被正确识别")]
        public void TimestampEntity_RecognizesTimestampAttribute()
        {
            var productType = typeof(ProductWithRowVersion);
            var versionProperty = productType.GetProperty("RowVersion");

            Assert.IsNotNull(versionProperty, "RowVersion 属性应该存在");

            var timestampAttribute = versionProperty.GetCustomAttributes(typeof(TimestampAttribute), false);
            var concurrencyCheckAttribute = versionProperty.GetCustomAttributes(typeof(ConcurrencyCheckAttribute), false);

            Assert.AreEqual(1, timestampAttribute.Length, "RowVersion 应该有 TimestampAttribute");
            Assert.AreEqual(1, concurrencyCheckAttribute.Length, "RowVersion 应该有 ConcurrencyCheckAttribute");
        }

        [TestMethod]
        [Description("验证带有多个 ConcurrencyCheck 属性的实体")]
        public void Entity_WithMultipleConcurrencyChecks()
        {
            var productType = typeof(ProductWithMultipleConcurrencyChecks);
            var nameProperty = productType.GetProperty("Name");
            var priceProperty = productType.GetProperty("Price");

            var nameConcurrencyCheck = nameProperty.GetCustomAttributes(typeof(ConcurrencyCheckAttribute), false);
            var priceConcurrencyCheck = priceProperty.GetCustomAttributes(typeof(ConcurrencyCheckAttribute), false);

            Assert.AreEqual(1, nameConcurrencyCheck.Length, "Name 应该有 ConcurrencyCheckAttribute");
            Assert.AreEqual(1, priceConcurrencyCheck.Length, "Price 应该有 ConcurrencyCheckAttribute");
        }

        #endregion
    }
}
