#if NET8_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Serialization;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using OpenRiaServices.Client.DomainClients;

namespace OpenRiaServices.Client.DomainClients.Http
{
    sealed class JsonHttpDomainClient : DomainClient
    {
        internal const string MediaType = "application/json";

        private const HttpCompletionOption DefaultHttpCompletionOption = HttpCompletionOption.ResponseContentRead;
        private static readonly Task<HttpResponseMessage> s_skipGetUsePostInstead = Task.FromResult<HttpResponseMessage>(null);

        private readonly HttpClient _httpClient;
        private readonly Type _serviceInterface;
        private readonly Dictionary<string, MethodParameters> _methodParametersCache = new Dictionary<string, MethodParameters>();

        private JsonSerializerOptions _serializerOptions;

        public override bool SupportsCancellation => true;

        private string ContentType => MediaType;

        public JsonHttpDomainClient(HttpClient httpClient, Type serviceInterface)
        {
            ArgumentNullException.ThrowIfNull(serviceInterface);

            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _serviceInterface = serviceInterface;
        }

        private JsonSerializerOptions GetSerializerOptions()
        {
            _serializerOptions ??= JsonSerializationHelper.GetSerializerOptions(_serviceInterface, EntityTypes);
            return _serializerOptions;
        }

        internal MethodParameters GetMethodParameters(string operationName)
        {
            lock (_methodParametersCache)
            {
                if (!_methodParametersCache.TryGetValue(operationName, out var methodParameters))
                {
                    methodParameters = new MethodParameters(_serviceInterface, operationName);
                    _methodParametersCache.Add(operationName, methodParameters);
                }
                return methodParameters;
            }
        }

        protected override async Task<InvokeCompletedResult> InvokeAsyncCore(InvokeArgs invokeArgs, CancellationToken cancellationToken)
        {
            var response = await ExecuteRequestAsync(invokeArgs.OperationName, invokeArgs.HasSideEffects, invokeArgs.Parameters, queryOptions: null, cancellationToken: cancellationToken)
                 .ConfigureAwait(false);

            IEnumerable<ValidationResult> validationErrors = null;
            object returnValue = null;

            try
            {
                returnValue = await ReadResponseAsync(response, invokeArgs.OperationName, invokeArgs.ReturnType)
                     .ConfigureAwait(false);
            }
            catch (DomainServiceFaultException fe)
            {
                if (fe.Fault.OperationErrors != null)
                {
                    validationErrors = fe.Fault.GetValidationErrors();
                }
                else
                {
                    throw GetExceptionFromServiceFault(fe.Fault);
                }
            }

            return new InvokeCompletedResult(returnValue, validationErrors ?? Enumerable.Empty<ValidationResult>());
        }

        protected override async Task<SubmitCompletedResult> SubmitAsyncCore(EntityChangeSet changeSet, CancellationToken cancellationToken)
        {
            const string operationName = "SubmitChanges";
            var entries = changeSet.GetChangeSetEntries().ToList();
            var parameters = new Dictionary<string, object>() {
                     {"changeSet", entries}
                };

            var response = await ExecuteRequestAsync(operationName, hasSideEffects: true, parameters: parameters, queryOptions: null, cancellationToken: cancellationToken)
                 .ConfigureAwait(false);

            try
            {
                var returnValue = (IEnumerable<ChangeSetEntry>)await ReadResponseAsync(response, operationName, typeof(IEnumerable<ChangeSetEntry>))
                     .ConfigureAwait(false);
                return new SubmitCompletedResult(changeSet, returnValue ?? Enumerable.Empty<ChangeSetEntry>());
            }
            catch (DomainServiceFaultException fe)
            {
                throw GetExceptionFromServiceFault(fe.Fault);
            }
        }

        protected override Task<QueryCompletedResult> QueryAsyncCore(EntityQuery query, CancellationToken cancellationToken)
        {
            List<ServiceQueryPart> queryOptions = query.Query != null ? QuerySerializer.Serialize(query.Query) : null;

            if (query.IncludeTotalCount)
            {
                queryOptions = queryOptions ?? new List<ServiceQueryPart>();
                queryOptions.Add(new ServiceQueryPart()
                {
                    QueryOperator = "includeTotalCount",
                    Expression = "True"
                });
            }

            var responseTask = ExecuteRequestAsync(query.QueryName, query.HasSideEffects, query.Parameters, queryOptions, cancellationToken);
            return QueryAsyncCoreContinuation();

            async Task<QueryCompletedResult> QueryAsyncCoreContinuation()
            {
                var response = await responseTask.ConfigureAwait(false);
                IEnumerable<ValidationResult> validationErrors = null;
                try
                {
                    var queryType = typeof(QueryResult<>).MakeGenericType(query.EntityType);
                    var queryResult = (QueryResult)await ReadResponseAsync(response, query.QueryName, queryType)
                         .ConfigureAwait(false);
                    if (queryResult != null)
                    {
                        return new QueryCompletedResult(
                             queryResult.GetRootResults().Cast<Entity>(),
                             queryResult.GetIncludedResults().Cast<Entity>(),
                             queryResult.TotalCount,
                             Enumerable.Empty<ValidationResult>());
                    }
                }
                catch (DomainServiceFaultException fe)
                {
                    if (fe.Fault.OperationErrors != null)
                    {
                        validationErrors = fe.Fault.GetValidationErrors();
                    }
                    else
                    {
                        throw GetExceptionFromServiceFault(fe.Fault);
                    }
                }

                return new QueryCompletedResult(
                          Enumerable.Empty<Entity>(),
                          Enumerable.Empty<Entity>(),
                          0,
                          validationErrors ?? Enumerable.Empty<ValidationResult>());
            }
        }

        private Task<HttpResponseMessage> ExecuteRequestAsync(string operationName, bool hasSideEffects, IDictionary<string, object> parameters,
             List<ServiceQueryPart> queryOptions,
             CancellationToken cancellationToken)
        {
            Task<HttpResponseMessage> response = s_skipGetUsePostInstead;

            if (!hasSideEffects)
            {
                response = GetAsync(operationName, parameters, queryOptions, cancellationToken);
            }
            if (ReferenceEquals(response, s_skipGetUsePostInstead))
            {
                response = PostAsync(operationName, parameters, queryOptions, cancellationToken);
            }

            return response;
        }

        private Task<HttpResponseMessage> PostAsync(string operationName, IDictionary<string, object> parameters, List<ServiceQueryPart> queryOptions, CancellationToken cancellationToken)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, operationName);

            var requestBody = new Dictionary<string, object>();

            if (queryOptions != null && queryOptions.Count > 0)
            {
                requestBody["QueryOptions"] = queryOptions.Select(q => new { Name = q.QueryOperator, Value = q.Expression }).ToList();
            }

            if (parameters != null && parameters.Count > 0)
            {
                MethodParameters methodParameter = GetMethodParameters(operationName);
                foreach (var param in parameters)
                {
                    requestBody[param.Key] = param.Value;
                }
            }

            var options = GetSerializerOptions();
            var json = JsonSerializer.Serialize(requestBody, options);
            var content = new StringContent(json, Encoding.UTF8, ContentType);
            request.Content = content;

            return _httpClient.SendAsync(request, DefaultHttpCompletionOption, cancellationToken);
        }

        private Task<HttpResponseMessage> GetAsync(string operationName, IDictionary<string, object> parameters, List<ServiceQueryPart> queryOptions, CancellationToken cancellationToken)
        {
            int i = 0;
            var uriBuilder = new StringBuilder(256);
            uriBuilder.Append(operationName);

            if (parameters != null && parameters.Count > 0)
            {
                var methodParameters = GetMethodParameters(operationName);
                foreach (var param in parameters)
                {
                    if (param.Value != null)
                    {
                        uriBuilder.Append(i++ == 0 ? '?' : '&');
                        uriBuilder.Append(Uri.EscapeDataString(param.Key));
                        uriBuilder.Append('=');
                        var parameterType = methodParameters.GetTypeForMethodParameter(param.Key);
                        var value = WebQueryStringConverter.ConvertValueToString(param.Value, parameterType);
                        uriBuilder.Append(Uri.EscapeDataString(value));
                    }
                }
            }

            if (queryOptions != null && queryOptions.Count > 0)
            {
                foreach (var queryPart in queryOptions)
                {
                    uriBuilder.Append(i++ == 0 ? "?$" : "&$");
                    uriBuilder.Append(queryPart.QueryOperator);
                    uriBuilder.Append('=');
                    uriBuilder.Append(Uri.EscapeDataString(Uri.EscapeDataString(queryPart.Expression)));
                }
            }

            var uri = uriBuilder.ToString();

            if (uri.Length - operationName.Length > 2048)
                return s_skipGetUsePostInstead;

            return _httpClient.GetAsync(uri, DefaultHttpCompletionOption, cancellationToken);
        }

        private async Task<object> ReadResponseAsync(HttpResponseMessage response, string operationName, Type returnType)
        {
            using (response)
            {
                var contentType = response.Content.Headers.ContentType?.MediaType;
                if (!response.IsSuccessStatusCode && contentType != ContentType && contentType != "application/problem+json")
                {
                    var message = string.Format(CultureInfo.InvariantCulture, Resources.DomainClient_UnexpectedHttpStatusCode, (int)response.StatusCode, response.StatusCode);

                    if (response.StatusCode == HttpStatusCode.BadRequest)
                        throw new DomainOperationException(message, OperationErrorStatus.NotSupported, (int)response.StatusCode, null);
                    else if (response.StatusCode == HttpStatusCode.Unauthorized)
                        throw new DomainOperationException(message, OperationErrorStatus.Unauthorized, (int)response.StatusCode, null);
                    else if (response.StatusCode == HttpStatusCode.NotFound)
                        throw new DomainOperationException(message, OperationErrorStatus.NotFound, (int)response.StatusCode, null);
                    else
                        throw new DomainOperationException(message, OperationErrorStatus.ServerError, (int)response.StatusCode, null);
                }

                using (var stream = await response.Content.ReadAsStreamAsync())
                {
                    var options = GetSerializerOptions();

                    try
                    {
                        using (var doc = await JsonDocument.ParseAsync(stream))
                        {
                            var root = doc.RootElement;

                            if (root.TryGetProperty("error", out var errorElement) || root.TryGetProperty("Fault", out _))
                            {
                                var fault = DeserializeFault(root, options);
                                throw new DomainServiceFaultException(fault);
                            }

                            var responsePropertyName = operationName + "Response";
                            if (root.TryGetProperty(responsePropertyName, out var responseElement))
                            {
                                var resultPropertyName = operationName + "Result";
                                if (responseElement.TryGetProperty(resultPropertyName, out var resultElement))
                                {
                                    if (returnType == typeof(void))
                                    {
                                        return null;
                                    }

                                    if (resultElement.ValueKind == JsonValueKind.Null)
                                    {
                                        return null;
                                    }

                                    return JsonSerializer.Deserialize(resultElement, returnType, options);
                                }
                                else if (responseElement.TryGetProperty("returnValue", out var returnValueElement))
                                {
                                    if (returnType == typeof(void))
                                    {
                                        return null;
                                    }

                                    if (returnValueElement.ValueKind == JsonValueKind.Null)
                                    {
                                        return null;
                                    }

                                    return JsonSerializer.Deserialize(returnValueElement, returnType, options);
                                }
                                else
                                {
                                    if (returnType == typeof(void))
                                    {
                                        return null;
                                    }

                                    throw new DomainOperationException(
                                        string.Format(CultureInfo.InvariantCulture, Resources.DomainClient_UnexpectedResultContent, operationName + "Result", "JSON"),
                                        OperationErrorStatus.ServerError, 0, null);
                                }
                            }
                            else if (root.TryGetProperty("d", out var dElement))
                            {
                                if (returnType == typeof(void))
                                {
                                    return null;
                                }

                                if (dElement.ValueKind == JsonValueKind.Null)
                                {
                                    return null;
                                }

                                return JsonSerializer.Deserialize(dElement, returnType, options);
                            }
                            else
                            {
                                if (returnType == typeof(void))
                                {
                                    return null;
                                }

                                return JsonSerializer.Deserialize(root, returnType, options);
                            }
                        }
                    }
                    catch (JsonException ex)
                    {
                        throw new DomainOperationException(
                            string.Format(Resources.DomainClient_UnexpectedResultContent, operationName + "Response", "JSON"),
                            ex);
                    }
                }
            }
        }

        private static DomainServiceFault DeserializeFault(JsonElement root, JsonSerializerOptions options)
        {
            if (root.TryGetProperty("Fault", out var faultElement))
            {
                return JsonSerializer.Deserialize<DomainServiceFault>(faultElement, options);
            }
            else if (root.TryGetProperty("error", out var errorElement))
            {
                var fault = new DomainServiceFault();
                
                if (errorElement.TryGetProperty("code", out var codeElement))
                {
                    fault.ErrorCode = codeElement.GetInt32();
                }
                else if (errorElement.TryGetProperty("ErrorCode", out codeElement))
                {
                    fault.ErrorCode = codeElement.GetInt32();
                }

                if (errorElement.TryGetProperty("message", out var messageElement))
                {
                    fault.ErrorMessage = messageElement.GetString();
                }
                else if (errorElement.TryGetProperty("ErrorMessage", out messageElement))
                {
                    fault.ErrorMessage = messageElement.GetString();
                }

                return fault;
            }

            return JsonSerializer.Deserialize<DomainServiceFault>(root, options);
        }

        private static Exception GetExceptionFromServiceFault(DomainServiceFault serviceFault)
        {
            if (serviceFault.IsDomainException)
            {
                return new DomainException(serviceFault.ErrorMessage, serviceFault.ErrorCode, serviceFault.StackTrace);
            }
            else if (serviceFault.ErrorCode == 400)
            {
                return new DomainOperationException(serviceFault.ErrorMessage, OperationErrorStatus.NotSupported, serviceFault.ErrorCode, serviceFault.StackTrace);
            }
            else if (serviceFault.ErrorCode is 401 or 403)
            {
                return new DomainOperationException(serviceFault.ErrorMessage, OperationErrorStatus.Unauthorized, serviceFault.ErrorCode, serviceFault.StackTrace);
            }
            else
            {
                return new DomainOperationException(serviceFault.ErrorMessage, OperationErrorStatus.ServerError, serviceFault.ErrorCode, serviceFault.StackTrace);
            }
        }
    }

    internal class DomainServiceFaultException : Exception
    {
        public DomainServiceFault Fault { get; }

        public DomainServiceFaultException(DomainServiceFault fault)
        {
            Fault = fault;
        }
    }
}
#endif
