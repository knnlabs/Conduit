using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Models;
using ConduitLLM.Providers;
using ConduitLLM.Providers.InternalModels;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;
using Xunit.Abstractions;
using ConduitLLM.Tests.TestHelpers;

namespace ConduitLLM.Tests.Providers
{
    /// <summary>
    /// Unit tests for the AnthropicClient class, covering Claude model interactions.
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Component", "Providers")]
    public class AnthropicClientTests : TestBase
    {
        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
        private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
        private readonly HttpClient _httpClient;

        public AnthropicClientTests(ITestOutputHelper output) : base(output)
        {
            _httpClientFactoryMock = new Mock<IHttpClientFactory>();
            _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_httpMessageHandlerMock.Object)
            {
                BaseAddress = new Uri("https://api.anthropic.com/v1/")
            };
            _httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(_httpClient);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidCredentials_InitializesCorrectly()
        {
            // Arrange
            var credentials = new ProviderCredentials
            {
                ApiKey = "test-api-key",
                ProviderType = ProviderType.Anthropic
            };
            var modelId = "claude-3-opus-20240229";
            var logger = CreateLogger<AnthropicClient>();

            // Act
            var client = new AnthropicClient(credentials, modelId, logger.Object, _httpClientFactoryMock.Object);

            // Assert
            Assert.NotNull(client);
        }

        [Fact]
        public void Constructor_WithMissingApiKey_ThrowsConfigurationException()
        {
            // Arrange
            var credentials = new ProviderCredentials
            {
                ApiKey = "", // Empty API key
                ProviderType = ProviderType.Anthropic
            };
            var modelId = "claude-3-opus-20240229";
            var logger = CreateLogger<AnthropicClient>();

            // Act & Assert
            var exception = Assert.Throws<ConfigurationException>(() =>
                new AnthropicClient(credentials, modelId, logger.Object, _httpClientFactoryMock.Object));

            Assert.Contains("API key (x-api-key) is missing", exception.Message);
        }

        [Fact]
        public void Constructor_WithNullCredentials_ThrowsException()
        {
            // Arrange
            var modelId = "claude-3-opus-20240229";
            var logger = CreateLogger<AnthropicClient>();

            // Act & Assert
            Assert.ThrowsAny<Exception>(() =>
                new AnthropicClient(null!, modelId, logger.Object, _httpClientFactoryMock.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Arrange
            var credentials = new ProviderCredentials
            {
                ApiKey = "test-key",
                ProviderType = ProviderType.Anthropic
            };
            var modelId = "claude-3-opus-20240229";

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new AnthropicClient(credentials, modelId, null!, _httpClientFactoryMock.Object));
        }

        [Fact]
        public void Constructor_WithCustomApiBase_UsesProvidedUrl()
        {
            // Arrange
            var customBaseUrl = "https://custom.anthropic.api/v1";
            var credentials = new ProviderCredentials
            {
                ApiKey = "test-key",
                BaseUrl = customBaseUrl,
                ProviderType = ProviderType.Anthropic
            };
            var modelId = "claude-3-opus-20240229";
            var logger = CreateLogger<AnthropicClient>();

            // Setup custom HttpClient with expected base address
            var customHttpClient = new HttpClient(_httpMessageHandlerMock.Object)
            {
                BaseAddress = new Uri(customBaseUrl)
            };
            _httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(customHttpClient);

            // Act
            var client = new AnthropicClient(credentials, modelId, logger.Object, _httpClientFactoryMock.Object);

            // Assert
            Assert.NotNull(client);
        }

        #endregion

        #region Chat Completion Tests

        [Fact]
        public async Task CreateChatCompletionAsync_WithValidRequest_ReturnsResponse()
        {
            // Arrange
            var client = CreateAnthropicClient();
            var request = new ChatCompletionRequest
            {
                Model = "claude-3-opus-20240229",
                Messages = new List<Message>
                {
                    new Message { Role = "user", Content = "Hello, Claude!" }
                },
                MaxTokens = 100
            };

            var expectedResponse = new AnthropicMessageResponse
            {
                Id = "msg_123",
                Type = "message",
                Role = "assistant",
                Content = new List<object> { new { type = "text", text = "Hello! How can I help you today?" } },
                Model = "claude-3-opus-20240229",
                StopReason = "stop",
                Usage = new AnthropicUsage
                {
                    InputTokens = 10,
                    OutputTokens = 20
                }
            };

            SetupHttpResponse(HttpStatusCode.OK, expectedResponse);

            // Act
            var result = await client.CreateChatCompletionAsync(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Hello! How can I help you today?", result.Choices[0].Message.Content);
            Assert.Equal("assistant", result.Choices[0].Message.Role);
            Assert.Equal(10, result.Usage.PromptTokens);
            Assert.Equal(20, result.Usage.CompletionTokens);
            Assert.Equal(30, result.Usage.TotalTokens);
        }

        [Fact]
        public async Task CreateChatCompletionAsync_WithSystemMessage_MovesToSystemPrompt()
        {
            // Arrange
            var client = CreateAnthropicClient();
            var request = new ChatCompletionRequest
            {
                Model = "claude-3-opus-20240229",
                Messages = new List<Message>
                {
                    new Message { Role = "system", Content = "You are a helpful assistant." },
                    new Message { Role = "user", Content = "Hello!" }
                },
                MaxTokens = 100
            };

            AnthropicMessageRequest? capturedRequest = null;
            _httpMessageHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Returns(async (HttpRequestMessage request, CancellationToken ct) =>
                {
                    if (request.Content != null)
                    {
                        var content = await request.Content.ReadAsStringAsync();
                        capturedRequest = JsonSerializer.Deserialize<AnthropicMessageRequest>(content);
                    }

                    return new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.OK,
                        Content = new StringContent(JsonSerializer.Serialize(new AnthropicMessageResponse
                        {
                            Id = "msg_123",
                            Type = "message",
                            Role = "assistant",
                            Content = "Hello!",
                            Model = "claude-3-opus-20240229",
                            StopReason = "stop",
                            Usage = new AnthropicUsage { InputTokens = 10, OutputTokens = 5 }
                        }))
                    };
                });

            // Act
            await client.CreateChatCompletionAsync(request);

            // Assert
            Assert.NotNull(capturedRequest);
            Assert.Equal("You are a helpful assistant.", capturedRequest.SystemPrompt);
            Assert.Single(capturedRequest.Messages); // Only user message should remain
            Assert.Equal("user", capturedRequest.Messages.First().Role);
        }

        [Fact]
        public async Task CreateChatCompletionAsync_WithMultimodalContent_HandlesImagesCorrectly()
        {
            // Arrange
            var client = CreateAnthropicClient();
            var base64Image = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";
            var request = new ChatCompletionRequest
            {
                Model = "claude-3-opus-20240229",
                Messages = new List<Message>
                {
                    new Message 
                    { 
                        Role = "user", 
                        Content = new List<object>
                        {
                            new TextContentPart { Text = "What's in this image?" },
                            new ImageUrlContentPart 
                            { 
                                ImageUrl = new ImageUrl { Url = $"data:image/png;base64,{base64Image}" }
                            }
                        }
                    }
                },
                MaxTokens = 100
            };

            AnthropicMessageRequest? capturedRequest = null;
            _httpMessageHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Returns(async (HttpRequestMessage request, CancellationToken ct) =>
                {
                    if (request.Content != null)
                    {
                        var content = await request.Content.ReadAsStringAsync();
                        capturedRequest = JsonSerializer.Deserialize<AnthropicMessageRequest>(content);
                    }

                    return new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.OK,
                        Content = new StringContent(JsonSerializer.Serialize(new AnthropicMessageResponse
                        {
                            Id = "msg_123",
                            Type = "message",
                            Role = "assistant",
                            Content = "I see a small red pixel.",
                            Model = "claude-3-opus-20240229",
                            StopReason = "stop",
                            Usage = new AnthropicUsage { InputTokens = 100, OutputTokens = 10 }
                        }))
                    };
                });

            // Act
            var result = await client.CreateChatCompletionAsync(request);

            // Assert
            Assert.NotNull(capturedRequest);
            Assert.NotNull(capturedRequest.Messages.First().Content);
            // The content should be a list with text and image blocks
            var contentJson = JsonSerializer.Serialize(capturedRequest.Messages.First().Content);
            Assert.Contains("text", contentJson);
            Assert.Contains("image", contentJson);
            Assert.Contains("base64", contentJson);
        }

        [Fact]
        public async Task CreateChatCompletionAsync_WithInvalidApiKey_ThrowsLLMCommunicationException()
        {
            // Arrange
            var client = CreateAnthropicClient();
            var request = new ChatCompletionRequest
            {
                Model = "claude-3-opus-20240229",
                Messages = new List<Message>
                {
                    new Message { Role = "user", Content = "Hello!" }
                },
                MaxTokens = 100
            };

            SetupHttpResponse(HttpStatusCode.Unauthorized, new { error = "Invalid API key" });

            // Act & Assert
            var exception = await Assert.ThrowsAsync<LLMCommunicationException>(() =>
                client.CreateChatCompletionAsync(request));

            Assert.Contains("Invalid Anthropic API key", exception.Message);
        }

        [Fact]
        public async Task CreateChatCompletionAsync_WithRateLimit_ThrowsLLMCommunicationException()
        {
            // Arrange
            var client = CreateAnthropicClient();
            var request = new ChatCompletionRequest
            {
                Model = "claude-3-opus-20240229",
                Messages = new List<Message>
                {
                    new Message { Role = "user", Content = "Hello!" }
                },
                MaxTokens = 100
            };

            SetupHttpResponse(HttpStatusCode.TooManyRequests, new { error = "Rate limit exceeded" });

            // Act & Assert
            var exception = await Assert.ThrowsAsync<LLMCommunicationException>(() =>
                client.CreateChatCompletionAsync(request));

            Assert.Contains("rate limit exceeded", exception.Message);
        }

        [Fact]
        public async Task CreateChatCompletionAsync_WithModelNotFound_ThrowsLLMCommunicationException()
        {
            // Arrange
            var client = CreateAnthropicClient();
            var request = new ChatCompletionRequest
            {
                Model = "invalid-model",
                Messages = new List<Message>
                {
                    new Message { Role = "user", Content = "Hello!" }
                },
                MaxTokens = 100
            };

            SetupHttpResponse(HttpStatusCode.NotFound, new { error = "Model not found" });

            // Act & Assert
            var exception = await Assert.ThrowsAsync<LLMCommunicationException>(() =>
                client.CreateChatCompletionAsync(request));

            Assert.Contains("Model not found", exception.Message);
        }

        // Note: Cached token mapping test would go here once AnthropicUsage model is updated
        // to include CacheCreationInputTokens and CacheReadInputTokens fields

        #endregion

        #region Streaming Tests

        [Fact]
        public async Task StreamChatCompletionAsync_WithValidRequest_StreamsResponses()
        {
            // Arrange
            var client = CreateAnthropicClient();
            var request = new ChatCompletionRequest
            {
                Model = "claude-3-opus-20240229",
                Messages = new List<Message>
                {
                    new Message { Role = "user", Content = "Tell me a story" }
                },
                MaxTokens = 100,
                Stream = true
            };

            var sseData = 
                "event: content_block_delta\n" +
                "data: {\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"text_delta\",\"text\":\"Once upon\"}}\n\n" +
                "event: content_block_delta\n" +
                "data: {\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"text_delta\",\"text\":\" a time\"}}\n\n" +
                "event: message_stop\n" +
                "data: {\"type\":\"message_stop\"}\n\n";

            SetupStreamingResponse(sseData);

            // Act
            var chunks = new List<ChatCompletionChunk>();
            await foreach (var chunk in client.StreamChatCompletionAsync(request))
            {
                chunks.Add(chunk);
            }

            // Assert
            Assert.Equal(3, chunks.Count); // Two content chunks + one stop chunk
            Assert.Equal("Once upon", chunks[0].Choices[0].Delta.Content);
            Assert.Equal(" a time", chunks[1].Choices[0].Delta.Content);
            Assert.Equal("stop", chunks[2].Choices[0].FinishReason);
        }

        [Fact]
        public async Task StreamChatCompletionAsync_WithSystemMessage_HandlesCorrectly()
        {
            // Arrange
            var client = CreateAnthropicClient();
            var request = new ChatCompletionRequest
            {
                Model = "claude-3-opus-20240229",
                Messages = new List<Message>
                {
                    new Message { Role = "system", Content = "You are a poet." },
                    new Message { Role = "user", Content = "Write a haiku" }
                },
                MaxTokens = 100,
                Stream = true
            };

            var sseData = 
                "event: content_block_delta\n" +
                "data: {\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"text_delta\",\"text\":\"Silent\"}}\n\n" +
                "event: message_stop\n" +
                "data: {\"type\":\"message_stop\"}\n\n";

            SetupStreamingResponse(sseData);

            // Act
            var chunks = new List<ChatCompletionChunk>();
            await foreach (var chunk in client.StreamChatCompletionAsync(request))
            {
                chunks.Add(chunk);
            }

            // Assert
            Assert.Equal(2, chunks.Count);
            Assert.Equal("Silent", chunks[0].Choices[0].Delta.Content);
        }

        #endregion

        #region Model Discovery Tests

        [Fact]
        public async Task GetModelsAsync_ReturnsStaticModelList()
        {
            // Arrange
            var client = CreateAnthropicClient();

            // Act
            var models = await client.GetModelsAsync();

            // Assert
            Assert.NotNull(models);
            Assert.NotEmpty(models);
            
            // Check for expected Claude models
            Assert.Contains(models, m => m.Id == "claude-3-opus-20240229");
            Assert.Contains(models, m => m.Id == "claude-3-sonnet-20240229");
            Assert.Contains(models, m => m.Id == "claude-3-haiku-20240307");
            Assert.Contains(models, m => m.Id == "claude-2.1");
            Assert.Contains(models, m => m.Id == "claude-2.0");
            Assert.Contains(models, m => m.Id == "claude-instant-1.2");

            // Check vision capabilities for Claude 3 models
            var opus = models.First(m => m.Id == "claude-3-opus-20240229");
            Assert.True(opus.Capabilities?.Vision);

            var sonnet = models.First(m => m.Id == "claude-3-sonnet-20240229");
            Assert.True(sonnet.Capabilities?.Vision);

            var haiku = models.First(m => m.Id == "claude-3-haiku-20240307");
            Assert.True(haiku.Capabilities?.Vision);

            // Check non-vision models
            var claude2 = models.First(m => m.Id == "claude-2.1");
            Assert.False(claude2.Capabilities?.Vision ?? false);
        }

        #endregion

        #region Capability Tests

        [Fact]
        public async Task GetCapabilitiesAsync_ForClaude3Model_ReturnsCorrectCapabilities()
        {
            // Arrange
            var client = CreateAnthropicClient("claude-3-opus-20240229");

            // Act
            var capabilities = await client.GetCapabilitiesAsync();

            // Assert
            Assert.NotNull(capabilities);
            Assert.Equal("anthropic", capabilities.Provider);
            Assert.Equal("claude-3-opus-20240229", capabilities.ModelId);

            // Chat parameters
            Assert.True(capabilities.ChatParameters.Temperature);
            Assert.True(capabilities.ChatParameters.MaxTokens);
            Assert.True(capabilities.ChatParameters.TopP);
            Assert.True(capabilities.ChatParameters.TopK); // Anthropic supports TopK
            Assert.True(capabilities.ChatParameters.Stop);
            Assert.False(capabilities.ChatParameters.PresencePenalty);
            Assert.False(capabilities.ChatParameters.FrequencyPenalty);
            Assert.False(capabilities.ChatParameters.LogitBias);
            Assert.False(capabilities.ChatParameters.N);
            Assert.False(capabilities.ChatParameters.Seed);
            Assert.True(capabilities.ChatParameters.Tools); // Claude models support tools

            // Features
            Assert.True(capabilities.Features.Streaming);
            Assert.False(capabilities.Features.Embeddings);
            Assert.False(capabilities.Features.ImageGeneration);
            Assert.True(capabilities.Features.VisionInput); // Claude 3 supports vision
            Assert.True(capabilities.Features.FunctionCalling);
            Assert.False(capabilities.Features.AudioTranscription);
            Assert.False(capabilities.Features.TextToSpeech);

            // Parameter constraints
            Assert.Equal(0.0, capabilities.ChatParameters.Constraints.TemperatureRange.Min);
            Assert.Equal(1.0, capabilities.ChatParameters.Constraints.TemperatureRange.Max);
            Assert.Equal(0.0, capabilities.ChatParameters.Constraints.TopPRange.Min);
            Assert.Equal(1.0, capabilities.ChatParameters.Constraints.TopPRange.Max);
            Assert.Equal(1, capabilities.ChatParameters.Constraints.TopKRange.Min);
            Assert.Equal(40, capabilities.ChatParameters.Constraints.TopKRange.Max);
            Assert.Equal(5, capabilities.ChatParameters.Constraints.MaxStopSequences);
            Assert.Equal(4096, capabilities.ChatParameters.Constraints.MaxTokenLimit);
        }

        [Fact]
        public async Task GetCapabilitiesAsync_ForClaude2Model_ReturnsCorrectCapabilities()
        {
            // Arrange
            var client = CreateAnthropicClient("claude-2.1");

            // Act
            var capabilities = await client.GetCapabilitiesAsync();

            // Assert
            Assert.NotNull(capabilities);
            Assert.False(capabilities.Features.VisionInput); // Claude 2 doesn't support vision
            Assert.True(capabilities.Features.FunctionCalling); // But still supports tools
        }

        #endregion

        #region Unsupported Operation Tests

        [Fact]
        public async Task CreateEmbeddingAsync_ThrowsNotSupportedException()
        {
            // Arrange
            var client = CreateAnthropicClient();
            var request = new EmbeddingRequest
            {
                Input = "Test text",
                Model = "text-embedding-ada-002",
                EncodingFormat = "float"
            };

            // Act & Assert
            await Assert.ThrowsAsync<NotSupportedException>(() =>
                client.CreateEmbeddingAsync(request));
        }

        [Fact]
        public async Task CreateImageAsync_ThrowsNotSupportedException()
        {
            // Arrange
            var client = CreateAnthropicClient();
            var request = new ImageGenerationRequest
            {
                Prompt = "A beautiful sunset",
                Model = "dall-e-3"
            };

            // Act & Assert
            await Assert.ThrowsAsync<NotSupportedException>(() =>
                client.CreateImageAsync(request));
        }

        #endregion

        #region Edge Case Tests

        [Fact]
        public async Task CreateChatCompletionAsync_WithNullRequest_ThrowsArgumentNullException()
        {
            // Arrange
            var client = CreateAnthropicClient();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                client.CreateChatCompletionAsync(null!));
        }


        [Fact]
        public async Task CreateChatCompletionAsync_WithToolCalls_HandlesCorrectly()
        {
            // Arrange
            var client = CreateAnthropicClient();
            var request = new ChatCompletionRequest
            {
                Model = "claude-3-opus-20240229",
                Messages = new List<Message>
                {
                    new Message 
                    { 
                        Role = "assistant",
                        Content = "Let me help you with that.",
                        ToolCalls = new List<ToolCall>
                        {
                            new ToolCall
                            {
                                Id = "tool_123",
                                Type = "function",
                                Function = new FunctionCall
                                {
                                    Name = "get_weather",
                                    Arguments = "{\"location\": \"New York\"}"
                                }
                            }
                        }
                    },
                    new Message 
                    { 
                        Role = "user",  // Tool results need to be sent as user role in Anthropic
                        Content = "Temperature: 72°F",
                        ToolCallId = "tool_123"
                    }
                },
                MaxTokens = 100
            };

            AnthropicMessageRequest? capturedRequest = null;
            _httpMessageHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Returns(async (HttpRequestMessage request, CancellationToken ct) =>
                {
                    if (request.Content != null)
                    {
                        var content = await request.Content.ReadAsStringAsync();
                        capturedRequest = JsonSerializer.Deserialize<AnthropicMessageRequest>(content);
                    }

                    return new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.OK,
                        Content = new StringContent(JsonSerializer.Serialize(new AnthropicMessageResponse
                        {
                            Id = "msg_123",
                            Type = "message",
                            Role = "assistant",
                            Content = "Based on the weather data...",
                            Model = "claude-3-opus-20240229",
                            StopReason = "stop",
                            Usage = new AnthropicUsage { InputTokens = 50, OutputTokens = 20 }
                        }))
                    };
                });

            // Act
            await client.CreateChatCompletionAsync(request);

            // Assert
            Assert.NotNull(capturedRequest);
            var contentJson = JsonSerializer.Serialize(capturedRequest.Messages);
            Assert.Contains("tool_use", contentJson);
            Assert.Contains("get_weather", contentJson);
            
            // Verify the tool call was mapped correctly
            Assert.Equal(2, capturedRequest.Messages.Count());
            
            // First message should have tool_use
            var firstMessageContent = JsonSerializer.Serialize(capturedRequest.Messages.First().Content);
            Assert.Contains("tool_use", firstMessageContent);
            
            // Second message should have the tool result
            var secondMessage = capturedRequest.Messages.Skip(1).First();
            Assert.Equal("user", secondMessage.Role);
        }

        [Fact]
        public async Task CreateChatCompletionAsync_WithMaxTokensNotSet_UsesDefault()
        {
            // Arrange
            var client = CreateAnthropicClient();
            var request = new ChatCompletionRequest
            {
                Model = "claude-3-opus-20240229",
                Messages = new List<Message>
                {
                    new Message { Role = "user", Content = "Hello!" }
                }
                // MaxTokens not set
            };

            AnthropicMessageRequest? capturedRequest = null;
            _httpMessageHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Returns(async (HttpRequestMessage request, CancellationToken ct) =>
                {
                    if (request.Content != null)
                    {
                        var content = await request.Content.ReadAsStringAsync();
                        capturedRequest = JsonSerializer.Deserialize<AnthropicMessageRequest>(content);
                    }

                    return new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.OK,
                        Content = new StringContent(JsonSerializer.Serialize(new AnthropicMessageResponse
                        {
                            Id = "msg_123",
                            Type = "message",
                            Role = "assistant",
                            Content = "Hello!",
                            Model = "claude-3-opus-20240229",
                            StopReason = "stop",
                            Usage = new AnthropicUsage { InputTokens = 10, OutputTokens = 5 }
                        }))
                    };
                });

            // Act
            await client.CreateChatCompletionAsync(request);

            // Assert
            Assert.NotNull(capturedRequest);
            Assert.Equal(4096, capturedRequest.MaxTokens); // Default value
        }


        #endregion

        #region Helper Methods

        private AnthropicClient CreateAnthropicClient(string modelId = "claude-3-opus-20240229")
        {
            var credentials = new ProviderCredentials
            {
                ApiKey = "test-api-key",
                ProviderType = ProviderType.Anthropic
            };
            var logger = CreateLogger<AnthropicClient>();

            return new AnthropicClient(
                credentials,
                modelId,
                logger.Object,
                _httpClientFactoryMock.Object);
        }

        private void SetupHttpResponse<T>(HttpStatusCode statusCode, T responseContent)
        {
            _httpMessageHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = statusCode,
                    Content = new StringContent(
                        JsonSerializer.Serialize(responseContent),
                        Encoding.UTF8,
                        "application/json")
                });
        }

        private void SetupStreamingResponse(string sseData)
        {
            _httpMessageHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(sseData, Encoding.UTF8, "text/event-stream")
                });
        }

        #endregion
    }
}