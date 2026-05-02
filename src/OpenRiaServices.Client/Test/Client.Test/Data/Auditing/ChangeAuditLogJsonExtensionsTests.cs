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
    }
}
