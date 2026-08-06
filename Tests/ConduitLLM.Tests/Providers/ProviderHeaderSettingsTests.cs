using System.Net;
using System.Text;

using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Providers.OpenAI;

using Microsoft.Extensions.Logging.Abstractions;

using Moq;
using Moq.Protected;

using Xunit;

namespace ConduitLLM.Tests.Providers
{
    /// <summary>
    /// Header-bound provider settings must actually reach the wire. The organization was previously
    /// stored on the key credential and read by nothing, so requests went out unscoped and usage was
    /// attributed to the key's default organization (issue #1185).
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Component", "Providers")]
    public class ProviderHeaderSettingsTests
    {
        [Fact]
        public async Task OpenAI_Requests_Should_Carry_The_Configured_Organization_And_Project_Headers()
        {
            HttpRequestMessage? captured = null;
            var client = CreateOpenAIClient(
                new Dictionary<string, string>
                {
                    ["organization"] = "org-acme",
                    ["project"] = "proj_widgets"
                },
                request => captured = request);

            await client.GetModelsAsync();

            Assert.NotNull(captured);
            Assert.Equal("org-acme", Single(captured!, "OpenAI-Organization"));
            Assert.Equal("proj_widgets", Single(captured, "OpenAI-Project"));
        }

        [Fact]
        public async Task OpenAI_Requests_Should_Omit_Headers_That_Were_Not_Configured()
        {
            HttpRequestMessage? captured = null;
            var client = CreateOpenAIClient(
                new Dictionary<string, string> { ["organization"] = "org-acme" },
                request => captured = request);

            await client.GetModelsAsync();

            Assert.NotNull(captured);
            Assert.Equal("org-acme", Single(captured!, "OpenAI-Organization"));
            Assert.False(HasHeader(captured, "OpenAI-Project"));
        }

        [Fact]
        public async Task OpenAI_Requests_Should_Send_No_Scoping_Headers_When_No_Settings_Are_Configured()
        {
            HttpRequestMessage? captured = null;
            var client = CreateOpenAIClient(settings: null, request => captured = request);

            await client.GetModelsAsync();

            Assert.NotNull(captured);
            Assert.False(HasHeader(captured!, "OpenAI-Organization"));
            Assert.False(HasHeader(captured, "OpenAI-Project"));
        }

        private static bool HasHeader(HttpRequestMessage request, string name) =>
            request.Headers.Contains(name);

        private static string? Single(HttpRequestMessage request, string name) =>
            request.Headers.TryGetValues(name, out var values) ? values.Single() : null;

        private static OpenAIClient CreateOpenAIClient(
            Dictionary<string, string>? settings,
            Action<HttpRequestMessage> onRequest)
        {
            var handler = new Mock<HttpMessageHandler>();
            handler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((request, _) => onRequest(request))
                .ReturnsAsync(() => new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("""{ "object": "list", "data": [] }""", Encoding.UTF8, "application/json")
                });

            var factory = new Mock<IHttpClientFactory>();
            // A fresh HttpClient per call: DefaultRequestHeaders are configured on the instance, so a
            // shared one would let a previous test's headers leak into the next assertion.
            factory.Setup(f => f.CreateClient(It.IsAny<string>()))
                .Returns(() => new HttpClient(handler.Object));

            var provider = new Provider
            {
                Id = 1,
                ProviderType = ProviderType.OpenAI,
                ProviderName = "openai",
                Settings = settings
            };

            var key = new ProviderKeyCredential
            {
                Id = 1,
                ProviderId = 1,
                ApiKey = "sk-test",
                IsPrimary = true,
                IsEnabled = true
            };

            return new OpenAIClient(
                provider,
                key,
                "gpt-4o",
                NullLogger<OpenAIClient>.Instance,
                factory.Object);
        }
    }
}
