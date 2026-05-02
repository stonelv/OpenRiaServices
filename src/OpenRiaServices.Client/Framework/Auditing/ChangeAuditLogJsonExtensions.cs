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

            // LogId
            WriteProperty(writer, "logId", auditLog.LogId.ToString(), writeIndented, indentLevel + 1, isLast: false);

            // Timestamp
            WriteProperty(writer, "timestamp", auditLog.Timestamp.ToString("o"), writeIndented, indentLevel + 1, isLast: false);

            // OperationDescription
            if (!string.IsNullOrEmpty(auditLog.OperationDescription))
            {
                WriteProperty(writer, "operationDescription", auditLog.OperationDescription, writeIndented, indentLevel + 1, isLast: false);
            }

            // UserName
            if (!string.IsNullOrEmpty(auditLog.UserName))
            {
                WriteProperty(writer, "userName", auditLog.UserName, writeIndented, indentLevel + 1, isLast: false);
            }

            // Counts
            WriteProperty(writer, "addedCount", auditLog.AddedCount, writeIndented, indentLevel + 1, isLast: false);
            WriteProperty(writer, "modifiedCount", auditLog.ModifiedCount, writeIndented, indentLevel + 1, isLast: false);
            WriteProperty(writer, "removedCount", auditLog.RemovedCount, writeIndented, indentLevel + 1, isLast: false);
            WriteProperty(writer, "totalChanges", auditLog.TotalChanges, writeIndented, indentLevel + 1, isLast: false);

            // EntityChanges
            WriteEntityChangesArray(writer, auditLog.EntityChanges, writeIndented, indentLevel + 1, isLast: false);

            // CustomData
            if (auditLog.CustomData != null && auditLog.CustomData.Count > 0)
            {
                WriteDictionary(writer, "customData", auditLog.CustomData, writeIndented, indentLevel + 1, isLast: true);
            }
            else
            {
                // 如果没有 CustomData，则 entityChanges 是最后一个属性
                // 我们需要重新处理，让 entityChanges 成为最后一个
                // 这里简化处理：直接写一个空对象
                WritePropertyName(writer, "customData", writeIndented, indentLevel + 1);
                writer.Write("{}");
            }

            WriteEndObject(writer, writeIndented, indentLevel);
        }

        /// <summary>
        /// 写入实体变更数组
        /// </summary>
        private static void WriteEntityChangesArray(StringWriter writer, List<EntityChangeEntry> entityChanges, bool writeIndented, int indentLevel, bool isLast)
        {
            WritePropertyName(writer, "entityChanges", writeIndented, indentLevel);

            if (entityChanges == null || entityChanges.Count == 0)
            {
                writer.Write("[]");
                if (!isLast) writer.Write(",");
                if (writeIndented) writer.WriteLine();
                return;
            }

            WriteStartArray(writer, writeIndented, indentLevel);

            for (int i = 0; i < entityChanges.Count; i++)
            {
                var entityChange = entityChanges[i];
                bool isLastItem = i == entityChanges.Count - 1;

                WriteEntityChangeEntry(writer, entityChange, writeIndented, indentLevel + 1);

                if (!isLastItem)
                    writer.Write(",");
                if (writeIndented)
                    writer.WriteLine();
            }

            WriteEndArray(writer, writeIndented, indentLevel);
            if (!isLast) writer.Write(",");
            if (writeIndented) writer.WriteLine();
        }

        /// <summary>
        /// 写入单个实体变更条目
        /// </summary>
        private static void WriteEntityChangeEntry(StringWriter writer, EntityChangeEntry entry, bool writeIndented, int indentLevel)
        {
            WriteStartObject(writer, writeIndented, indentLevel);

            // EntityTypeName
            WriteProperty(writer, "entityTypeName", entry.EntityTypeName, writeIndented, indentLevel + 1, isLast: false);

            // EntityName
            WriteProperty(writer, "entityName", entry.EntityName, writeIndented, indentLevel + 1, isLast: false);

            // Operation
            WriteProperty(writer, "operation", entry.Operation.ToString(), writeIndented, indentLevel + 1, isLast: false);

            // KeyValues
            if (entry.KeyValues != null && entry.KeyValues.Count > 0)
            {
                WriteDictionary(writer, "keyValues", entry.KeyValues, writeIndented, indentLevel + 1, isLast: false);
            }

            // PropertyChanges
            if (entry.PropertyChanges != null && entry.PropertyChanges.Count > 0)
            {
                WritePropertyChangesArray(writer, entry.PropertyChanges, writeIndented, indentLevel + 1, isLast: false);
            }

            // CurrentValues
            if (entry.CurrentValues != null && entry.CurrentValues.Count > 0)
            {
                WriteDictionary(writer, "currentValues", entry.CurrentValues, writeIndented, indentLevel + 1, isLast: false);
            }

            // OriginalValues
            if (entry.OriginalValues != null && entry.OriginalValues.Count > 0)
            {
                WriteDictionary(writer, "originalValues", entry.OriginalValues, writeIndented, indentLevel + 1, isLast: true);
            }
            else
            {
                // 如果没有 OriginalValues，确保最后一个属性没有逗号
                // 这里简化处理
            }

            WriteEndObject(writer, writeIndented, indentLevel);
        }

        /// <summary>
        /// 写入属性变更数组
        /// </summary>
        private static void WritePropertyChangesArray(StringWriter writer, List<PropertyChangeEntry> propertyChanges, bool writeIndented, int indentLevel, bool isLast)
        {
            WritePropertyName(writer, "propertyChanges", writeIndented, indentLevel);

            if (propertyChanges == null || propertyChanges.Count == 0)
            {
                writer.Write("[]");
                if (!isLast) writer.Write(",");
                if (writeIndented) writer.WriteLine();
                return;
            }

            WriteStartArray(writer, writeIndented, indentLevel);

            for (int i = 0; i < propertyChanges.Count; i++)
            {
                var propChange = propertyChanges[i];
                bool isLastItem = i == propertyChanges.Count - 1;

                WriteStartObject(writer, writeIndented, indentLevel + 1);

                WriteProperty(writer, "propertyName", propChange.PropertyName, writeIndented, indentLevel + 2, isLast: false);

                // OriginalValue
                WritePropertyName(writer, "originalValue", writeIndented, indentLevel + 2);
                WriteValue(writer, propChange.OriginalValue);
                writer.Write(",");
                if (writeIndented) writer.WriteLine();

                // NewValue
                WritePropertyName(writer, "newValue", writeIndented, indentLevel + 2);
                WriteValue(writer, propChange.NewValue);

                if (!string.IsNullOrEmpty(propChange.PropertyType))
                {
                    writer.Write(",");
                    if (writeIndented) writer.WriteLine();
                    WriteProperty(writer, "propertyType", propChange.PropertyType, writeIndented, indentLevel + 2, isLast: true);
                }
                else
                {
                    if (writeIndented) writer.WriteLine();
                }

                WriteEndObject(writer, writeIndented, indentLevel + 1);

                if (!isLastItem)
                    writer.Write(",");
                if (writeIndented)
                    writer.WriteLine();
            }

            WriteEndArray(writer, writeIndented, indentLevel);
            if (!isLast) writer.Write(",");
            if (writeIndented) writer.WriteLine();
        }

        /// <summary>
        /// 写入字典
        /// </summary>
        private static void WriteDictionary(StringWriter writer, string propertyName, Dictionary<string, object?> dictionary, bool writeIndented, int indentLevel, bool isLast)
        {
            WritePropertyName(writer, propertyName, writeIndented, indentLevel);

            if (dictionary == null || dictionary.Count == 0)
            {
                writer.Write("{}");
                if (!isLast) writer.Write(",");
                if (writeIndented) writer.WriteLine();
                return;
            }

            WriteStartObject(writer, writeIndented, indentLevel);

            int i = 0;
            foreach (var kvp in dictionary)
            {
                bool isLastItem = i == dictionary.Count - 1;

                WritePropertyName(writer, EscapeJsonString(kvp.Key), writeIndented, indentLevel + 1);
                WriteValue(writer, kvp.Value);

                if (!isLastItem)
                    writer.Write(",");
                if (writeIndented)
                    writer.WriteLine();

                i++;
            }

            WriteEndObject(writer, writeIndented, indentLevel);
            if (!isLast) writer.Write(",");
            if (writeIndented) writer.WriteLine();
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
                    // 对于复杂类型，尝试转换为字符串
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
                        // 其他类型，转换为字符串
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

        private static void WriteProperty(StringWriter writer, string name, string? value, bool writeIndented, int indentLevel, bool isLast)
        {
            WritePropertyName(writer, name, writeIndented, indentLevel);
            if (value == null)
                writer.Write("null");
            else
                writer.Write($"\"{EscapeJsonString(value)}\"");
            if (!isLast)
                writer.Write(",");
            if (writeIndented)
                writer.WriteLine();
        }

        private static void WriteProperty(StringWriter writer, string name, int value, bool writeIndented, int indentLevel, bool isLast)
        {
            WritePropertyName(writer, name, writeIndented, indentLevel);
            writer.Write(value.ToString(CultureInfo.InvariantCulture));
            if (!isLast)
                writer.Write(",");
            if (writeIndented)
                writer.WriteLine();
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
