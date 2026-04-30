using Microsoft.AspNetCore.Http;
using OpenRiaServices.Server;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace OpenRiaServices.Hosting.AspNetCore.Serialization
{
    internal sealed class JsonSerializationProvider : ISerializationProvider
    {
        private readonly ConcurrentDictionary<(Type, string), JsonRequestSerializer> _serializers = new();

        public RequestSerializer GetRequestSerializer(DomainOperationEntry operation)
        {
            var key = (operation.DomainServiceType, operation.Name);

            if (_serializers.TryGetValue(key, out var serializer))
                return serializer;

            var domainServiceDescription = DomainServiceDescription.GetDescription(operation.DomainServiceType);
            serializer = new JsonRequestSerializer(operation, domainServiceDescription);
            return _serializers.GetOrAdd(key, serializer);
        }
    }

    internal sealed class JsonRequestSerializer : RequestSerializer
    {
        private readonly DomainOperationEntry _operation;
        private readonly DomainServiceDescription _domainServiceDescription;
        private readonly JsonSerializerOptions _serializerOptions;
        private readonly string _responseName;
        private readonly string _resultName;

        public JsonRequestSerializer(DomainOperationEntry operation, DomainServiceDescription domainServiceDescription)
        {
            _operation = operation;
            _domainServiceDescription = domainServiceDescription;
            _serializerOptions = JsonSerializationHelper.GetSerializerOptions(domainServiceDescription);
            _responseName = operation.Name + "Response";
            _resultName = operation.Name + "Result";

            if (operation.Operation == DomainOperation.Custom)
            {
                _responseName = "SubmitChangesResponse";
                _resultName = "SubmitChangesResult";
            }
        }

        public override bool CanRead(ReadOnlySpan<char> contentType)
            => MatchesMediaType(contentType, MimeTypes.Json);

        public override bool CanWrite(ReadOnlySpan<char> contentType)
            => MatchesMediaType(contentType, MimeTypes.Json);

        public override async Task<(ServiceQuery?, object?[])> ReadParametersFromBodyAsync(HttpContext context, DomainOperationEntry operation)
        {
            var request = context.Request;

            using var document = await JsonDocument.ParseAsync(request.Body);
            var root = document.RootElement;

            ServiceQuery? serviceQuery = null;
            object?[]? values = null;

            if (root.TryGetProperty("QueryOptions", out var queryOptionsElement))
            {
                serviceQuery = ReadServiceQuery(queryOptionsElement);
            }

            values = ReadParameters(root, operation);

            return (serviceQuery, values);
        }

        public override async Task<IEnumerable<ChangeSetEntry>> ReadSubmitRequestAsync(HttpContext context)
        {
            (_, var parameters) = await ReadParametersFromBodyAsync(context, _operation);
            return (IEnumerable<ChangeSetEntry>)parameters[0]!;
        }

        private static ServiceQuery ReadServiceQuery(JsonElement queryOptionsElement)
        {
            var serviceQueryParts = new List<ServiceQueryPart>();
            bool includeTotalCount = false;

            if (queryOptionsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var option in queryOptionsElement.EnumerateArray())
                {
                    string? name = null;
                    string? value = null;

                    if (option.TryGetProperty("Name", out var nameElement))
                    {
                        name = nameElement.GetString();
                    }
                    else if (option.TryGetProperty("name", out nameElement))
                    {
                        name = nameElement.GetString();
                    }

                    if (option.TryGetProperty("Value", out var valueElement))
                    {
                        value = valueElement.GetString();
                    }
                    else if (option.TryGetProperty("value", out valueElement))
                    {
                        value = valueElement.GetString();
                    }

                    if (string.Equals(name, "includeTotalCount", StringComparison.OrdinalIgnoreCase))
                    {
                        if (bool.TryParse(value, out var queryOptionValue))
                        {
                            includeTotalCount = queryOptionValue;
                        }
                    }
                    else if (name != null)
                    {
                        serviceQueryParts.Add(new ServiceQueryPart { QueryOperator = name, Expression = value });
                    }
                }
            }

            return new ServiceQuery()
            {
                QueryParts = serviceQueryParts,
                IncludeTotalCount = includeTotalCount
            };
        }

        private object?[] ReadParameters(JsonElement root, DomainOperationEntry operation)
        {
            if (operation.Name == "Submit")
            {
                return ReadSubmitRequest(root);
            }

            var parameters = operation.Parameters;
            object?[] values = new object?[parameters.Count];

            for (int i = 0; i < parameters.Count; ++i)
            {
                var parameter = parameters[i];

                if (root.TryGetProperty(parameter.Name, out var paramElement))
                {
                    values[i] = JsonSerializer.Deserialize(paramElement, parameter.ParameterType, _serializerOptions);
                }
                else if (parameter.IsNullable)
                {
                    values[i] = null;
                }
                else
                {
                    throw new BadHttpRequestException($"Missing value for parameter '{parameter.Name}'");
                }
            }

            return values;
        }

        private object?[] ReadSubmitRequest(JsonElement root)
        {
            if (root.TryGetProperty("changeSet", out var changeSetElement))
            {
                var changeSet = JsonSerializer.Deserialize<IEnumerable<ChangeSetEntry>>(changeSetElement, _serializerOptions);
                return new object?[] { changeSet };
            }
            else
            {
                throw new BadHttpRequestException("missing changeSet");
            }
        }

        public override async Task WriteErrorAsync(HttpContext context, DomainServiceFault fault, DomainOperationEntry operation)
        {
            var ct = context.RequestAborted;
            if (ct.IsCancellationRequested)
                return;

            var response = context.Response;
            response.ContentType = MimeTypes.Json;

            using var memoryStream = new MemoryStream();
            using var writer = new Utf8JsonWriter(memoryStream);

            writer.WriteStartObject();
            writer.WritePropertyName("error");
            JsonSerializer.Serialize(writer, fault, _serializerOptions);
            writer.WriteEndObject();

            await writer.FlushAsync(ct);
            memoryStream.Position = 0;

            response.ContentLength = memoryStream.Length;
            await memoryStream.CopyToAsync(response.Body, ct);
        }

        public override async Task WriteResponseAsync(HttpContext context, object? result, DomainOperationEntry operation)
        {
            var ct = context.RequestAborted;
            if (ct.IsCancellationRequested)
                return;

            var response = context.Response;
            response.ContentType = MimeTypes.Json;

            using var memoryStream = new MemoryStream();
            using var writer = new Utf8JsonWriter(memoryStream);

            writer.WriteStartObject();
            writer.WritePropertyName(_responseName);
            writer.WriteStartObject();
            writer.WritePropertyName(_resultName);

            if (result != null)
            {
                JsonSerializer.Serialize(writer, result, _serializerOptions);
            }
            else
            {
                writer.WriteNullValue();
            }

            writer.WriteEndObject();
            writer.WriteEndObject();

            await writer.FlushAsync(ct);
            memoryStream.Position = 0;

            response.ContentLength = memoryStream.Length;
            await memoryStream.CopyToAsync(response.Body, ct);
        }

        public override Task WriteSubmitResponseAsync(HttpContext context, IEnumerable<ChangeSetEntry> result)
        {
            return WriteResponseAsync(context, result, _operation);
        }

        private static bool MatchesMediaType(ReadOnlySpan<char> value, ReadOnlySpan<char> expected)
        {
            int separator = value.IndexOf(';');
            if (separator >= 0)
                value = value[..separator];

            return value.Equals(expected, StringComparison.OrdinalIgnoreCase);
        }
    }
}
