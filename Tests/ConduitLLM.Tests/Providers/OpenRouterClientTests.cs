using System.Net;
using System.Text;

using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Providers.OpenRouter;

using FluentAssertions;

using Moq;
using Moq.Protected;

using Xunit.Abstractions;

namespace ConduitLLM.Tests.Providers
{
    /// <summary>
    /// Unit tests for OpenRouterClient: attribution headers and rich /models metadata mapping.
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Component", "Providers")]
    public class OpenRouterClientTests : TestBase
    {
        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
        private readonly Mock<HttpMessageHandler> _handlerMock;
        private readonly HttpClient _httpClient;
        private readonly List<Dictionary<string, string>> _capturedHeaders = new();
        private string _modelsJson = "{\"data\":[]}";

        public OpenRouterClientTests(ITestOutputHelper output) : base(output)
        {
            _httpClientFactoryMock = new Mock<IHttpClientFactory>();
            _handlerMock = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_handlerMock.Object)
            {
                BaseAddress = new Uri("https://openrouter.ai/api/v1/")
            };
            _httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(_httpClient);

            _handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Returns<HttpRequestMessage, CancellationToken>((req, _) =>
                {
                    _capturedHeaders.Add(req.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value)));
                    var path = req.RequestUri!.AbsolutePath;
                    var body = path.EndsWith("/key") ? "{\"data\":{}}" : _modelsJson;
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(body, Encoding.UTF8, "application/json")
                    });
                });
        }

        private OpenRouterClient CreateClient()
        {
            var provider = new Provider { Id = 1, ProviderType = ProviderType.OpenRouter };
            var keyCredential = new ProviderKeyCredential { Id = 1, ProviderId = 1, ApiKey = "test-api-key" };
            var logger = CreateLogger<OpenRouterClient>();
            return new OpenRouterClient(provider, keyCredential, "openai/gpt-4o", logger.Object, _httpClientFactoryMock.Object);
        }

        [Fact]
        public async Task GetModelsAsync_SendsAttributionHeaders_OnEveryRequest()
        {
            // Arrange
            var client = CreateClient();

            // Act — this makes both the GET /key and GET /models calls
            await client.GetModelsAsync();

            // Assert — HTTP-Referer + X-Title present on every outgoing request
            _capturedHeaders.Should().NotBeEmpty();
            _capturedHeaders.Should().OnlyContain(h =>
                h.ContainsKey("HTTP-Referer") && h.ContainsKey("X-Title"));
        }

        [Fact]
        public async Task GetModelsAsync_FullMetadata_PopulatesCapabilitiesAndTokenLimits()
        {
            // Arrange — a rich model entry + a bare minimal one
            _modelsJson = """
            {
                "data": [
                    {
                        "id": "anthropic/claude-3.5-sonnet",
                        "name": "Anthropic: Claude 3.5 Sonnet",
                        "context_length": 200000,
                        "architecture": {
                            "input_modalities": ["text", "image"],
                            "output_modalities": ["text"],
                            "tokenizer": "Claude"
                        },
                        "pricing": { "prompt": "0.000003", "completion": "0.000015" },
                        "top_provider": { "max_completion_tokens": 8192, "context_length": 200000 },
                        "supported_parameters": ["tools", "response_format", "reasoning"]
                    },
                    { "id": "minimal/model" }
                ]
            }
            """;
            var client = CreateClient();

            // Act
            var models = await client.GetModelsAsync();

            // Assert
            models.Should().HaveCount(2);

            var claude = models.First(m => m.Id == "anthropic/claude-3.5-sonnet");
            claude.Name.Should().Be("Anthropic: Claude 3.5 Sonnet");
            claude.Capabilities.Should().NotBeNull();
            claude.Capabilities!.Vision.Should().BeTrue();          // image input modality
            claude.Capabilities.FunctionCalling.Should().BeTrue();  // supported_parameters contains "tools"
            claude.Capabilities.ToolUsage.Should().BeTrue();
            claude.Capabilities.JsonMode.Should().BeTrue();         // supported_parameters contains "response_format"
            claude.Capabilities.ImageGeneration.Should().BeFalse(); // no image output modality
            claude.TokenLimits.Should().NotBeNull();
            claude.TokenLimits!.Context.Should().Be(200000);
            claude.TokenLimits.Output.Should().Be(8192);
        }

        [Fact]
        public async Task GetModelsAsync_MinimalModel_StillParses()
        {
            // Arrange — only the required id field present (schema-drift tolerance)
            _modelsJson = "{\"data\":[{\"id\":\"minimal/model\"}]}";
            var client = CreateClient();

            // Act
            var models = await client.GetModelsAsync();

            // Assert
            models.Should().ContainSingle();
            var model = models[0];
            model.Id.Should().Be("minimal/model");
            model.Capabilities.Should().NotBeNull();      // chat defaults still applied
            model.Capabilities!.Chat.Should().BeTrue();
            model.TokenLimits.Should().BeNull();          // no context/limits reported
        }
    }
}
