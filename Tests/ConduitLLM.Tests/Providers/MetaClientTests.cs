using System.Net;
using System.Text;
using System.Text.Json;

using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Models;
using ConduitLLM.Providers.Meta;

using Moq;

using Xunit.Abstractions;

namespace ConduitLLM.Tests.Providers
{
    /// <summary>
    /// Unit tests for the MetaClient class, covering the Meta Model API (Muse Spark models).
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Component", "Providers")]
    public class MetaClientTests : TestBase
    {
        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
        private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
        private readonly HttpClient _httpClient;

        public MetaClientTests(ITestOutputHelper output) : base(output)
        {
            _httpClientFactoryMock = new Mock<IHttpClientFactory>();
            _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_httpMessageHandlerMock.Object)
            {
                BaseAddress = new Uri("https://api.meta.ai/v1/")
            };
            _httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(_httpClient);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidCredentials_InitializesCorrectly()
        {
            // Arrange
            var provider = new Provider
            {
                Id = 1,
                ProviderType = ProviderType.Meta
            };

            var keyCredential = new ProviderKeyCredential
            {
                Id = 1,
                ProviderId = 1,
                ApiKey = "test-api-key"
            };

            var modelId = "muse-spark-1.1";
            var logger = CreateLogger<MetaClient>();

            // Act
            var client = new MetaClient(
                provider,
                keyCredential,
                modelId,
                logger.Object,
                _httpClientFactoryMock.Object);

            // Assert
            Assert.NotNull(client);
        }

        [Fact]
        public void Constructor_WithMissingApiKey_ThrowsConfigurationException()
        {
            // Arrange
            var provider = new Provider
            {
                Id = 1,
                ProviderType = ProviderType.Meta
            };

            var keyCredential = new ProviderKeyCredential
            {
                Id = 1,
                ProviderId = 1,
                ApiKey = "" // Empty API key
            };

            var modelId = "muse-spark-1.1";
            var logger = CreateLogger<MetaClient>();

            // Act & Assert
            var ex = Assert.Throws<ConfigurationException>(() =>
                new MetaClient(
                    provider,
                    keyCredential,
                    modelId,
                    logger.Object,
                    _httpClientFactoryMock.Object));

            Assert.Contains("API key is missing", ex.Message);
        }

        [Fact]
        public void Constructor_WithNullCredentials_ThrowsException()
        {
            // Arrange
            var modelId = "muse-spark-1.1";
            var logger = CreateLogger<MetaClient>();

            // Act & Assert
            Assert.ThrowsAny<Exception>(() =>
                new MetaClient(
                    null!,
                    null!,
                    modelId,
                    logger.Object,
                    _httpClientFactoryMock.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Arrange
            var provider = new Provider
            {
                Id = 1,
                ProviderType = ProviderType.Meta
            };

            var keyCredential = new ProviderKeyCredential
            {
                Id = 1,
                ProviderId = 1,
                ApiKey = "test-api-key"
            };

            var modelId = "muse-spark-1.1";

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new MetaClient(
                    provider,
                    keyCredential,
                    modelId,
                    null!,
                    _httpClientFactoryMock.Object));
        }

        #endregion

        #region Model Support Tests

        [Theory]
        [InlineData("muse-spark-1.1")]
        [InlineData("muse-spark-1")]
        public void SupportedModels_AreRecognized(string modelId)
        {
            // Arrange
            var provider = new Provider
            {
                Id = 1,
                ProviderType = ProviderType.Meta
            };

            var keyCredential = new ProviderKeyCredential
            {
                Id = 1,
                ProviderId = 1,
                ApiKey = "test-api-key"
            };

            var logger = CreateLogger<MetaClient>();

            // Act
            var client = new MetaClient(
                provider,
                keyCredential,
                modelId,
                logger.Object,
                _httpClientFactoryMock.Object);

            // Assert
            Assert.NotNull(client);
        }

        #endregion

        public static IEnumerable<object[]> HttpErrorCases()
        {
            yield return new object[]
            {
                HttpStatusCode.BadRequest,
                """{"error":{"message":"input token limit exceeded"}}""",
                typeof(ValidationException),
                "Token limit error"
            };
            yield return new object[]
            {
                HttpStatusCode.Unauthorized,
                """{"error":{"message":"invalid access token"}}""",
                typeof(ConfigurationException),
                "Invalid Meta Model API key"
            };
            yield return new object[]
            {
                HttpStatusCode.NotFound,
                """{"error":{"message":"model not found"}}""",
                typeof(ModelNotFoundException),
                "specified model is not available"
            };
            yield return new object[]
            {
                HttpStatusCode.TooManyRequests,
                """{"error":{"message":"rate limit exceeded"}}""",
                typeof(LLMCommunicationException),
                "rate limit exceeded"
            };
            yield return new object[]
            {
                HttpStatusCode.InternalServerError,
                """{"error":{"message":"provider failed"}}""",
                typeof(LLMCommunicationException),
                "internal error"
            };
        }

        [Theory]
        [MemberData(nameof(HttpErrorCases))]
        public async Task ChatCompletion_TranslatesMetaErrorsAndPreservesRequestDetails(
            HttpStatusCode statusCode,
            string responseBody,
            Type expectedExceptionType,
            string expectedMessage)
        {
            var handler = new RecordingHandler(statusCode, responseBody);
            var client = CreateClient(handler);

            var exception = await Assert.ThrowsAnyAsync<Exception>(
                () => client.CreateChatCompletionAsync(CreateChatRequest()));

            AssertProviderError(
                exception,
                expectedExceptionType,
                expectedMessage,
                statusCode,
                responseBody);
            AssertRequest(handler, streaming: false);
        }

        [Theory]
        [MemberData(nameof(HttpErrorCases))]
        public async Task StreamingChatCompletion_TranslatesMetaErrorsAndPreservesRequestDetails(
            HttpStatusCode statusCode,
            string responseBody,
            Type expectedExceptionType,
            string expectedMessage)
        {
            var handler = new RecordingHandler(statusCode, responseBody);
            var client = CreateClient(handler);

            var exception = await Assert.ThrowsAnyAsync<Exception>(async () =>
            {
                await foreach (var _ in client.StreamChatCompletionAsync(CreateChatRequest()))
                {
                }
            });

            AssertProviderError(
                exception,
                expectedExceptionType,
                expectedMessage,
                statusCode,
                responseBody);
            AssertRequest(handler, streaming: true);
        }

        [Theory]
        [MemberData(nameof(HttpErrorCases))]
        public async Task Embeddings_UseProviderSpecificErrorTranslation(
            HttpStatusCode statusCode,
            string responseBody,
            Type expectedExceptionType,
            string expectedMessage)
        {
            var client = CreateClient(new RecordingHandler(statusCode, responseBody));

            var exception = await Assert.ThrowsAnyAsync<Exception>(() =>
                client.CreateEmbeddingAsync(new EmbeddingRequest
                {
                    Model = "muse-spark-1.1",
                    Input = "hello"
                }));

            AssertProviderError(
                exception,
                expectedExceptionType,
                expectedMessage,
                statusCode,
                responseBody);
        }

        [Theory]
        [MemberData(nameof(HttpErrorCases))]
        public async Task Images_UseProviderSpecificErrorTranslation(
            HttpStatusCode statusCode,
            string responseBody,
            Type expectedExceptionType,
            string expectedMessage)
        {
            var client = CreateClient(new RecordingHandler(statusCode, responseBody));

            var exception = await Assert.ThrowsAnyAsync<Exception>(() =>
                client.CreateImageAsync(new ImageGenerationRequest
                {
                    Model = "muse-spark-1.1",
                    Prompt = "hello"
                }));

            AssertProviderError(
                exception,
                expectedExceptionType,
                expectedMessage,
                statusCode,
                responseBody);
        }

        private MetaClient CreateClient(RecordingHandler handler)
        {
            var factory = new Mock<IHttpClientFactory>();
            factory
                .Setup(value => value.CreateClient(It.IsAny<string>()))
                .Returns(new HttpClient(handler));

            var provider = new Provider
            {
                Id = 1,
                ProviderType = ProviderType.Meta,
                ProviderName = "meta-test"
            };

            var keyCredential = new ProviderKeyCredential
            {
                Id = 1,
                ProviderId = 1,
                ApiKey = "test-api-key",
                IsPrimary = true,
                IsEnabled = true
            };

            return new MetaClient(
                provider,
                keyCredential,
                "muse-spark-1.1",
                CreateLogger<MetaClient>().Object,
                factory.Object);
        }

        private static ChatCompletionRequest CreateChatRequest() => new()
        {
            Model = "muse-spark-1.1",
            Messages = new List<Message>
            {
                new() { Role = "user", Content = "Hello" }
            }
        };

        private static void AssertProviderError(
            Exception exception,
            Type expectedExceptionType,
            string expectedMessage,
            HttpStatusCode expectedStatusCode,
            string expectedResponseBody)
        {
            Assert.Equal(expectedExceptionType, exception.GetType());
            Assert.Contains(expectedMessage, exception.Message, StringComparison.OrdinalIgnoreCase);

            var communicationError = exception as LLMCommunicationException
                ?? exception.InnerException as LLMCommunicationException;

            Assert.NotNull(communicationError);
            Assert.Equal(expectedStatusCode, communicationError!.StatusCode);
            Assert.Equal(expectedResponseBody, communicationError.ResponseBody);
            Assert.Equal("meta-request-id", communicationError.Data["RequestId"]);
        }

        private static void AssertRequest(RecordingHandler handler, bool streaming)
        {
            Assert.Equal(HttpMethod.Post, handler.Method);
            Assert.EndsWith("/chat/completions", handler.RequestUri!.AbsolutePath);
            Assert.Equal("Bearer", handler.AuthorizationScheme);
            Assert.Equal("test-api-key", handler.AuthorizationParameter);

            using var document = JsonDocument.Parse(handler.RequestBody!);
            Assert.Equal("muse-spark-1.1", document.RootElement.GetProperty("model").GetString());
            Assert.Equal(
                "Hello",
                document.RootElement
                    .GetProperty("messages")[0]
                    .GetProperty("content")
                    .GetString());

            if (streaming)
            {
                Assert.True(document.RootElement.GetProperty("stream").GetBoolean());
            }
            else
            {
                Assert.False(document.RootElement.TryGetProperty("stream", out _));
            }
        }

        private sealed class RecordingHandler(
            HttpStatusCode statusCode,
            string responseBody) : HttpMessageHandler
        {
            public HttpMethod? Method { get; private set; }
            public Uri? RequestUri { get; private set; }
            public string? AuthorizationScheme { get; private set; }
            public string? AuthorizationParameter { get; private set; }
            public string? RequestBody { get; private set; }

            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                Method = request.Method;
                RequestUri = request.RequestUri;
                AuthorizationScheme = request.Headers.Authorization?.Scheme;
                AuthorizationParameter = request.Headers.Authorization?.Parameter;
                RequestBody = request.Content == null
                    ? null
                    : await request.Content.ReadAsStringAsync(cancellationToken);

                var response = new HttpResponseMessage(statusCode)
                {
                    Content = new StringContent(
                        responseBody,
                        Encoding.UTF8,
                        "application/json")
                };
                response.Headers.TryAddWithoutValidation("X-Request-Id", "meta-request-id");
                return response;
            }
        }
    }
}
