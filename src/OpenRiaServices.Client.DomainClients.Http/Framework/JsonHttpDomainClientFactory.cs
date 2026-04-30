#if NET8_0_OR_GREATER
using System;
using System.Net.Http;
using OpenRiaServices.Client.DomainClients.Http;

namespace OpenRiaServices.Client.DomainClients
{
    public class JsonHttpDomainClientFactory
        : HttpDomainClientFactory
    {
        public JsonHttpDomainClientFactory(Uri serverBaseUri, HttpMessageHandler messageHandler)
            : base(serverBaseUri, messageHandler)
        {
        }

        public JsonHttpDomainClientFactory(Uri serverBaseUri, Func<HttpClient> httpClientFactory)
            : base(serverBaseUri, httpClientFactory)
        {
        }

        public JsonHttpDomainClientFactory(Uri serverBaseUri, Func<Uri, HttpClient> httpClientFactory)
            : base(serverBaseUri, httpClientFactory)
        {
        }

        protected override DomainClient CreateDomainClientCore(Type serviceContract, Uri serviceUri, bool requiresSecureEndpoint)
        {
            HttpClient httpClient = CreateHttpClient(serviceUri, JsonHttpDomainClient.MediaType);

            return new JsonHttpDomainClient(httpClient, serviceContract);
        }
    }
}
#endif
