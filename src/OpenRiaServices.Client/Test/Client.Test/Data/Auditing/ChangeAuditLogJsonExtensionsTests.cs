using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenRiaServices.Client.Auditing;

#nullable enable

namespace OpenRiaServices.Client.Test
{
    /// <summary>
    /// 测试 ChangeAuditLog 的 JSON 序列化功能
    /// </summary>
    [TestClass]
    public class ChangeAuditLogJsonExtensionsTests
    {
        /// <summary>
        /// 测试基本的 JSON 序列化，验证输出是有效的 JSON
        /// </summary>
        [TestMethod]
        public void ToJson_BasicSerialization_ProducesValidJson()
        {
            // Arrange
            var auditLog = new ChangeAuditLog
            {
                LogId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
                Timestamp = new DateTime(2026, 5, 2, 10, 30, 0, DateTimeKind.Utc),
                OperationDescription = "测试操作",
                UserName = "testUser"
            };

            // Act
            string json = auditLog.ToJson();

            // Assert
            // 验证是有效的 JSON
            var parsed = JsonDocument.Parse(json);
            Assert.IsNotNull(parsed);

            // 验证内容
            var root = parsed.RootElement;
            Assert.AreEqual("3fa85f64-5717-4562-b3fc-2c963f66afa6", root.GetProperty("logId").GetString());
            Assert.AreEqual("测试操作", root.GetProperty("operationDescription").GetString());
            Assert.AreEqual("testUser", root.GetProperty("userName").GetString());
        }

        /// <summary>
        /// 测试当没有 customData 时，JSON 不包含尾随逗号
        /// </summary>
        [TestMethod]
        public void ToJson_WithoutCustomData_NoTrailingComma()
        {
            // Arrange
            var auditLog = new ChangeAuditLog
            {
                LogId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
                Timestamp = new DateTime(2026, 5, 2, 10, 30, 0, DateTimeKind.Utc)
            };

            // Act
            string json = auditLog.ToJson();

            // Assert
            // 验证 JSON 可以被正确解析
            var parsed = JsonDocument.Parse(json);
            Assert.IsNotNull(parsed);

            // 验证最后一个字符是 }
            string trimmed = json.Trim();
            Assert.AreEqual('}', trimmed[trimmed.Length - 1]);

            // 验证 entityChanges 后面没有逗号（当没有 customData 时）
            StringAssert.DoesNotMatch(json, new System.Text.RegularExpressions.Regex(@"\[\]\s*,\s*\}"));
        }

        /// <summary>
        /// 测试带有 customData 的序列化
        /// </summary>
        [TestMethod]
        public void ToJson_WithCustomData_SerializesCorrectly()
        {
            // Arrange
            var auditLog = new ChangeAuditLog
            {
                LogId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
                Timestamp = new DateTime(2026, 5, 2, 10, 30, 0, DateTimeKind.Utc),
                CustomData = new Dictionary<string, object?>
                {
                    { "RequestId", "req-123" },
                    { "IpAddress", "192.168.1.1" }
                }
            };

            // Act
            string json = auditLog.ToJson();

            // Assert
            var parsed = JsonDocument.Parse(json);
            var root = parsed.RootElement;

            // 验证 customData 存在
            Assert.IsTrue(root.TryGetProperty("customData", out var customData));
            Assert.AreEqual("req-123", customData.GetProperty("RequestId").GetString());
            Assert.AreEqual("192.168.1.1", customData.GetProperty("IpAddress").GetString());
        }

        /// <summary>
        /// 测试实体变更记录的序列化
        /// </summary>
        [TestMethod]
        public void ToJson_WithEntityChanges_SerializesCorrectly()
        {
            // Arrange
            var auditLog = new ChangeAuditLog
            {
                LogId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
                Timestamp = new DateTime(2026, 5, 2, 10, 30, 0, DateTimeKind.Utc),
                EntityChanges = new List<EntityChangeEntry>
                {
                    new EntityChangeEntry
                    {
                        EntityTypeName = "TestNamespace.Employee",
                        EntityName = "Employee",
                        Operation = EntityChangeOperation.Modified,
                        KeyValues = new Dictionary<string, object?> { { "Id", 123 } },
                        PropertyChanges = new List<PropertyChangeEntry>
                        {
                            new PropertyChangeEntry
                            {
                                PropertyName = "Name",
                                OriginalValue = "张三",
                                NewValue = "李四",
                                PropertyType = "System.String"
                            }
                        },
                        OriginalValues = new Dictionary<string, object?> { { "Name", "张三" } },
                        CurrentValues = new Dictionary<string, object?> { { "Name", "李四" } }
                    }
                }
            };

            // Act
            string json = auditLog.ToJson();

            // Assert
            var parsed = JsonDocument.Parse(json);
            var root = parsed.RootElement;

            // 验证计数
            Assert.AreEqual(0, root.GetProperty("addedCount").GetInt32());
            Assert.AreEqual(1, root.GetProperty("modifiedCount").GetInt32());
            Assert.AreEqual(0, root.GetProperty("removedCount").GetInt32());
            Assert.AreEqual(1, root.GetProperty("totalChanges").GetInt32());

            // 验证 entityChanges
            var entityChanges = root.GetProperty("entityChanges");
            Assert.AreEqual(1, entityChanges.GetArrayLength());

            var entityChange = entityChanges[0];
            Assert.AreEqual("Employee", entityChange.GetProperty("entityName").GetString());
            Assert.AreEqual("Modified", entityChange.GetProperty("operation").GetString());

            // 验证 keyValues
            var keyValues = entityChange.GetProperty("keyValues");
            Assert.AreEqual(123, keyValues.GetProperty("Id").GetInt32());

            // 验证 propertyChanges
            var propertyChanges = entityChange.GetProperty("propertyChanges");
            Assert.AreEqual(1, propertyChanges.GetArrayLength());

            var propChange = propertyChanges[0];
            Assert.AreEqual("Name", propChange.GetProperty("propertyName").GetString());
            Assert.AreEqual("张三", propChange.GetProperty("originalValue").GetString());
            Assert.AreEqual("李四", propChange.GetProperty("newValue").GetString());
        }

        /// <summary>
        /// 测试空的 entityChanges 数组
        /// </summary>
        [TestMethod]
        public void ToJson_WithEmptyEntityChanges_SerializesCorrectly()
        {
            // Arrange
            var auditLog = new ChangeAuditLog
            {
                LogId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
                Timestamp = new DateTime(2026, 5, 2, 10, 30, 0, DateTimeKind.Utc),
                EntityChanges = new List<EntityChangeEntry>()
            };

            // Act
            string json = auditLog.ToJson();

            // Assert
            var parsed = JsonDocument.Parse(json);
            var root = parsed.RootElement;

            var entityChanges = root.GetProperty("entityChanges");
            Assert.AreEqual(0, entityChanges.GetArrayLength());
        }

        /// <summary>
        /// 测试格式化输出
        /// </summary>
        [TestMethod]
        public void ToJson_WithWriteIndented_ProducesFormattedJson()
        {
            // Arrange
            var auditLog = new ChangeAuditLog
            {
                LogId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
                Timestamp = new DateTime(2026, 5, 2, 10, 30, 0, DateTimeKind.Utc)
            };

            // Act
            string json = auditLog.ToJson(writeIndented: true);

            // Assert
            // 验证包含换行符
            StringAssert.Contains(json, Environment.NewLine);

            // 验证仍然是有效的 JSON
            var parsed = JsonDocument.Parse(json);
            Assert.IsNotNull(parsed);
        }

        /// <summary>
        /// 测试 Added 状态的实体
        /// </summary>
        [TestMethod]
        public void ToJson_WithAddedEntity_SerializesCorrectly()
        {
            // Arrange
            var auditLog = new ChangeAuditLog
            {
                LogId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
                Timestamp = new DateTime(2026, 5, 2, 10, 30, 0, DateTimeKind.Utc),
                EntityChanges = new List<EntityChangeEntry>
                {
                    new EntityChangeEntry
                    {
                        EntityTypeName = "TestNamespace.Product",
                        EntityName = "Product",
                        Operation = EntityChangeOperation.Added,
                        KeyValues = new Dictionary<string, object?> { { "Id", 1 } },
                        CurrentValues = new Dictionary<string, object?>
                        {
                            { "Name", "新产品" },
                            { "Price", 99.99 }
                        }
                    }
                }
            };

            // Act
            string json = auditLog.ToJson();

            // Assert
            var parsed = JsonDocument.Parse(json);
            var root = parsed.RootElement;

            Assert.AreEqual(1, root.GetProperty("addedCount").GetInt32());
            Assert.AreEqual(0, root.GetProperty("modifiedCount").GetInt32());

            var entityChange = root.GetProperty("entityChanges")[0];
            Assert.AreEqual("Added", entityChange.GetProperty("operation").GetString());
        }

        /// <summary>
        /// 测试 Removed 状态的实体
        /// </summary>
        [TestMethod]
        public void ToJson_WithRemovedEntity_SerializesCorrectly()
        {
            // Arrange
            var auditLog = new ChangeAuditLog
            {
                LogId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
                Timestamp = new DateTime(2026, 5, 2, 10, 30, 0, DateTimeKind.Utc),
                EntityChanges = new List<EntityChangeEntry>
                {
                    new EntityChangeEntry
                    {
                        EntityTypeName = "TestNamespace.Order",
                        EntityName = "Order",
                        Operation = EntityChangeOperation.Removed,
                        KeyValues = new Dictionary<string, object?> { { "OrderId", 999 } },
                        OriginalValues = new Dictionary<string, object?>
                        {
                            { "OrderNumber", "ORD-001" },
                            { "Status", "Cancelled" }
                        }
                    }
                }
            };

            // Act
            string json = auditLog.ToJson();

            // Assert
            var parsed = JsonDocument.Parse(json);
            var root = parsed.RootElement;

            Assert.AreEqual(0, root.GetProperty("addedCount").GetInt32());
            Assert.AreEqual(0, root.GetProperty("modifiedCount").GetInt32());
            Assert.AreEqual(1, root.GetProperty("removedCount").GetInt32());

            var entityChange = root.GetProperty("entityChanges")[0];
            Assert.AreEqual("Removed", entityChange.GetProperty("operation").GetString());
        }

        /// <summary>
        /// 测试多种类型的值序列化
        /// </summary>
        [TestMethod]
        public void ToJson_WithVariousValueTypes_SerializesCorrectly()
        {
            // Arrange
            var auditLog = new ChangeAuditLog
            {
                LogId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
                Timestamp = new DateTime(2026, 5, 2, 10, 30, 0, DateTimeKind.Utc),
                EntityChanges = new List<EntityChangeEntry>
                {
                    new EntityChangeEntry
                    {
                        EntityName = "TestEntity",
                        EntityTypeName = "Test.TestEntity",
                        Operation = EntityChangeOperation.Modified,
                        KeyValues = new Dictionary<string, object?>(),
                        PropertyChanges = new List<PropertyChangeEntry>
                        {
                            new PropertyChangeEntry
                            {
                                PropertyName = "StringProp",
                                OriginalValue = "old",
                                NewValue = "new",
                                PropertyType = "System.String"
                            },
                            new PropertyChangeEntry
                            {
                                PropertyName = "IntProp",
                                OriginalValue = 1,
                                NewValue = 2,
                                PropertyType = "System.Int32"
                            },
                            new PropertyChangeEntry
                            {
                                PropertyName = "BoolProp",
                                OriginalValue = true,
                                NewValue = false,
                                PropertyType = "System.Boolean"
                            },
                            new PropertyChangeEntry
                            {
                                PropertyName = "NullProp",
                                OriginalValue = null,
                                NewValue = null,
                                PropertyType = "System.Object"
                            },
                            new PropertyChangeEntry
                            {
                                PropertyName = "DecimalProp",
                                OriginalValue = 123.45m,
                                NewValue = 678.90m,
                                PropertyType = "System.Decimal"
                            }
                        }
                    }
                }
            };

            // Act
            string json = auditLog.ToJson();

            // Assert
            var parsed = JsonDocument.Parse(json);
            var root = parsed.RootElement;
            var propChanges = root.GetProperty("entityChanges")[0].GetProperty("propertyChanges");

            // 验证字符串
            var stringProp = propChanges[0];
            Assert.AreEqual("old", stringProp.GetProperty("originalValue").GetString());
            Assert.AreEqual("new", stringProp.GetProperty("newValue").GetString());

            // 验证整数
            var intProp = propChanges[1];
            Assert.AreEqual(1, intProp.GetProperty("originalValue").GetInt32());
            Assert.AreEqual(2, intProp.GetProperty("newValue").GetInt32());

            // 验证布尔值
            var boolProp = propChanges[2];
            Assert.AreEqual(true, boolProp.GetProperty("originalValue").GetBoolean());
            Assert.AreEqual(false, boolProp.GetProperty("newValue").GetBoolean());

            // 验证 null
            var nullProp = propChanges[3];
            Assert.AreEqual(JsonValueKind.Null, nullProp.GetProperty("originalValue").ValueKind);
            Assert.AreEqual(JsonValueKind.Null, nullProp.GetProperty("newValue").ValueKind);
        }

        /// <summary>
        /// 测试可选属性（PropertyType）缺失的情况
        /// </summary>
        [TestMethod]
        public void ToJson_WithoutPropertyType_SerializesCorrectly()
        {
            // Arrange
            var auditLog = new ChangeAuditLog
            {
                LogId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
                Timestamp = new DateTime(2026, 5, 2, 10, 30, 0, DateTimeKind.Utc),
                EntityChanges = new List<EntityChangeEntry>
                {
                    new EntityChangeEntry
                    {
                        EntityName = "Test",
                        EntityTypeName = "Test",
                        Operation = EntityChangeOperation.Modified,
                        KeyValues = new Dictionary<string, object?>(),
                        PropertyChanges = new List<PropertyChangeEntry>
                        {
                            new PropertyChangeEntry
                            {
                                PropertyName = "Name",
                                OriginalValue = "A",
                                NewValue = "B"
                                // PropertyType 为 null
                            }
                        }
                    }
                }
            };

            // Act
            string json = auditLog.ToJson();

            // Assert
            var parsed = JsonDocument.Parse(json);
            var propChange = parsed.RootElement.GetProperty("entityChanges")[0].GetProperty("propertyChanges")[0];

            // 验证 propertyType 不存在
            Assert.IsFalse(propChange.TryGetProperty("propertyType", out _));

            // 验证其他属性存在
            Assert.AreEqual("Name", propChange.GetProperty("propertyName").GetString());
            Assert.AreEqual("A", propChange.GetProperty("originalValue").GetString());
            Assert.AreEqual("B", propChange.GetProperty("newValue").GetString());
        }

        /// <summary>
        /// 测试空对象的情况（没有可选属性）
        /// </summary>
        [TestMethod]
        public void ToJson_MinimalObject_SerializesCorrectly()
        {
            // Arrange
            var auditLog = new ChangeAuditLog
            {
                LogId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
                Timestamp = new DateTime(2026, 5, 2, 10, 30, 0, DateTimeKind.Utc)
            };

            // Act
            string json = auditLog.ToJson();

            // Assert
            // 验证 JSON 结构正确
            var parsed = JsonDocument.Parse(json);
            var root = parsed.RootElement;

            // 必需属性存在
            Assert.IsTrue(root.TryGetProperty("logId", out _));
            Assert.IsTrue(root.TryGetProperty("timestamp", out _));
            Assert.IsTrue(root.TryGetProperty("addedCount", out _));
            Assert.IsTrue(root.TryGetProperty("modifiedCount", out _));
            Assert.IsTrue(root.TryGetProperty("removedCount", out _));
            Assert.IsTrue(root.TryGetProperty("totalChanges", out _));
            Assert.IsTrue(root.TryGetProperty("entityChanges", out _));

            // 可选属性不存在
            Assert.IsFalse(root.TryGetProperty("operationDescription", out _));
            Assert.IsFalse(root.TryGetProperty("userName", out _));
            Assert.IsFalse(root.TryGetProperty("customData", out _));
        }

        /// <summary>
        /// 测试当 entityChanges 为最后一个属性且无 customData 时不产生尾随逗号
        /// </summary>
        [TestMethod]
        public void ToJson_EntityChangesAsLastProperty_NoTrailingComma()
        {
            // Arrange
            var auditLog = new ChangeAuditLog
            {
                LogId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
                Timestamp = new DateTime(2026, 5, 2, 10, 30, 0, DateTimeKind.Utc),
                OperationDescription = "测试操作",
                UserName = "testUser",
                // 注意：不设置 CustomData，使其为 null
                EntityChanges = new List<EntityChangeEntry>
                {
                    new EntityChangeEntry
                    {
                        EntityName = "Employee",
                        EntityTypeName = "Test.Employee",
                        Operation = EntityChangeOperation.Modified,
                        KeyValues = new Dictionary<string, object?> { { "Id", 1 } },
                        PropertyChanges = new List<PropertyChangeEntry>
                        {
                            new PropertyChangeEntry
                            {
                                PropertyName = "Name",
                                OriginalValue = "Old",
                                NewValue = "New",
                                PropertyType = "System.String"
                            }
                        }
                    }
                }
            };

            // Act
            string json = auditLog.ToJson();

            // Assert
            // 1. 验证 JSON 可以被正确解析（这是最关键的验证）
            var parsed = JsonDocument.Parse(json);
            Assert.IsNotNull(parsed);

            // 2. 验证 customData 不存在
            var root = parsed.RootElement;
            Assert.IsFalse(root.TryGetProperty("customData", out _), "customData 不应该存在");

            // 3. 验证 entityChanges 存在且有数据
            Assert.IsTrue(root.TryGetProperty("entityChanges", out var entityChanges));
            Assert.AreEqual(1, entityChanges.GetArrayLength());

            // 4. 验证最后一个字符是 }
            string trimmedJson = json.Trim();
            Assert.AreEqual('}', trimmedJson[trimmedJson.Length - 1], "JSON 应该以 } 结尾");

            // 5. 验证不存在尾随逗号模式（] 或 } 后面跟 , 然后跟 }）
            // 更严格的验证：检查 entityChanges 数组的闭合 ] 后面没有逗号
            int entityChangesIndex = json.IndexOf("\"entityChanges\"");
            Assert.IsTrue(entityChangesIndex > 0);

            // 从 entityChanges 开始找，确认数组闭合后没有逗号
            // 简化验证：用正则检查是否存在无效模式
            var invalidPattern = new System.Text.RegularExpressions.Regex(@"\]\s*,\s*\}");
            StringAssert.DoesNotMatch(json, invalidPattern, "不应该存在尾随逗号");
        }

        /// <summary>
        /// 测试所有属性完整时的 JSON 结构
        /// </summary>
        [TestMethod]
        public void ToJson_AllPropertiesComplete_StructureIsValid()
        {
            // Arrange
            var auditLog = new ChangeAuditLog
            {
                LogId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
                Timestamp = new DateTime(2026, 5, 2, 10, 30, 0, DateTimeKind.Utc),
                OperationDescription = "完整的保存操作",
                UserName = "admin@example.com",
                CustomData = new Dictionary<string, object?>
                {
                    { "RequestId", "req-2026-001" },
                    { "ClientIp", "192.168.1.100" },
                    { "IsRetry", false },
                    { "RetryCount", 3 }
                },
                EntityChanges = new List<EntityChangeEntry>
                {
                    // Added 实体
                    new EntityChangeEntry
                    {
                        EntityName = "Product",
                        EntityTypeName = "Catalog.Product",
                        Operation = EntityChangeOperation.Added,
                        KeyValues = new Dictionary<string, object?> { { "ProductId", 1001 } },
                        CurrentValues = new Dictionary<string, object?>
                        {
                            { "Name", "新产品" },
                            { "Price", 99.99m },
                            { "IsActive", true }
                        },
                        PropertyChanges = new List<PropertyChangeEntry>
                        {
                            new PropertyChangeEntry
                            {
                                PropertyName = "Name",
                                OriginalValue = null,
                                NewValue = "新产品",
                                PropertyType = "System.String"
                            },
                            new PropertyChangeEntry
                            {
                                PropertyName = "Price",
                                OriginalValue = null,
                                NewValue = 99.99m,
                                PropertyType = "System.Decimal"
                            }
                        }
                    },
                    // Modified 实体
                    new EntityChangeEntry
                    {
                        EntityName = "Customer",
                        EntityTypeName = "Sales.Customer",
                        Operation = EntityChangeOperation.Modified,
                        KeyValues = new Dictionary<string, object?> { { "CustomerId", 50 } },
                        OriginalValues = new Dictionary<string, object?>
                        {
                            { "Name", "老客户" },
                            { "Email", "old@example.com" }
                        },
                        CurrentValues = new Dictionary<string, object?>
                        {
                            { "Name", "新客户" },
                            { "Email", "new@example.com" }
                        },
                        PropertyChanges = new List<PropertyChangeEntry>
                        {
                            new PropertyChangeEntry
                            {
                                PropertyName = "Name",
                                OriginalValue = "老客户",
                                NewValue = "新客户",
                                PropertyType = "System.String"
                            },
                            new PropertyChangeEntry
                            {
                                PropertyName = "Email",
                                OriginalValue = "old@example.com",
                                NewValue = "new@example.com",
                                PropertyType = "System.String"
                            }
                        }
                    },
                    // Removed 实体
                    new EntityChangeEntry
                    {
                        EntityName = "Order",
                        EntityTypeName = "Sales.Order",
                        Operation = EntityChangeOperation.Removed,
                        KeyValues = new Dictionary<string, object?> { { "OrderId", 999 } },
                        OriginalValues = new Dictionary<string, object?>
                        {
                            { "OrderNumber", "ORD-2026-001" },
                            { "Status", "Cancelled" }
                        },
                        PropertyChanges = new List<PropertyChangeEntry>
                        {
                            new PropertyChangeEntry
                            {
                                PropertyName = "OrderNumber",
                                OriginalValue = "ORD-2026-001",
                                NewValue = null,
                                PropertyType = "System.String"
                            }
                        }
                    }
                }
            };

            // Act
            string json = auditLog.ToJson();

            // Assert
            // 1. 验证是有效的 JSON
            var parsed = JsonDocument.Parse(json);
            var root = parsed.RootElement;

            // 2. 验证顶层属性
            Assert.AreEqual("3fa85f64-5717-4562-b3fc-2c963f66afa6", root.GetProperty("logId").GetString());
            Assert.AreEqual("完整的保存操作", root.GetProperty("operationDescription").GetString());
            Assert.AreEqual("admin@example.com", root.GetProperty("userName").GetString());
            Assert.AreEqual(1, root.GetProperty("addedCount").GetInt32());
            Assert.AreEqual(1, root.GetProperty("modifiedCount").GetInt32());
            Assert.AreEqual(1, root.GetProperty("removedCount").GetInt32());
            Assert.AreEqual(3, root.GetProperty("totalChanges").GetInt32());

            // 3. 验证 customData
            var customData = root.GetProperty("customData");
            Assert.AreEqual("req-2026-001", customData.GetProperty("RequestId").GetString());
            Assert.AreEqual("192.168.1.100", customData.GetProperty("ClientIp").GetString());
            Assert.AreEqual(false, customData.GetProperty("IsRetry").GetBoolean());
            Assert.AreEqual(3, customData.GetProperty("RetryCount").GetInt32());

            // 4. 验证 entityChanges
            var entityChanges = root.GetProperty("entityChanges");
            Assert.AreEqual(3, entityChanges.GetArrayLength());

            // 验证第一个实体（Added）
            var addedEntity = entityChanges[0];
            Assert.AreEqual("Product", addedEntity.GetProperty("entityName").GetString());
            Assert.AreEqual("Added", addedEntity.GetProperty("operation").GetString());
            Assert.AreEqual(1001, addedEntity.GetProperty("keyValues").GetProperty("ProductId").GetInt32());
            Assert.IsTrue(addedEntity.TryGetProperty("currentValues", out _));
            Assert.IsFalse(addedEntity.TryGetProperty("originalValues", out _));

            // 验证第二个实体（Modified）
            var modifiedEntity = entityChanges[1];
            Assert.AreEqual("Customer", modifiedEntity.GetProperty("entityName").GetString());
            Assert.AreEqual("Modified", modifiedEntity.GetProperty("operation").GetString());
            Assert.IsTrue(modifiedEntity.TryGetProperty("originalValues", out _));
            Assert.IsTrue(modifiedEntity.TryGetProperty("currentValues", out _));

            // 验证第三个实体（Removed）
            var removedEntity = entityChanges[2];
            Assert.AreEqual("Order", removedEntity.GetProperty("entityName").GetString());
            Assert.AreEqual("Removed", removedEntity.GetProperty("operation").GetString());
            Assert.IsTrue(removedEntity.TryGetProperty("originalValues", out _));
            Assert.IsFalse(removedEntity.TryGetProperty("currentValues", out _));

            // 5. 验证 propertyChanges 中的 null 值处理
            var addedProps = addedEntity.GetProperty("propertyChanges");
            var firstAddedProp = addedProps[0];
            Assert.AreEqual(JsonValueKind.Null, firstAddedProp.GetProperty("originalValue").ValueKind);

            var removedProps = removedEntity.GetProperty("propertyChanges");
            var firstRemovedProp = removedProps[0];
            Assert.AreEqual(JsonValueKind.Null, firstRemovedProp.GetProperty("newValue").ValueKind);
        }

        /// <summary>
        /// 验证特定属性缺失时逗号正确处理
        /// 测试多种可选属性缺失的组合情况
        /// </summary>
        [TestMethod]
        public void ToJson_VariousOptionalPropertiesMissing_CommasHandledCorrectly()
        {
            // 场景 1: 缺少 operationDescription，但有 userName
            var log1 = new ChangeAuditLog
            {
                LogId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
                Timestamp = new DateTime(2026, 5, 2, 10, 30, 0, DateTimeKind.Utc),
                // operationDescription 缺失
                UserName = "user1",
                // customData 缺失
                // entityChanges 为空列表
            };
            TestJsonValidity(log1, "场景 1: 缺少 operationDescription");

            // 场景 2: 缺少 userName，但有 operationDescription
            var log2 = new ChangeAuditLog
            {
                LogId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
                Timestamp = new DateTime(2026, 5, 2, 10, 30, 0, DateTimeKind.Utc),
                OperationDescription = "操作描述",
                // userName 缺失
            };
            TestJsonValidity(log2, "场景 2: 缺少 userName");

            // 场景 3: 缺少 operationDescription 和 userName
            var log3 = new ChangeAuditLog
            {
                LogId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
                Timestamp = new DateTime(2026, 5, 2, 10, 30, 0, DateTimeKind.Utc),
                // 两个都缺失
            };
            TestJsonValidity(log3, "场景 3: 缺少 operationDescription 和 userName");

            // 场景 4: 有实体变更，但缺少可选属性
            var log4 = new ChangeAuditLog
            {
                LogId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
                Timestamp = new DateTime(2026, 5, 2, 10, 30, 0, DateTimeKind.Utc),
                EntityChanges = new List<EntityChangeEntry>
                {
                    new EntityChangeEntry
                    {
                        EntityName = "Test",
                        EntityTypeName = "Test",
                        Operation = EntityChangeOperation.Modified,
                        KeyValues = new Dictionary<string, object?>(),
                        // originalValues 缺失
                        // currentValues 缺失
                        PropertyChanges = new List<PropertyChangeEntry>
                        {
                            new PropertyChangeEntry
                            {
                                PropertyName = "A",
                                OriginalValue = 1,
                                NewValue = 2
                                // propertyType 缺失
                            }
                        }
                    }
                }
            };
            TestJsonValidity(log4, "场景 4: 实体缺少 originalValues/currentValues，属性缺少 propertyType");

            // 场景 5: 完整属性 + 空的可选字典
            var log5 = new ChangeAuditLog
            {
                LogId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
                Timestamp = new DateTime(2026, 5, 2, 10, 30, 0, DateTimeKind.Utc),
                OperationDescription = "测试",
                UserName = "user",
                CustomData = new Dictionary<string, object?>(), // 空字典
                EntityChanges = new List<EntityChangeEntry>
                {
                    new EntityChangeEntry
                    {
                        EntityName = "Test",
                        EntityTypeName = "Test",
                        Operation = EntityChangeOperation.Added,
                        KeyValues = new Dictionary<string, object?>(), // 空字典
                        CurrentValues = new Dictionary<string, object?>(), // 空字典
                        PropertyChanges = new List<PropertyChangeEntry>() // 空列表
                    }
                }
            };
            TestJsonValidity(log5, "场景 5: 空字典和空列表");
        }

        /// <summary>
        /// 辅助方法：测试 JSON 有效性
        /// </summary>
        private static void TestJsonValidity(ChangeAuditLog auditLog, string scenarioName)
        {
            try
            {
                string json = auditLog.ToJson();
                
                // 验证是有效的 JSON
                var parsed = JsonDocument.Parse(json);
                Assert.IsNotNull(parsed, $"{scenarioName}: JSON 解析失败");

                // 验证最后一个字符是 }
                string trimmed = json.Trim();
                Assert.AreEqual('}', trimmed[trimmed.Length - 1], $"{scenarioName}: 应该以 }} 结尾");

                // 验证没有尾随逗号
                var invalidPattern = new System.Text.RegularExpressions.Regex(@"(,|\])\s*\}");
                StringAssert.DoesNotMatch(json, invalidPattern, $"{scenarioName}: 不应该存在尾随逗号");
            }
            catch (Exception ex)
            {
                Assert.Fail($"{scenarioName}: 测试失败 - {ex.Message}");
            }
        }

        /// <summary>
        /// 测试 EntityChangeEntry 中各种可选属性缺失的情况
        /// </summary>
        [TestMethod]
        public void ToJson_EntityChangeEntryOptionalPropertiesMissing_CommasCorrect()
        {
            // 场景 1: EntityChangeEntry 只有必需属性
            var log1 = new ChangeAuditLog
            {
                LogId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
                Timestamp = new DateTime(2026, 5, 2, 10, 30, 0, DateTimeKind.Utc),
                EntityChanges = new List<EntityChangeEntry>
                {
                    new EntityChangeEntry
                    {
                        EntityTypeName = "Test.Entity",
                        EntityName = "Entity",
                        Operation = EntityChangeOperation.Modified
                        // KeyValues 为空字典
                        // PropertyChanges 为空列表
                        // CurrentValues 缺失
                        // OriginalValues 缺失
                    }
                }
            };
            TestJsonValidity(log1, "EntityChangeEntry: 只有必需属性");

            // 场景 2: EntityChangeEntry 有 KeyValues，但没有其他可选属性
            var log2 = new ChangeAuditLog
            {
                LogId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
                Timestamp = new DateTime(2026, 5, 2, 10, 30, 0, DateTimeKind.Utc),
                EntityChanges = new List<EntityChangeEntry>
                {
                    new EntityChangeEntry
                    {
                        EntityTypeName = "Test.Entity",
                        EntityName = "Entity",
                        Operation = EntityChangeOperation.Modified,
                        KeyValues = new Dictionary<string, object?> { { "Id", 1 } }
                        // 没有 PropertyChanges
                        // 没有 CurrentValues/OriginalValues
                    }
                }
            };
            TestJsonValidity(log2, "EntityChangeEntry: 有 KeyValues，无其他可选属性");

            // 场景 3: PropertyChangeEntry 缺少 PropertyType
            var log3 = new ChangeAuditLog
            {
                LogId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
                Timestamp = new DateTime(2026, 5, 2, 10, 30, 0, DateTimeKind.Utc),
                EntityChanges = new List<EntityChangeEntry>
                {
                    new EntityChangeEntry
                    {
                        EntityTypeName = "Test.Entity",
                        EntityName = "Entity",
                        Operation = EntityChangeOperation.Modified,
                        KeyValues = new Dictionary<string, object?> { { "Id", 1 } },
                        PropertyChanges = new List<PropertyChangeEntry>
                        {
                            new PropertyChangeEntry
                            {
                                PropertyName = "Name",
                                OriginalValue = "Old",
                                NewValue = "New"
                                // PropertyType 缺失
                            }
                        }
                    }
                }
            };
            TestJsonValidity(log3, "PropertyChangeEntry: 缺少 PropertyType");

            // 场景 4: 多个 PropertyChangeEntry，有些有 PropertyType，有些没有
            var log4 = new ChangeAuditLog
            {
                LogId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
                Timestamp = new DateTime(2026, 5, 2, 10, 30, 0, DateTimeKind.Utc),
                EntityChanges = new List<EntityChangeEntry>
                {
                    new EntityChangeEntry
                    {
                        EntityTypeName = "Test.Entity",
                        EntityName = "Entity",
                        Operation = EntityChangeOperation.Modified,
                        KeyValues = new Dictionary<string, object?> { { "Id", 1 } },
                        PropertyChanges = new List<PropertyChangeEntry>
                        {
                            new PropertyChangeEntry
                            {
                                PropertyName = "Name",
                                OriginalValue = "Old",
                                NewValue = "New",
                                PropertyType = "System.String"
                            },
                            new PropertyChangeEntry
                            {
                                PropertyName = "Age",
                                OriginalValue = 20,
                                NewValue = 30
                                // 没有 PropertyType
                            },
                            new PropertyChangeEntry
                            {
                                PropertyName = "Active",
                                OriginalValue = true,
                                NewValue = false,
                                PropertyType = "System.Boolean"
                            }
                        }
                    }
                }
            };
            TestJsonValidity(log4, "多个 PropertyChangeEntry: 混合有/无 PropertyType");
        }
    }
}
