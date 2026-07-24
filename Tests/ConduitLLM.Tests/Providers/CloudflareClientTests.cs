using System.Net;
using System.Text;

using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Providers.Cloudflare;

using Microsoft.Extensions.Logging.Abstractions;

using Moq;
using Moq.Protected;

using Xunit;

namespace ConduitLLM.Tests.Providers
{
    /// <summary>
    /// Unit tests for <see cref="CloudflareClient"/> model discovery, which backs the provider
    /// connection test. Cloudflare has no OpenAI-style <c>GET /ai/v1/models</c> endpoint (that path
    /// returns HTTP 405), so discovery must target the native <c>/ai/models/search</c> endpoint.
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Component", "Providers")]
    public class CloudflareClientTests
    {
        private const string AccountId = "9aa7981cb0a1a3c713be5eb800686daf";

        [Fact]
        public async Task GetModelsAsync_QueriesNativeModelsSearchEndpoint_NotOpenAiModelsPath()
        {
            // Arrange
            HttpRequestMessage? captured = null;
            var handler = CreateHandler(
                HttpStatusCode.OK,
                """
                {
                  "success": true,
                  "errors": [],
                  "messages": [],
                  "result": [
                    { "id": "uuid-1", "name": "@cf/meta/llama-3.1-8b-instruct", "description": "Llama 3.1 8B" },
                    { "id": "uuid-2", "name": "@cf/baai/bge-m3", "description": "BGE-M3 Embedding" }
                  ]
                }
                """,
                onRequest: req => captured = req);

            var client = CreateClient(handler);

            // Act
            var models = await client.GetModelsAsync();

            // Assert: the request must hit the native models-search endpoint via GET, not /ai/v1/models.
            Assert.NotNull(captured);
            Assert.Equal(HttpMethod.Get, captured!.Method);
            Assert.Equal(
                $"https://api.cloudflare.com/client/v4/accounts/{AccountId}/ai/models/search",
                captured.RequestUri!.ToString());

            Assert.Contains(models, m => m.Id == "@cf/meta/llama-3.1-8b-instruct");
            Assert.Contains(models, m => m.Id == "@cf/baai/bge-m3");
        }

        [Fact]
        public async Task GetModelsAsync_AuthenticationError_SurfacesCloudflareMessage()
        {
            // Arrange: Cloudflare returns its "Authentication error" envelope for a bad token.
            var handler = CreateHandler(
                HttpStatusCode.BadRequest,
                """{ "success": false, "errors": [ { "code": 10000, "message": "Authentication error" } ], "result": null }""");

            var client = CreateClient(handler);

            // Act & Assert: the error propagates (is not swallowed into the fallback list) and carries
            // Cloudflare's message so the connection-test classifier can report an invalid key.
            var ex = await Assert.ThrowsAsync<LLMCommunicationException>(() => client.GetModelsAsync());
            Assert.Contains("Authentication error", ex.Message);
        }

        private static Mock<HttpMessageHandler> CreateHandler(
            HttpStatusCode statusCode,
            string body,
            Action<HttpRequestMessage>? onRequest = null)
        {
            var handler = new Mock<HttpMessageHandler>();
            handler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((req, _) => onRequest?.Invoke(req))
                .ReturnsAsync(() => new HttpResponseMessage
                {
                    StatusCode = statusCode,
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                });
            return handler;
        }

        private static CloudflareClient CreateClient(Mock<HttpMessageHandler> handler)
        {
            var httpClient = new HttpClient(handler.Object);
            var factory = new Mock<IHttpClientFactory>();
            factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

            var provider = new Provider
            {
                Id = 1,
                ProviderType = ProviderType.Cloudflare,
                ProviderName = "cloudflare1",
                Settings = new Dictionary<string, string> { ["account_id"] = AccountId }
            };

            var key = new ProviderKeyCredential
            {
                Id = 1,
                ProviderId = 1,
                ApiKey = "test-token",
                IsPrimary = true,
                IsEnabled = true
            };

            return new CloudflareClient(
                provider,
                key,
                "@cf/meta/llama-3.1-8b-instruct",
                NullLogger<CloudflareClient>.Instance,
                factory.Object);
        }
    }
}
