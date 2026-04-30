#if NET8_0_OR_GREATER
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using OpenRiaServices.Client.Internal;

namespace OpenRiaServices.Client.DomainClients.Http
{
    internal static class JsonSerializationHelper
    {
        private static readonly ConcurrentDictionary<Type, JsonSerializerOptions> s_optionsCache = new();
        private static readonly ConcurrentDictionary<Type, PropertyInfo> s_isDeserializingPropertyCache = new();
        private static readonly ConcurrentDictionary<Type, Dictionary<string, DataMemberProperty>> s_dataMemberCache = new();

        public static JsonSerializerOptions GetSerializerOptions(Type serviceInterface, IEnumerable<Type> entityTypes)
        {
            return s_optionsCache.GetOrAdd(serviceInterface, _ =>
            {
                var resolver = new DataContractJsonTypeInfoResolver(entityTypes);
                var options = new JsonSerializerOptions
                {
                    TypeInfoResolver = resolver,
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = null,
                    DefaultIgnoreCondition = JsonIgnoreCondition.Never,
                    ReferenceHandler = ReferenceHandler.IgnoreCycles,
                };

                options.Converters.Add(new EntityJsonConverterFactory());
                options.Converters.Add(new ChangeSetEntryJsonConverter());
                options.Converters.Add(new QueryResultJsonConverterFactory());
                options.Converters.Add(new DomainServiceFaultJsonConverter());
                options.Converters.Add(new KeyValueJsonConverterFactory());

                return options;
            });
        }

        public static JsonSerializerOptions GetSerializerOptions(Type serviceInterface)
        {
            return GetSerializerOptions(serviceInterface, Enumerable.Empty<Type>());
        }

        internal static void SetIsDeserializing(Entity entity, bool value)
        {
            var prop = s_isDeserializingPropertyCache.GetOrAdd(entity.GetType(), t =>
                typeof(Entity).GetProperty("IsDeserializing", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public));
            
            prop?.SetValue(entity, value);
        }

        internal static Dictionary<string, DataMemberProperty> GetDataMembers(Type type)
        {
            return s_dataMemberCache.GetOrAdd(type, t =>
            {
                var result = new Dictionary<string, DataMemberProperty>(StringComparer.OrdinalIgnoreCase);
                var metaType = MetaType.GetMetaType(t);

                foreach (var member in metaType.DataMembers)
                {
                    var propInfo = t.GetProperty(member.Name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (propInfo != null)
                    {
                        var dataMemberAttr = propInfo.GetCustomAttribute<DataMemberAttribute>();
                        if (dataMemberAttr != null)
                        {
                            var name = dataMemberAttr.Name ?? member.Name;
                            result[name] = new DataMemberProperty
                            {
                                MetaMember = member,
                                PropertyInfo = propInfo,
                                DataMemberAttribute = dataMemberAttr
                            };
                        }
                    }
                }

                return result;
            });
        }
    }

    internal class DataMemberProperty
    {
        public MetaMember MetaMember { get; set; }
        public PropertyInfo PropertyInfo { get; set; }
        public DataMemberAttribute DataMemberAttribute { get; set; }
    }

    internal class DataContractJsonTypeInfoResolver : DefaultJsonTypeInfoResolver
    {
        private readonly HashSet<Type> _knownTypes;

        public DataContractJsonTypeInfoResolver(IEnumerable<Type> knownTypes)
        {
            _knownTypes = new HashSet<Type>(knownTypes);
        }

        public override JsonTypeInfo GetTypeInfo(Type type, JsonSerializerOptions options)
        {
            var typeInfo = base.GetTypeInfo(type, options);

            if (typeInfo.Kind == JsonTypeInfoKind.Object)
            {
                var dataContractAttr = type.GetCustomAttribute<DataContractAttribute>();
                if (dataContractAttr != null && !typeof(Entity).IsAssignableFrom(type))
                {
                    ModifyTypeInfoForDataContract(typeInfo, dataContractAttr, options);
                }
            }

            return typeInfo;
        }

        private static void ModifyTypeInfoForDataContract(JsonTypeInfo typeInfo, DataContractAttribute dataContractAttr, JsonSerializerOptions options)
        {
            var type = typeInfo.Type;
            var dataMembers = new Dictionary<string, PropertyInfo>();
            var allProperties = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            foreach (var prop in allProperties)
            {
                var dataMemberAttr = prop.GetCustomAttribute<DataMemberAttribute>();
                if (dataMemberAttr != null)
                {
                    var name = dataMemberAttr.Name ?? prop.Name;
                    dataMembers[name] = prop;
                }
            }

            var propertiesToRemove = new List<JsonPropertyInfo>();
            foreach (var prop in typeInfo.Properties)
            {
                if (!dataMembers.ContainsKey(prop.Name))
                {
                    propertiesToRemove.Add(prop);
                }
            }

            foreach (var prop in propertiesToRemove)
            {
                typeInfo.Properties.Remove(prop);
            }

            foreach (var kvp in dataMembers)
            {
                var jsonName = kvp.Key;
                var propInfo = kvp.Value;
                var dataMemberAttr = propInfo.GetCustomAttribute<DataMemberAttribute>();

                var existingProp = typeInfo.Properties.FirstOrDefault(p => p.Name == jsonName);
                if (existingProp != null)
                {
                    existingProp.Name = jsonName;
                    existingProp.ShouldSerialize = dataMemberAttr.EmitDefaultValue 
                        ? null 
                        : (obj, value) => value != null && !IsDefault(value, propInfo.PropertyType);
                }
                else if (propInfo.CanRead && propInfo.CanWrite)
                {
                    var jsonProp = typeInfo.CreateJsonPropertyInfo(propInfo.PropertyType, jsonName);
                    jsonProp.Get = CreateGetter(propInfo);
                    jsonProp.Set = CreateSetter(propInfo);
                    jsonProp.ShouldSerialize = dataMemberAttr.EmitDefaultValue 
                        ? null 
                        : (obj, value) => value != null && !IsDefault(value, propInfo.PropertyType);
                    
                    typeInfo.Properties.Add(jsonProp);
                }
            }
        }

        private static Func<object, object> CreateGetter(PropertyInfo prop)
        {
            return obj => prop.GetValue(obj);
        }

        private static Action<object, object> CreateSetter(PropertyInfo prop)
        {
            return (obj, value) => prop.SetValue(obj, value);
        }

        private static bool IsDefault(object value, Type type)
        {
            if (value == null) return true;
            if (type.IsValueType)
            {
                return EqualityComparer<object>.Default.Equals(value, Activator.CreateInstance(type));
            }
            return false;
        }
    }

    internal class EntityJsonConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert)
        {
            return typeof(Entity).IsAssignableFrom(typeToConvert);
        }

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        {
            var converterType = typeof(EntityJsonConverter<>).MakeGenericType(typeToConvert);
            return (JsonConverter)Activator.CreateInstance(converterType);
        }
    }

    internal class EntityJsonConverter<T> : JsonConverter<T> where T : Entity
    {
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException();
            }

            var entity = (T)Activator.CreateInstance(typeToConvert, nonPublic: true);
            JsonSerializationHelper.SetIsDeserializing(entity, true);

            try
            {
                var dataMembers = JsonSerializationHelper.GetDataMembers(typeToConvert);

                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndObject)
                    {
                        break;
                    }

                    if (reader.TokenType == JsonTokenType.PropertyName)
                    {
                        var propertyName = reader.GetString();
                        reader.Read();

                        if (dataMembers.TryGetValue(propertyName, out var dataMember))
                        {
                            var value = JsonSerializer.Deserialize(ref reader, dataMember.MetaMember.PropertyType, options);
                            dataMember.MetaMember.SetValue(entity, value);
                        }
                        else
                        {
                            reader.Skip();
                        }
                    }
                }
            }
            finally
            {
                JsonSerializationHelper.SetIsDeserializing(entity, false);
            }

            return entity;
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            var dataMembers = JsonSerializationHelper.GetDataMembers(typeof(T));
            
            foreach (var kvp in dataMembers)
            {
                var dataMember = kvp.Value;
                var memberValue = dataMember.MetaMember.GetValue(value);
                var name = dataMember.DataMemberAttribute.Name ?? dataMember.MetaMember.Name;

                if (!dataMember.DataMemberAttribute.EmitDefaultValue && IsDefaultValue(memberValue, dataMember.MetaMember.PropertyType))
                {
                    continue;
                }

                writer.WritePropertyName(name);
                JsonSerializer.Serialize(writer, memberValue, dataMember.MetaMember.PropertyType, options);
            }

            writer.WriteEndObject();
        }

        private static bool IsDefaultValue(object value, Type type)
        {
            if (value == null) return true;
            if (type.IsValueType)
            {
                return EqualityComparer<object>.Default.Equals(value, Activator.CreateInstance(type));
            }
            return false;
        }
    }

    internal class ChangeSetEntryJsonConverter : JsonConverter<ChangeSetEntry>
    {
        public override ChangeSetEntry Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException();
            }

            int id = 0;
            Entity entity = null;
            Entity originalEntity = null;
            Entity storeEntity = null;
            EntityOperationType operation = EntityOperationType.None;
            bool hasMemberChanges = false;
            IList<OpenRiaServices.Serialization.KeyValue<string, object[]>> entityActions = null;
            IEnumerable<ValidationResultInfo> validationErrors = null;
            IEnumerable<string> conflictMembers = null;
            bool isDeleteConflict = false;
            IDictionary<string, int[]> associations = null;
            IDictionary<string, int[]> originalAssociations = null;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    break;
                }

                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    var propertyName = reader.GetString();
                    reader.Read();

                    switch (propertyName)
                    {
                        case "Id":
                            id = reader.GetInt32();
                            break;
                        case "Entity":
                            entity = JsonSerializer.Deserialize<Entity>(ref reader, options);
                            break;
                        case "OriginalEntity":
                            originalEntity = JsonSerializer.Deserialize<Entity>(ref reader, options);
                            break;
                        case "StoreEntity":
                            storeEntity = JsonSerializer.Deserialize<Entity>(ref reader, options);
                            break;
                        case "Operation":
                            operation = JsonSerializer.Deserialize<EntityOperationType>(ref reader, options);
                            break;
                        case "HasMemberChanges":
                            hasMemberChanges = reader.GetBoolean();
                            break;
                        case "EntityActions":
                            entityActions = JsonSerializer.Deserialize<IList<OpenRiaServices.Serialization.KeyValue<string, object[]>>>(ref reader, options);
                            break;
                        case "ValidationErrors":
                            validationErrors = JsonSerializer.Deserialize<IEnumerable<ValidationResultInfo>>(ref reader, options);
                            break;
                        case "ConflictMembers":
                            conflictMembers = JsonSerializer.Deserialize<IEnumerable<string>>(ref reader, options);
                            break;
                        case "IsDeleteConflict":
                            isDeleteConflict = reader.GetBoolean();
                            break;
                        case "Associations":
                            associations = JsonSerializer.Deserialize<IDictionary<string, int[]>>(ref reader, options);
                            break;
                        case "OriginalAssociations":
                            originalAssociations = JsonSerializer.Deserialize<IDictionary<string, int[]>>(ref reader, options);
                            break;
                        default:
                            reader.Skip();
                            break;
                    }
                }
            }

            var entry = new ChangeSetEntry(entity, id, operation);
            if (originalEntity != null) entry.OriginalEntity = originalEntity;
            if (storeEntity != null) entry.StoreEntity = storeEntity;
            entry.HasMemberChanges = hasMemberChanges;
            if (entityActions != null) entry.EntityActions = entityActions;
            if (validationErrors != null) entry.ValidationErrors = validationErrors;
            if (conflictMembers != null) entry.ConflictMembers = conflictMembers;
            entry.IsDeleteConflict = isDeleteConflict;
            if (associations != null) entry.Associations = associations;
            if (originalAssociations != null) entry.OriginalAssociations = originalAssociations;

            return entry;
        }

        public override void Write(Utf8JsonWriter writer, ChangeSetEntry value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            writer.WriteNumber("Id", value.Id);
            writer.WritePropertyName("Entity");
            JsonSerializer.Serialize(writer, value.Entity, options);

            if (value.OriginalEntity != null)
            {
                writer.WritePropertyName("OriginalEntity");
                JsonSerializer.Serialize(writer, value.OriginalEntity, options);
            }

            if (value.StoreEntity != null)
            {
                writer.WritePropertyName("StoreEntity");
                JsonSerializer.Serialize(writer, value.StoreEntity, options);
            }

            writer.WriteNumber("Operation", (int)value.Operation);
            writer.WriteBoolean("HasMemberChanges", value.HasMemberChanges);

            if (value.EntityActions != null)
            {
                writer.WritePropertyName("EntityActions");
                JsonSerializer.Serialize(writer, value.EntityActions, options);
            }

            if (value.ValidationErrors != null)
            {
                writer.WritePropertyName("ValidationErrors");
                JsonSerializer.Serialize(writer, value.ValidationErrors, options);
            }

            if (value.ConflictMembers != null)
            {
                writer.WritePropertyName("ConflictMembers");
                JsonSerializer.Serialize(writer, value.ConflictMembers, options);
            }

            writer.WriteBoolean("IsDeleteConflict", value.IsDeleteConflict);

            if (value.Associations != null)
            {
                writer.WritePropertyName("Associations");
                JsonSerializer.Serialize(writer, value.Associations, options);
            }

            if (value.OriginalAssociations != null)
            {
                writer.WritePropertyName("OriginalAssociations");
                JsonSerializer.Serialize(writer, value.OriginalAssociations, options);
            }

            writer.WriteEndObject();
        }
    }

    internal class QueryResultJsonConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert)
        {
            return typeToConvert == typeof(QueryResult) || 
                   (typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(QueryResult<>));
        }

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        {
            if (typeToConvert == typeof(QueryResult))
            {
                return new QueryResultJsonConverter();
            }

            var elementType = typeToConvert.GetGenericArguments()[0];
            var converterType = typeof(QueryResultJsonConverter<>).MakeGenericType(elementType);
            return (JsonConverter)Activator.CreateInstance(converterType);
        }
    }

    internal class QueryResultJsonConverter : JsonConverter<QueryResult>
    {
        public override QueryResult Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            throw new NotImplementedException("Cannot deserialize to non-generic QueryResult");
        }

        public override void Write(Utf8JsonWriter writer, QueryResult value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteNumber("TotalCount", value.TotalCount);
            writer.WriteEndObject();
        }
    }

    internal class QueryResultJsonConverter<T> : JsonConverter<QueryResult<T>>
    {
        public override QueryResult<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException();
            }

            int totalCount = 0;
            IEnumerable<T> rootResults = null;
            IEnumerable<object> includedResults = null;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    break;
                }

                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    var propertyName = reader.GetString();
                    reader.Read();

                    switch (propertyName)
                    {
                        case "TotalCount":
                            totalCount = reader.GetInt32();
                            break;
                        case "RootResults":
                            rootResults = JsonSerializer.Deserialize<IEnumerable<T>>(ref reader, options);
                            break;
                        case "IncludedResults":
                            includedResults = JsonSerializer.Deserialize<IEnumerable<object>>(ref reader, options);
                            break;
                        default:
                            reader.Skip();
                            break;
                    }
                }
            }

            return new QueryResult<T>(rootResults, totalCount)
            {
                IncludedResults = includedResults
            };
        }

        public override void Write(Utf8JsonWriter writer, QueryResult<T> value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteNumber("TotalCount", value.TotalCount);

            if (value.RootResults != null)
            {
                writer.WritePropertyName("RootResults");
                JsonSerializer.Serialize(writer, value.RootResults, options);
            }

            if (value.IncludedResults != null)
            {
                writer.WritePropertyName("IncludedResults");
                JsonSerializer.Serialize(writer, value.IncludedResults, options);
            }

            writer.WriteEndObject();
        }
    }

    internal class DomainServiceFaultJsonConverter : JsonConverter<DomainServiceFault>
    {
        public override DomainServiceFault Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException();
            }

            var fault = new DomainServiceFault();

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    break;
                }

                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    var propertyName = reader.GetString();
                    reader.Read();

                    switch (propertyName)
                    {
                        case "ErrorCode":
                            fault.ErrorCode = reader.GetInt32();
                            break;
                        case "ErrorMessage":
                            fault.ErrorMessage = reader.GetString();
                            break;
                        case "IsDomainException":
                            fault.IsDomainException = reader.GetBoolean();
                            break;
                        case "StackTrace":
                            fault.StackTrace = reader.GetString();
                            break;
                        case "OperationErrors":
                            fault.OperationErrors = JsonSerializer.Deserialize<IEnumerable<ValidationResultInfo>>(ref reader, options);
                            break;
                        default:
                            reader.Skip();
                            break;
                    }
                }
            }

            return fault;
        }

        public override void Write(Utf8JsonWriter writer, DomainServiceFault value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteNumber("ErrorCode", value.ErrorCode);
            writer.WriteString("ErrorMessage", value.ErrorMessage);
            writer.WriteBoolean("IsDomainException", value.IsDomainException);
            
            if (value.StackTrace != null)
            {
                writer.WriteString("StackTrace", value.StackTrace);
            }

            if (value.OperationErrors != null)
            {
                writer.WritePropertyName("OperationErrors");
                JsonSerializer.Serialize(writer, value.OperationErrors, options);
            }

            writer.WriteEndObject();
        }
    }

    internal class KeyValueJsonConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert)
        {
            return typeToConvert.IsGenericType && 
                   typeToConvert.GetGenericTypeDefinition() == typeof(OpenRiaServices.Serialization.KeyValue<,>);
        }

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        {
            var args = typeToConvert.GetGenericArguments();
            var converterType = typeof(KeyValueJsonConverter<,>).MakeGenericType(args[0], args[1]);
            return (JsonConverter)Activator.CreateInstance(converterType);
        }
    }

    internal class KeyValueJsonConverter<TKey, TValue> : JsonConverter<OpenRiaServices.Serialization.KeyValue<TKey, TValue>>
    {
        public override OpenRiaServices.Serialization.KeyValue<TKey, TValue> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException();
            }

            TKey key = default;
            TValue value = default;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    break;
                }

                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    var propertyName = reader.GetString();
                    reader.Read();

                    switch (propertyName)
                    {
                        case "Key":
                            key = JsonSerializer.Deserialize<TKey>(ref reader, options);
                            break;
                        case "Value":
                            value = JsonSerializer.Deserialize<TValue>(ref reader, options);
                            break;
                        default:
                            reader.Skip();
                            break;
                    }
                }
            }

            return new OpenRiaServices.Serialization.KeyValue<TKey, TValue>(key, value);
        }

        public override void Write(Utf8JsonWriter writer, OpenRiaServices.Serialization.KeyValue<TKey, TValue> value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("Key");
            JsonSerializer.Serialize(writer, value.Key, options);
            writer.WritePropertyName("Value");
            JsonSerializer.Serialize(writer, value.Value, options);
            writer.WriteEndObject();
        }
    }
}
#endif
