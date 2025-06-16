using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Models;
using ConduitLLM.Providers;
using ConduitLLM.Providers.InternalModels;
using ConduitLLM.Tests.TestHelpers;

using Microsoft.Extensions.Logging;

using Moq;
using Moq.Contrib.HttpClient;
using Moq.Protected;

using Xunit;
// Use alias to avoid ambiguity with mocks
using TestHelperMocks = ConduitLLM.Tests.TestHelpers.Mocks;

namespace ConduitLLM.Tests
{
    public class CohereClientTests
    {
        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
        private readonly Mock<HttpMessageHandler> _handlerMock;
        private readonly HttpClient _httpClient;
        private readonly Mock<ILogger<CohereClient>> _loggerMock;
        private readonly ProviderCredentials _credentials;
        private const string DefaultApiBase = "https://api.cohere.ai";
        private const string ChatEndpoint = "v1/chat";

        public CohereClientTests()
        {
            _handlerMock = new Mock<HttpMessageHandler>();
            _httpClient = _handlerMock.CreateClient();

            _httpClientFactoryMock = new Mock<IHttpClientFactory>();
            _httpClientFactoryMock
                .Setup(x => x.CreateClient(It.IsAny<string>()))
                .Returns(_httpClient);

            _loggerMock = new Mock<ILogger<CohereClient>>();
            _credentials = new ProviderCredentials
            {
                ProviderName = "Cohere",
                ApiKey = "cohere-testkey",
                ApiBase = DefaultApiBase
            };
        }

        // Helper to create a standard ChatCompletionRequest for testing
        private ChatCompletionRequest CreateTestRequest(string modelAlias = "cohere-alias")
        {
            return new ChatCompletionRequest
            {
                Model = modelAlias,
                Messages = new List<Message>
                {
                    new Message { Role = "system", Content = "You are a helpful assistant." },
                    new Message { Role = "user", Content = "Previous user message" },
                    new Message { Role = "assistant", Content = "Previous assistant response" },
                    new Message { Role = "user", Content = "Hello Cohere!" } // Last message is user
                },
                Temperature = 0.5,
                MaxTokens = 50,
                TopP = 0.95
            };
        }

        // Helper to create a standard Cohere API response
        private TestHelperMocks.CohereChatResponse CreateCohereResponse()
        {
            return new TestHelperMocks.CohereChatResponse
            {
                Text = "Hello there! How can I help you today?",
                Id = "gen-12345",
                Meta = new TestHelperMocks.CohereResponseMetadata
                {
                    TokenUsage = new TestHelperMocks.CohereTokenUsage
                    {
                        InputTokens = 10,
                        OutputTokens = 15
                    }
                }
            };
        }

        [Fact]
        public void Constructor_MissingApiKey_ThrowsConfigurationException()
        {
            // Arrange
            var credentialsWithMissingKey = new ProviderCredentials
            {
                ProviderName = "Cohere",
                ApiKey = null,
                ApiBase = DefaultApiBase
            };
            var providerModelId = "command-r";

            // Act & Assert
            var ex = Assert.Throws<ConfigurationException>(() =>
            {
                var httpClientFactory = HttpClientFactoryAdapter.AdaptHttpClient(_httpClient);
                new CohereClient(credentialsWithMissingKey, providerModelId, _loggerMock.Object, httpClientFactory);
            });

            Assert.Contains("API key is missing for provider 'cohere'", ex.Message);
        }

        [Fact]
        public async Task CreateChatCompletionAsync_Success()
        {
            // Arrange
            var request = CreateTestRequest();
            var providerModelId = "command-r";
            var expectedResponse = CreateCohereResponse();
            var expectedUrl = $"{DefaultApiBase}/{ChatEndpoint}";

            _handlerMock.SetupRequest(HttpMethod.Post, expectedUrl)
                .ReturnsResponse(HttpStatusCode.OK, JsonContent.Create(expectedResponse))
                .Verifiable();

            var httpClientFactory = HttpClientFactoryAdapter.AdaptHttpClient(_httpClient);
            var client = new CohereClient(_credentials, providerModelId, _loggerMock.Object, httpClientFactory);

            // Act
            var response = await client.CreateChatCompletionAsync(request, cancellationToken: CancellationToken.None);

            // Assert
            Assert.NotNull(response);
            // The response ID is auto-generated in the client, so we just check it's not empty
            Assert.NotEmpty(response.Id);
            Assert.Equal(request.Model, response.Model); // Should return original alias
            Assert.Equal(expectedResponse.Text, response.Choices[0].Message.Content);
            Assert.Equal("assistant", response.Choices[0].Message.Role);
            Assert.Equal("stop", response.Choices[0].FinishReason); // Mapped from COMPLETE

            // Verify token usage
            Assert.NotNull(response.Usage);
            Assert.Equal(expectedResponse.Meta?.Tokens?.InputTokens ?? 0, response.Usage.PromptTokens);
            Assert.Equal(expectedResponse.Meta?.Tokens?.OutputTokens ?? 0, response.Usage.CompletionTokens);
            Assert.Equal(
                (expectedResponse.Meta?.Tokens?.InputTokens ?? 0) + (expectedResponse.Meta?.Tokens?.OutputTokens ?? 0),
                response.Usage.TotalTokens);

            // Verify request was properly formatted
            _handlerMock.Protected()
                .Verify("SendAsync", Times.Once(), Moq.Protected.ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri != null && req.RequestUri.ToString() == expectedUrl &&
                    req.Headers.Authorization != null && req.Headers.Authorization.Scheme == "Bearer" &&
                    req.Headers.Authorization.Parameter == _credentials.ApiKey),
                    Moq.Protected.ItExpr.IsAny<CancellationToken>());
        }

        [Fact]
        public async Task CreateChatCompletionAsync_ApiReturnsError_ThrowsLLMCommunicationException()
        {
            // Arrange
            var request = CreateTestRequest();
            var providerModelId = "command-r";
            var expectedUrl = $"{DefaultApiBase}/{ChatEndpoint}";
            var errorMessage = "Invalid request: message is required.";
            var errorResponse = new TestHelperMocks.CohereErrorResponse { Message = errorMessage };

            _handlerMock.SetupRequest(HttpMethod.Post, expectedUrl)
                .ReturnsResponse(HttpStatusCode.BadRequest, JsonContent.Create(errorResponse))
                .Verifiable();

            var httpClientFactory = HttpClientFactoryAdapter.AdaptHttpClient(_httpClient);
            var client = new CohereClient(_credentials, providerModelId, _loggerMock.Object, httpClientFactory);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<LLMCommunicationException>(() =>
                client.CreateChatCompletionAsync(request, cancellationToken: CancellationToken.None));

            // Verify error message contains Cohere's error
            Assert.Contains(errorMessage, ex.Message);

            // Verify request was sent
            _handlerMock.Protected()
                .Verify("SendAsync", Times.Once(), Moq.Protected.ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri != null && req.RequestUri.ToString() == expectedUrl),
                    Moq.Protected.ItExpr.IsAny<CancellationToken>());
        }

        [Fact]
        public async Task StreamChatCompletionAsync_Success()
        {
            // Arrange
            var request = CreateTestRequest();
            request.Stream = true;
            var providerModelId = "command-r";
            var expectedUrl = $"{DefaultApiBase}/{ChatEndpoint}";

            // Create sample stream responses
            var streamLines = new List<string>
            {
                JsonSerializer.Serialize(new { event_type = "stream-start", generation_id = "gen-12345" }),
                JsonSerializer.Serialize(new { event_type = "text-generation", text = "Hello" }),
                JsonSerializer.Serialize(new { event_type = "text-generation", text = " there!" }),
                JsonSerializer.Serialize(new {
                    event_type = "stream-end",
                    finish_reason = "COMPLETE",
                    response = new {
                        text = "Hello there!",
                        generation_id = "gen-12345",
                        finish_reason = "COMPLETE",
                        meta = new {
                            tokens = new {
                                input_tokens = 10,
                                output_tokens = 2
                            }
                        }
                    }
                })
            };

            // Create a stream response
            var streamContent = new StreamResponse(streamLines);
            _handlerMock.SetupRequest(HttpMethod.Post, expectedUrl)
                .ReturnsResponse(HttpStatusCode.OK, streamContent)
                .Verifiable();

            var httpClientFactory = HttpClientFactoryAdapter.AdaptHttpClient(_httpClient);
            var client = new CohereClient(_credentials, providerModelId, _loggerMock.Object, httpClientFactory);

            // Act
            var chunks = new List<ChatCompletionChunk>();
            await foreach (var chunk in client.StreamChatCompletionAsync(request, cancellationToken: CancellationToken.None))
            {
                chunks.Add(chunk);
            }

            // Assert
            Assert.NotEmpty(chunks);
            Assert.Equal(3, chunks.Count); // Two text chunks and one final chunk with finish reason

            // First chunk should have role assistant
            Assert.Equal("assistant", chunks[0].Choices[0].Delta.Role);
            Assert.Equal("Hello", chunks[0].Choices[0].Delta.Content);

            // Second chunk should only have content
            Assert.Null(chunks[1].Choices[0].Delta.Role);
            Assert.Equal(" there!", chunks[1].Choices[0].Delta.Content);

            // Last chunk should have finish reason
            // The content can be empty string or null depending on implementation
            Assert.True(string.IsNullOrEmpty(chunks[2].Choices[0].Delta.Content));
            Assert.Equal("stop", chunks[2].Choices[0].FinishReason);

            // Verify request was sent
            _handlerMock.Protected()
                .Verify("SendAsync", Times.Once(), Moq.Protected.ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri != null && req.RequestUri.ToString() == expectedUrl),
                    Moq.Protected.ItExpr.IsAny<CancellationToken>());
        }

        [Fact]
        public async Task GetModelsAsync_ReturnsKnownCohereModels()
        {
            // Arrange
            var providerModelId = "command-r";
            var httpClientFactory = HttpClientFactoryAdapter.AdaptHttpClient(_httpClient);
            var client = new CohereClient(_credentials, providerModelId, _loggerMock.Object, httpClientFactory);

            // Act
            var models = await client.GetModelsAsync();

            // Assert
            Assert.NotNull(models);
            Assert.NotEmpty(models);

            // Verify it contains some expected Cohere models
            Assert.Contains(models, m => m.Id == "command-r");
            Assert.Contains(models, m => m.Id == "command-r-plus");
            Assert.Contains(models, m => m.Provider == "cohere");

            // Verify no HTTP requests were made (static list)
            _handlerMock.Protected().Verify("SendAsync", Times.Never(),
                Moq.Protected.ItExpr.IsAny<HttpRequestMessage>(),
                Moq.Protected.ItExpr.IsAny<CancellationToken>());
        }

        [Fact]
        public async Task CreateEmbeddingAsync_Success()
        {
            // Arrange
            var providerModelId = "embed-english-v3.0";
            var embeddingRequest = new ConduitLLM.Core.Models.EmbeddingRequest
            {
                Model = "embed-model",
                Input = new[] { "Test text" },
                EncodingFormat = "float"
            };
            
            var expectedUrl = $"{DefaultApiBase}/embed";
            var cohereResponse = new
            {
                embeddings = new List<List<double>> { new List<double> { 0.1, 0.2, 0.3 } },
                id = "embed-123",
                meta = new { billed_units = new { input_tokens = 3 } }
            };

            _handlerMock.SetupRequest(HttpMethod.Post, expectedUrl)
                .ReturnsResponse(HttpStatusCode.OK, JsonContent.Create(cohereResponse))
                .Verifiable();

            var httpClientFactory = HttpClientFactoryAdapter.AdaptHttpClient(_httpClient);
            var client = new CohereClient(_credentials, providerModelId, _loggerMock.Object, httpClientFactory);

            // Act
            var response = await client.CreateEmbeddingAsync(embeddingRequest);

            // Assert
            Assert.NotNull(response);
            Assert.Single(response.Data);
            Assert.Equal(3, response.Data[0].Embedding.Count);
            Assert.Equal(0.1, response.Data[0].Embedding[0], 0.0001);
            Assert.Equal(0.2, response.Data[0].Embedding[1], 0.0001);
            Assert.Equal(0.3, response.Data[0].Embedding[2], 0.0001);
            Assert.Equal(3, response.Usage.TotalTokens);

            // Verify request was sent
            _handlerMock.Protected()
                .Verify("SendAsync", Times.Once(), Moq.Protected.ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri != null && req.RequestUri.ToString() == expectedUrl),
                    Moq.Protected.ItExpr.IsAny<CancellationToken>());
        }

        [Fact]
        public async Task CreateImageAsync_NotSupported_ThrowsNotSupportedException()
        {
            // Arrange
            var providerModelId = "command-r";
            var httpClientFactory = HttpClientFactoryAdapter.AdaptHttpClient(_httpClient);
            var client = new CohereClient(_credentials, providerModelId, _loggerMock.Object, httpClientFactory);
            var imageRequest = new ConduitLLM.Core.Models.ImageGenerationRequest
            {
                Model = "command-r",
                Prompt = "A cat"
            };

            // Act & Assert
            await Assert.ThrowsAsync<NotSupportedException>(() =>
                client.CreateImageAsync(imageRequest));
        }

        // Helper class to create a stream response for testing
        private class StreamResponse : HttpContent
        {
            private readonly List<string> _streamLines;

            public StreamResponse(List<string> streamLines)
            {
                _streamLines = streamLines;
            }

            protected override async Task SerializeToStreamAsync(System.IO.Stream stream, TransportContext? context)
            {
                using var writer = new System.IO.StreamWriter(stream, Encoding.UTF8, leaveOpen: true);
                foreach (var line in _streamLines)
                {
                    await writer.WriteLineAsync(line);
                    await writer.FlushAsync();
                }
            }

            protected override bool TryComputeLength(out long length)
            {
                length = 0;
                return false;
            }
        }
    }
}
