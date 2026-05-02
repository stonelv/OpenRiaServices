using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization.Json;
using System.Text;

#nullable enable

namespace OpenRiaServices.Client.Auditing
{
    /// <summary>
    /// 提供 ChangeAuditLog 对象的 JSON 序列化扩展方法
    /// </summary>
    public static class ChangeAuditLogJsonExtensions
    {
        /// <summary>
        /// 将审计日志对象序列化为 JSON 字符串
        /// </summary>
        /// <param name="auditLog">审计日志对象</param>
        /// <returns>JSON 字符串</returns>
        public static string ToJson(this ChangeAuditLog auditLog)
        {
            return ToJson(auditLog, writeIndented: false);
        }

        /// <summary>
        /// 将审计日志对象序列化为 JSON 字符串
        /// </summary>
        /// <param name="auditLog">审计日志对象</param>
        /// <param name="writeIndented">是否格式化输出（缩进）</param>
        /// <returns>JSON 字符串</returns>
        public static string ToJson(this ChangeAuditLog auditLog, bool writeIndented)
        {
            if (auditLog == null)
                return "null";

            var sb = new StringBuilder();
            using (var writer = new StringWriter(sb))
            {
                WriteAuditLog(writer, auditLog, writeIndented, 0);
            }
            return sb.ToString();
        }

        /// <summary>
        /// 写入审计日志对象
        /// </summary>
        private static void WriteAuditLog(StringWriter writer, ChangeAuditLog auditLog, bool writeIndented, int indentLevel)
        {
            WriteStartObject(writer, writeIndented, indentLevel);

            bool hasPrevious = false;

            // LogId
            WriteStringProperty(writer, "logId", auditLog.LogId.ToString(), writeIndented, indentLevel + 1, ref hasPrevious);

            // Timestamp
            WriteStringProperty(writer, "timestamp", auditLog.Timestamp.ToString("o"), writeIndented, indentLevel + 1, ref hasPrevious);

            // OperationDescription (可选)
            if (!string.IsNullOrEmpty(auditLog.OperationDescription))
            {
                WriteStringProperty(writer, "operationDescription", auditLog.OperationDescription, writeIndented, indentLevel + 1, ref hasPrevious);
            }

            // UserName (可选)
            if (!string.IsNullOrEmpty(auditLog.UserName))
            {
                WriteStringProperty(writer, "userName", auditLog.UserName, writeIndented, indentLevel + 1, ref hasPrevious);
            }

            // Counts
            WriteIntProperty(writer, "addedCount", auditLog.AddedCount, writeIndented, indentLevel + 1, ref hasPrevious);
            WriteIntProperty(writer, "modifiedCount", auditLog.ModifiedCount, writeIndented, indentLevel + 1, ref hasPrevious);
            WriteIntProperty(writer, "removedCount", auditLog.RemovedCount, writeIndented, indentLevel + 1, ref hasPrevious);
            WriteIntProperty(writer, "totalChanges", auditLog.TotalChanges, writeIndented, indentLevel + 1, ref hasPrevious);

            // EntityChanges
            WriteEntityChangesArray(writer, auditLog.EntityChanges, writeIndented, indentLevel + 1, ref hasPrevious);

            // CustomData (可选)
            if (auditLog.CustomData != null && auditLog.CustomData.Count > 0)
            {
                WriteDictionaryProperty(writer, "customData", auditLog.CustomData, writeIndented, indentLevel + 1, ref hasPrevious);
            }

            if (writeIndented)
                writer.WriteLine();
            WriteEndObject(writer, writeIndented, indentLevel);
        }

        /// <summary>
        /// 写入字符串属性，带逗号控制
        /// </summary>
        private static void WriteStringProperty(StringWriter writer, string name, string? value, bool writeIndented, int indentLevel, ref bool hasPrevious)
        {
            WritePropertyPrefix(writer, writeIndented, indentLevel, ref hasPrevious);
            WritePropertyName(writer, name, writeIndented, indentLevel);
            if (value == null)
                writer.Write("null");
            else
                writer.Write($"\"{EscapeJsonString(value)}\"");
        }

        /// <summary>
        /// 写入整数属性，带逗号控制
        /// </summary>
        private static void WriteIntProperty(StringWriter writer, string name, int value, bool writeIndented, int indentLevel, ref bool hasPrevious)
        {
            WritePropertyPrefix(writer, writeIndented, indentLevel, ref hasPrevious);
            WritePropertyName(writer, name, writeIndented, indentLevel);
            writer.Write(value.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// 写入实体变更数组属性
        /// </summary>
        private static void WriteEntityChangesArray(StringWriter writer, List<EntityChangeEntry>? entityChanges, bool writeIndented, int indentLevel, ref bool hasPrevious)
        {
            WritePropertyPrefix(writer, writeIndented, indentLevel, ref hasPrevious);
            WritePropertyName(writer, "entityChanges", writeIndented, indentLevel);

            if (entityChanges == null || entityChanges.Count == 0)
            {
                writer.Write("[]");
                return;
            }

            WriteStartArray(writer, writeIndented, indentLevel);

            bool hasItem = false;
            for (int i = 0; i < entityChanges.Count; i++)
            {
                var entityChange = entityChanges[i];
                WriteItemPrefix(writer, writeIndented, indentLevel + 1, ref hasItem);
                WriteEntityChangeEntry(writer, entityChange, writeIndented, indentLevel + 1);
            }

            if (writeIndented && hasItem)
                writer.WriteLine();
            WriteEndArray(writer, writeIndented, indentLevel);
        }

        /// <summary>
        /// 写入字典属性
        /// </summary>
        private static void WriteDictionaryProperty(StringWriter writer, string propertyName, Dictionary<string, object?>? dictionary, bool writeIndented, int indentLevel, ref bool hasPrevious)
        {
            WritePropertyPrefix(writer, writeIndented, indentLevel, ref hasPrevious);
            WritePropertyName(writer, propertyName, writeIndented, indentLevel);

            if (dictionary == null || dictionary.Count == 0)
            {
                writer.Write("{}");
                return;
            }

            WriteDictionaryValue(writer, dictionary, writeIndented, indentLevel);
        }

        /// <summary>
        /// 写入字典值（不带属性名前缀）
        /// </summary>
        private static void WriteDictionaryValue(StringWriter writer, Dictionary<string, object?> dictionary, bool writeIndented, int indentLevel)
        {
            WriteStartObject(writer, writeIndented, indentLevel);

            bool hasItem = false;
            foreach (var kvp in dictionary)
            {
                WriteItemPrefix(writer, writeIndented, indentLevel + 1, ref hasItem);
                WritePropertyName(writer, EscapeJsonString(kvp.Key), writeIndented, indentLevel + 1);
                WriteValue(writer, kvp.Value);
            }

            if (writeIndented && hasItem)
                writer.WriteLine();
            WriteEndObject(writer, writeIndented, indentLevel);
        }

        /// <summary>
        /// 写入单个实体变更条目
        /// </summary>
        private static void WriteEntityChangeEntry(StringWriter writer, EntityChangeEntry entry, bool writeIndented, int indentLevel)
        {
            WriteStartObject(writer, writeIndented, indentLevel);

            bool hasPrevious = false;

            // EntityTypeName
            WriteStringProperty(writer, "entityTypeName", entry.EntityTypeName, writeIndented, indentLevel + 1, ref hasPrevious);

            // EntityName
            WriteStringProperty(writer, "entityName", entry.EntityName, writeIndented, indentLevel + 1, ref hasPrevious);

            // Operation
            WriteStringProperty(writer, "operation", entry.Operation.ToString(), writeIndented, indentLevel + 1, ref hasPrevious);

            // KeyValues
            if (entry.KeyValues != null && entry.KeyValues.Count > 0)
            {
                WriteDictionaryProperty(writer, "keyValues", entry.KeyValues, writeIndented, indentLevel + 1, ref hasPrevious);
            }

            // PropertyChanges
            if (entry.PropertyChanges != null && entry.PropertyChanges.Count > 0)
            {
                WritePropertyChangesArray(writer, entry.PropertyChanges, writeIndented, indentLevel + 1, ref hasPrevious);
            }

            // CurrentValues
            if (entry.CurrentValues != null && entry.CurrentValues.Count > 0)
            {
                WriteDictionaryProperty(writer, "currentValues", entry.CurrentValues, writeIndented, indentLevel + 1, ref hasPrevious);
            }

            // OriginalValues
            if (entry.OriginalValues != null && entry.OriginalValues.Count > 0)
            {
                WriteDictionaryProperty(writer, "originalValues", entry.OriginalValues, writeIndented, indentLevel + 1, ref hasPrevious);
            }

            if (writeIndented && hasPrevious)
                writer.WriteLine();
            WriteEndObject(writer, writeIndented, indentLevel);
        }

        /// <summary>
        /// 写入属性变更数组
        /// </summary>
        private static void WritePropertyChangesArray(StringWriter writer, List<PropertyChangeEntry> propertyChanges, bool writeIndented, int indentLevel, ref bool hasPrevious)
        {
            WritePropertyPrefix(writer, writeIndented, indentLevel, ref hasPrevious);
            WritePropertyName(writer, "propertyChanges", writeIndented, indentLevel);

            if (propertyChanges == null || propertyChanges.Count == 0)
            {
                writer.Write("[]");
                return;
            }

            WriteStartArray(writer, writeIndented, indentLevel);

            bool hasItem = false;
            for (int i = 0; i < propertyChanges.Count; i++)
            {
                var propChange = propertyChanges[i];
                WriteItemPrefix(writer, writeIndented, indentLevel + 1, ref hasItem);
                WritePropertyChangeEntry(writer, propChange, writeIndented, indentLevel + 1);
            }

            if (writeIndented && hasItem)
                writer.WriteLine();
            WriteEndArray(writer, writeIndented, indentLevel);
        }

        /// <summary>
        /// 写入单个属性变更条目
        /// </summary>
        private static void WritePropertyChangeEntry(StringWriter writer, PropertyChangeEntry entry, bool writeIndented, int indentLevel)
        {
            WriteStartObject(writer, writeIndented, indentLevel);

            bool hasPrevious = false;

            // PropertyName
            WriteStringProperty(writer, "propertyName", entry.PropertyName, writeIndented, indentLevel + 1, ref hasPrevious);

            // OriginalValue
            WritePropertyPrefix(writer, writeIndented, indentLevel + 1, ref hasPrevious);
            WritePropertyName(writer, "originalValue", writeIndented, indentLevel + 1);
            WriteValue(writer, entry.OriginalValue);

            // NewValue
            WritePropertyPrefix(writer, writeIndented, indentLevel + 1, ref hasPrevious);
            WritePropertyName(writer, "newValue", writeIndented, indentLevel + 1);
            WriteValue(writer, entry.NewValue);

            // PropertyType (可选)
            if (!string.IsNullOrEmpty(entry.PropertyType))
            {
                WriteStringProperty(writer, "propertyType", entry.PropertyType, writeIndented, indentLevel + 1, ref hasPrevious);
            }

            if (writeIndented && hasPrevious)
                writer.WriteLine();
            WriteEndObject(writer, writeIndented, indentLevel);
        }

        /// <summary>
        /// 写入属性前缀（逗号和换行）
        /// </summary>
        private static void WritePropertyPrefix(StringWriter writer, bool writeIndented, int indentLevel, ref bool hasPrevious)
        {
            if (hasPrevious)
            {
                writer.Write(",");
                if (writeIndented)
                    writer.WriteLine();
            }
            hasPrevious = true;
        }

        /// <summary>
        /// 写入数组成员前缀（逗号和换行）
        /// </summary>
        private static void WriteItemPrefix(StringWriter writer, bool writeIndented, int indentLevel, ref bool hasPrevious)
        {
            if (hasPrevious)
            {
                writer.Write(",");
                if (writeIndented)
                    writer.WriteLine();
            }
            hasPrevious = true;
        }

        /// <summary>
        /// 写入属性值
        /// </summary>
        private static void WriteValue(StringWriter writer, object? value)
        {
            if (value == null)
            {
                writer.Write("null");
                return;
            }

            var typeCode = Type.GetTypeCode(value.GetType());
            switch (typeCode)
            {
                case TypeCode.Boolean:
                    writer.Write(((bool)value).ToString().ToLowerInvariant());
                    break;
                case TypeCode.Char:
                    writer.Write($"\"{EscapeJsonString(value.ToString()!)}\"");
                    break;
                case TypeCode.SByte:
                case TypeCode.Byte:
                case TypeCode.Int16:
                case TypeCode.UInt16:
                case TypeCode.Int32:
                case TypeCode.UInt32:
                case TypeCode.Int64:
                case TypeCode.UInt64:
                case TypeCode.Single:
                case TypeCode.Double:
                case TypeCode.Decimal:
                    writer.Write(Convert.ToString(value, CultureInfo.InvariantCulture));
                    break;
                case TypeCode.DateTime:
                    writer.Write($"\"{((DateTime)value).ToString("o")}\"");
                    break;
                case TypeCode.String:
                    writer.Write($"\"{EscapeJsonString((string)value)}\"");
                    break;
                default:
                    if (value is Guid guid)
                    {
                        writer.Write($"\"{guid}\"");
                    }
                    else if (value is DateTimeOffset dto)
                    {
                        writer.Write($"\"{dto.ToString("o")}\"");
                    }
                    else if (value is TimeSpan ts)
                    {
                        writer.Write($"\"{ts}\"");
                    }
                    else
                    {
                        writer.Write($"\"{EscapeJsonString(value.ToString()!)}\"");
                    }
                    break;
            }
        }

        /// <summary>
        /// 转义 JSON 字符串
        /// </summary>
        private static string EscapeJsonString(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var sb = new StringBuilder(value.Length);
            foreach (char c in value)
            {
                switch (c)
                {
                    case '\"':
                        sb.Append("\\\"");
                        break;
                    case '\\':
                        sb.Append("\\\\");
                        break;
                    case '\b':
                        sb.Append("\\b");
                        break;
                    case '\f':
                        sb.Append("\\f");
                        break;
                    case '\n':
                        sb.Append("\\n");
                        break;
                    case '\r':
                        sb.Append("\\r");
                        break;
                    case '\t':
                        sb.Append("\\t");
                        break;
                    default:
                        if (char.IsControl(c))
                        {
                            sb.Append(string.Format(CultureInfo.InvariantCulture, "\\u{0:X4}", (int)c));
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            return sb.ToString();
        }

        #region 辅助方法

        private static void WriteStartObject(StringWriter writer, bool writeIndented, int indentLevel)
        {
            if (writeIndented)
                writer.Write(new string(' ', indentLevel * 2));
            writer.Write("{");
            if (writeIndented)
                writer.WriteLine();
        }

        private static void WriteEndObject(StringWriter writer, bool writeIndented, int indentLevel)
        {
            if (writeIndented)
                writer.Write(new string(' ', indentLevel * 2));
            writer.Write("}");
        }

        private static void WriteStartArray(StringWriter writer, bool writeIndented, int indentLevel)
        {
            if (writeIndented)
                writer.Write(new string(' ', indentLevel * 2));
            writer.Write("[");
            if (writeIndented)
                writer.WriteLine();
        }

        private static void WriteEndArray(StringWriter writer, bool writeIndented, int indentLevel)
        {
            if (writeIndented)
                writer.Write(new string(' ', indentLevel * 2));
            writer.Write("]");
        }

        private static void WritePropertyName(StringWriter writer, string name, bool writeIndented, int indentLevel)
        {
            if (writeIndented)
                writer.Write(new string(' ', indentLevel * 2));
            writer.Write($"\"{name}\":");
            if (writeIndented)
                writer.Write(" ");
        }

        #endregion

        #region 使用 DataContractJsonSerializer 的备用方法（用于复杂场景）

        /// <summary>
        /// 使用 DataContractJsonSerializer 序列化为 JSON
        /// 注意：此方法可能无法正确处理所有类型（如 Dictionary）
        /// </summary>
        public static string ToJsonUsingDataContractSerializer(this ChangeAuditLog auditLog)
        {
            if (auditLog == null)
                return "null";

            var serializer = new DataContractJsonSerializer(typeof(ChangeAuditLog));
            using (var ms = new MemoryStream())
            {
                serializer.WriteObject(ms, auditLog);
                ms.Position = 0;
                using (var reader = new StreamReader(ms, Encoding.UTF8))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        #endregion
    }
}
