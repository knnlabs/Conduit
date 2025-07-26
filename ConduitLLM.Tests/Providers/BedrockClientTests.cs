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
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Providers;
using ConduitLLM.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;
using Xunit.Abstractions;

namespace ConduitLLM.Tests.Providers
{
    /// <summary>
    /// Unit tests for the BedrockClient class.
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Component", "Provider")]
    public class BedrockClientTests
    {
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
        private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
        private readonly HttpClient _httpClient;
        private readonly Mock<IMemoryCache> _mockCache;
        private readonly Mock<ILogger<BedrockClient>> _mockLogger;
        private readonly Mock<ITokenCounter> _mockTokenCounter;
        private readonly BedrockClient _client;
        private readonly ITestOutputHelper _output;

        public BedrockClientTests(ITestOutputHelper output)
        {
            _output = output;
            _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_mockHttpMessageHandler.Object);
            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _mockHttpClientFactory.Setup(x => x.CreateClient("bedrockLLMClient")).Returns(_httpClient);
            
            _mockCache = new Mock<IMemoryCache>();
            _mockLogger = new Mock<ILogger<BedrockClient>>();
            _mockTokenCounter = new Mock<ITokenCounter>();
            
            // Setup default token counting
            _mockTokenCounter.SetupDefaultTokenCounting(10, 20);

            var credentials = new ProviderCredentials
            {
                ProviderType = ProviderType.Bedrock,
                ApiKey = "AKIAIOSFODNN7EXAMPLE",
                ApiSecret = "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY",
                BaseUrl = "us-east-1"
            };

            _client = new BedrockClient(
                credentials,
                "anthropic.claude-3-sonnet-20240229-v1:0",
                _mockLogger.Object,
                _mockHttpClientFactory.Object);
        }

        #region Authentication Tests

        [Fact]
        public void Constructor_WithValidCredentials_ShouldConfigureClient()
        {
            // Arrange
            var credentials = new ProviderCredentials
            {
                ProviderType = ProviderType.Bedrock,
                ApiKey = "AKIAIOSFODNN7EXAMPLE",
                ApiSecret = "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY",
                BaseUrl = "us-east-1"
            };

            // Act
            var client = new BedrockClient(
                credentials,
                "anthropic.claude-3-sonnet-20240229-v1:0",
                _mockLogger.Object,
                _mockHttpClientFactory.Object);

            // Assert
            // Client should be configured properly - we can verify by making a call
            client.Should().NotBeNull();
        }

        [Fact]
        public void Constructor_WithNullCredentials_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new BedrockClient(
                null!,
                "model",
                _mockLogger.Object,
                _mockHttpClientFactory.Object));
        }

        [Fact]
        public void Constructor_WithMissingApiKey_ShouldThrowConfigurationException()
        {
            // Arrange
            var credentials = new ProviderCredentials
            {
                ProviderType = ProviderType.Bedrock,
                ApiKey = "",
                ApiSecret = "secret",
                BaseUrl = "us-east-1"
            };

            // Act & Assert
            Assert.Throws<ConfigurationException>(() => new BedrockClient(
                credentials,
                "model",
                _mockLogger.Object,
                _mockHttpClientFactory.Object));
        }

        #endregion

        #region Claude Model Tests

        [Fact]
        public async Task CompleteAsync_WithClaudeModel_ShouldReturnResponse()
        {
            // Arrange
            var request = new ChatCompletionRequest
            {
                Model = "anthropic.claude-3-sonnet-20240229-v1:0",
                Messages = new List<Message>
                {
                    new() { Role = "user", Content = "Hello Claude on Bedrock!" }
                },
                Temperature = 0.7,
                MaxTokens = 100
            };

            var claudeResponse = new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = "Hello! I'm Claude running on AWS Bedrock."
                    }
                },
                stop_reason = "end_turn",
                usage = new
                {
                    input_tokens = 10,
                    output_tokens = 15
                }
            };

            SetupBedrockResponse(request.Model, claudeResponse);

            // Act
            var response = await _client.CreateChatCompletionAsync(request);

            // Assert
            response.Should().NotBeNull();
            response.Choices.Should().HaveCount(1);
            response.Choices[0].Message.Content.Should().Be("Hello! I'm Claude running on AWS Bedrock.");
            response.Usage.PromptTokens.Should().Be(10);
            response.Usage.CompletionTokens.Should().Be(15);
            response.Model.Should().Be(request.Model);
        }

        [Fact]
        public async Task CompleteAsync_WithClaudeSystemMessage_ShouldFormatCorrectly()
        {
            // Arrange
            var request = new ChatCompletionRequest
            {
                Model = "anthropic.claude-3-opus-20240229-v1:0",
                Messages = new List<Message>
                {
                    new() { Role = "system", Content = "You are a helpful assistant." },
                    new() { Role = "user", Content = "What can you help with?" }
                },
                MaxTokens = 200
            };

            var claudeResponse = new
            {
                content = new[]
                {
                    new { type = "text", text = "I can help with many tasks!" }
                },
                stop_reason = "end_turn",
                usage = new { input_tokens = 20, output_tokens = 10 }
            };

            string? capturedRequestBody = null;
            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>(async (req, _) =>
                {
                    capturedRequestBody = await req.Content!.ReadAsStringAsync();
                })
                .ReturnsAsync(CreateInvokeModelResponse(claudeResponse));

            // Act
            await _client.CreateChatCompletionAsync(request);

            // Assert
            capturedRequestBody.Should().NotBeNull();
            var requestJson = JsonDocument.Parse(capturedRequestBody!);
            requestJson.RootElement.GetProperty("system").GetString().Should().Be("You are a helpful assistant.");
            requestJson.RootElement.GetProperty("messages").GetArrayLength().Should().Be(1); // Only user message
        }

        [Fact]
        public async Task CompleteAsync_WithClaudeVisionModel_ShouldHandleImages()
        {
            // Arrange
            var request = new ChatCompletionRequest
            {
                Model = "anthropic.claude-3-sonnet-20240229-v1:0",
                Messages = new List<Message>
                {
                    new()
                    {
                        Role = "user",
                        Content = new List<object>
                        {
                            new TextContentPart { Text = "What's in this image?" },
                            new ImageUrlContentPart 
                            { 
                                ImageUrl = new ImageUrl 
                                { 
                                    Url = "data:image/jpeg;base64,/9j/4AAQSkZJRg==" 
                                } 
                            }
                        }
                    }
                },
                MaxTokens = 150
            };

            var claudeResponse = new
            {
                content = new[]
                {
                    new { type = "text", text = "I can see an image..." }
                },
                stop_reason = "end_turn",
                usage = new { input_tokens = 50, output_tokens = 10 }
            };

            SetupBedrockResponse(request.Model, claudeResponse);

            // Act
            var response = await _client.CreateChatCompletionAsync(request);

            // Assert
            response.Choices[0].Message.Content?.ToString().Should().Contain("I can see an image");
        }

        #endregion

        #region Llama Model Tests

        [Fact]
        public async Task CompleteAsync_WithLlamaModel_ShouldReturnResponse()
        {
            // Arrange
            var request = new ChatCompletionRequest
            {
                Model = "meta.llama3-70b-instruct-v1:0",
                Messages = new List<Message>
                {
                    new() { Role = "user", Content = "Hello Llama!" }
                },
                Temperature = 0.8,
                MaxTokens = 50
            };

            var llamaResponse = new
            {
                generation = "Hello! I'm Llama 3 running on AWS Bedrock.",
                prompt_token_count = 10,
                generation_token_count = 12,
                stop_reason = "stop"
            };

            SetupBedrockResponse(request.Model, llamaResponse);

            // Act
            var response = await _client.CreateChatCompletionAsync(request);

            // Assert
            response.Should().NotBeNull();
            response.Choices[0].Message.Content.Should().Be("Hello! I'm Llama 3 running on AWS Bedrock.");
            response.Usage.PromptTokens.Should().Be(10);
            response.Usage.CompletionTokens.Should().Be(12);
        }

        [Fact]
        public async Task CompleteAsync_WithLlamaSystemPrompt_ShouldFormatCorrectly()
        {
            // Arrange
            var request = new ChatCompletionRequest
            {
                Model = "meta.llama3-8b-instruct-v1:0",
                Messages = new List<Message>
                {
                    new() { Role = "system", Content = "You are a coding assistant." },
                    new() { Role = "user", Content = "Write a Python function" }
                },
                MaxTokens = 100
            };

            string? capturedRequestBody = null;
            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>(async (req, _) =>
                {
                    capturedRequestBody = await req.Content!.ReadAsStringAsync();
                })
                .ReturnsAsync(CreateInvokeModelResponse(new { generation = "def hello():", prompt_token_count = 20, generation_token_count = 5 }));

            // Act
            await _client.CreateChatCompletionAsync(request);

            // Assert
            capturedRequestBody.Should().NotBeNull();
            var requestJson = JsonDocument.Parse(capturedRequestBody!);
            var prompt = requestJson.RootElement.GetProperty("prompt").GetString();
            prompt.Should().Contain("<s>[INST] <<SYS>>");
            prompt.Should().Contain("You are a coding assistant.");
            prompt.Should().Contain("<</SYS>>");
        }

        #endregion

        #region Mistral Model Tests

        [Fact]
        public async Task CompleteAsync_WithMistralModel_ShouldReturnResponse()
        {
            // Arrange
            var request = new ChatCompletionRequest
            {
                Model = "mistral.mistral-7b-instruct-v0:2",
                Messages = new List<Message>
                {
                    new() { Role = "user", Content = "Hello Mistral!" }
                },
                Temperature = 0.7
            };

            var mistralResponse = new
            {
                outputs = new[]
                {
                    new
                    {
                        text = "Hello! I'm Mistral AI on Bedrock.",
                        stop_reason = "stop_sequence"
                    }
                }
            };

            SetupBedrockResponse(request.Model, mistralResponse);

            // Act
            var response = await _client.CreateChatCompletionAsync(request);

            // Assert
            response.Choices[0].Message.Content.Should().Be("Hello! I'm Mistral AI on Bedrock.");
            response.Choices[0].FinishReason.Should().Be("stop");
        }

        #endregion

        #region Streaming Tests

        [Fact]
        public async Task StreamCompletionAsync_WithClaude_ShouldStreamChunks()
        {
            // Arrange
            var request = new ChatCompletionRequest
            {
                Model = "anthropic.claude-3-sonnet-20240229-v1:0",
                Messages = new List<Message>
                {
                    new() { Role = "user", Content = "Stream a response" }
                },
                Stream = true,
                MaxTokens = 100
            };

            var streamEvents = new List<object>
            {
                CreateStreamEvent("message_start", new { type = "message_start", message = new { usage = new { input_tokens = 10 } } }),
                CreateStreamEvent("content_block_start", new { type = "content_block_start", index = 0, content_block = new { type = "text", text = "" } }),
                CreateStreamEvent("content_block_delta", new { type = "content_block_delta", index = 0, delta = new { type = "text_delta", text = "Hello " } }),
                CreateStreamEvent("content_block_delta", new { type = "content_block_delta", index = 0, delta = new { type = "text_delta", text = "streaming!" } }),
                CreateStreamEvent("content_block_stop", new { type = "content_block_stop", index = 0 }),
                CreateStreamEvent("message_delta", new { type = "message_delta", delta = new { stop_reason = "end_turn" }, usage = new { output_tokens = 5 } }),
                CreateStreamEvent("message_stop", new { type = "message_stop" })
            };

            SetupBedrockStreamResponse(request.Model, streamEvents);

            // Act
            var chunks = new List<ChatCompletionChunk>();
            await foreach (var chunk in _client.StreamChatCompletionAsync(request))
            {
                chunks.Add(chunk);
            }

            // Assert
            chunks.Should().HaveCountGreaterThan(0);
            var contentChunks = chunks.Where(c => c.Choices[0].Delta.Content != null).ToList();
            contentChunks.Should().HaveCount(2);
            contentChunks[0].Choices[0].Delta.Content.Should().Be("Hello ");
            contentChunks[1].Choices[0].Delta.Content.Should().Be("streaming!");
        }

        [Fact]
        public async Task StreamCompletionAsync_WithLlama_ShouldStreamTokens()
        {
            // Arrange
            var request = new ChatCompletionRequest
            {
                Model = "meta.llama3-70b-instruct-v1:0",
                Messages = new List<Message>
                {
                    new() { Role = "user", Content = "Stream this" }
                },
                Stream = true
            };

            var streamEvents = new List<object>
            {
                CreateStreamEvent("chunk", new { generation = "First", prompt_token_count = 5 }),
                CreateStreamEvent("chunk", new { generation = " chunk", prompt_token_count = 5 }),
                CreateStreamEvent("chunk", new { generation = " done", prompt_token_count = 5, generation_token_count = 10, stop_reason = "stop" })
            };

            SetupBedrockStreamResponse(request.Model, streamEvents);

            // Act
            var chunks = new List<ChatCompletionChunk>();
            await foreach (var chunk in _client.StreamChatCompletionAsync(request))
            {
                chunks.Add(chunk);
            }

            // Assert
            chunks.Should().HaveCount(3);
            chunks[0].Choices[0].Delta.Content.Should().Be("First");
            chunks[1].Choices[0].Delta.Content.Should().Be(" chunk");
            chunks[2].Choices[0].Delta.Content.Should().Be(" done");
            chunks[2].Choices[0].FinishReason.Should().Be("stop");
        }

        #endregion

        #region Embeddings Tests

        [Fact]
        public async Task CreateEmbeddingAsync_WithTitanModel_ShouldReturnEmbedding()
        {
            // Arrange
            var request = new EmbeddingRequest
            {
                Model = "amazon.titan-embed-text-v1",
                Input = "Test embedding input",
                EncodingFormat = "float"
            };

            var titanResponse = new
            {
                embedding = Enumerable.Range(0, 1536).Select(i => i * 0.001f).ToArray(),
                inputTextTokenCount = 5
            };

            SetupBedrockResponse(request.Model, titanResponse);

            // Act
            var response = await _client.CreateEmbeddingAsync(request);

            // Assert
            response.Should().NotBeNull();
            response.Data.Should().HaveCount(1);
            response.Data[0].Embedding.Should().HaveCount(1536);
            response.Data[0].Embedding[0].Should().Be(0.0f);
            response.Usage.TotalTokens.Should().Be(5);
        }

        [Fact]
        public async Task CreateEmbeddingAsync_WithCohereModel_ShouldReturnEmbedding()
        {
            // Arrange
            var request = new EmbeddingRequest
            {
                Model = "cohere.embed-english-v3",
                Input = new List<string> { "First text", "Second text" },
                EncodingFormat = "float"
            };

            var cohereResponse = new
            {
                embeddings = new[]
                {
                    Enumerable.Range(0, 1024).Select(i => i * 0.001f).ToArray(),
                    Enumerable.Range(0, 1024).Select(i => i * 0.002f).ToArray()
                },
                id = "test-id",
                response_type = "embeddings_floats"
            };

            SetupBedrockResponse(request.Model, cohereResponse);

            // Act
            var response = await _client.CreateEmbeddingAsync(request);

            // Assert
            response.Data.Should().HaveCount(2);
            response.Data[0].Embedding.Should().HaveCount(1024);
            response.Data[1].Embedding.Should().HaveCount(1024);
            response.Data[0].Index.Should().Be(0);
            response.Data[1].Index.Should().Be(1);
        }

        #endregion

        #region Image Generation Tests

        [Fact]
        public async Task GenerateImageAsync_WithStableDiffusion_ShouldReturnImages()
        {
            // Arrange
            var request = new ImageGenerationRequest
            {
                Model = "stability.stable-diffusion-xl-v1",
                Prompt = "A futuristic city at sunset",
                N = 1,
                Size = "1024x1024",
                ResponseFormat = "b64_json"
            };

            var sdResponse = new
            {
                artifacts = new[]
                {
                    new
                    {
                        base64 = Convert.ToBase64String(new byte[] { 0xFF, 0xD8, 0xFF }),
                        seed = 123456,
                        finishReason = "SUCCESS"
                    }
                }
            };

            SetupBedrockResponse(request.Model, sdResponse);

            // Act
            var response = await _client.CreateImageAsync(request);

            // Assert
            response.Should().NotBeNull();
            response.Data.Should().HaveCount(1);
            response.Data[0].B64Json.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task GenerateImageAsync_WithStabilityAI_ShouldReturnImages()
        {
            // Arrange
            var request = new ImageGenerationRequest
            {
                Model = "stability.stable-diffusion-xl-v1",
                Prompt = "A beautiful landscape",
                N = 2,
                Size = "512x512"
            };

            var stabilityResponse = new
            {
                artifacts = new[]
                {
                    new { base64 = Convert.ToBase64String(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }) },
                    new { base64 = Convert.ToBase64String(new byte[] { 0xFF, 0xD8, 0xFF, 0xE1 }) }
                }
            };

            SetupBedrockResponse(request.Model, stabilityResponse);

            // Act
            var response = await _client.CreateImageAsync(request);

            // Assert
            response.Data.Should().HaveCount(2);
            response.Data[0].B64Json.Should().NotBeNullOrEmpty();
            response.Data[1].B64Json.Should().NotBeNullOrEmpty();
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public async Task CompleteAsync_WithThrottlingError_ShouldThrowWithRetryInfo()
        {
            // Arrange
            var request = new ChatCompletionRequest
            {
                Model = "anthropic.claude-3-sonnet-20240229-v1:0",
                Messages = new List<Message> { new() { Role = "user", Content = "Test" } }
            };

            var errorResponse = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Content = new StringContent("{\"message\":\"Too many requests\"}", Encoding.UTF8, "application/json"),
                Headers = { RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(60)) }
            };

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(errorResponse);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<LLMCommunicationException>(() => 
                _client.CreateChatCompletionAsync(request));
            
            ex.Message.Should().Contain("Too many requests");
        }

        [Fact]
        public async Task CompleteAsync_WithAccessDenied_ShouldThrowUnauthorized()
        {
            // Arrange
            var request = new ChatCompletionRequest
            {
                Model = "anthropic.claude-3-opus-20240229-v1:0",
                Messages = new List<Message> { new() { Role = "user", Content = "Test" } }
            };

            var errorResponse = new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = new StringContent("{\"message\":\"Access denied to model\"}", Encoding.UTF8, "application/json")
            };

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(errorResponse);

            // Act & Assert
            await Assert.ThrowsAsync<LLMCommunicationException>(() => 
                _client.CreateChatCompletionAsync(request));
        }

        [Fact]
        public async Task CompleteAsync_WithInvalidModel_ShouldThrowNotSupported()
        {
            // Arrange
            var request = new ChatCompletionRequest
            {
                Model = "invalid.model-v1",
                Messages = new List<Message> { new() { Role = "user", Content = "Test" } }
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<LLMCommunicationException>(() => 
                _client.CreateChatCompletionAsync(request));
            
            ex.InnerException.Should().BeOfType<UnsupportedProviderException>();
            ex.InnerException!.Message.Should().Contain("Unsupported Bedrock model");
        }

        #endregion

        #region Multi-Region Support Tests

        [Fact]
        public void Constructor_WithDifferentRegions_ShouldConfigureCorrectly()
        {
            // Test various AWS regions
            var regions = new[] { "us-east-1", "us-west-2", "eu-west-1", "ap-northeast-1" };

            foreach (var region in regions)
            {
                // Arrange
                var credentials = new ProviderCredentials
                {
                    ProviderType = ProviderType.Bedrock,
                    ApiKey = "AKIAIOSFODNN7EXAMPLE",
                    ApiSecret = "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY",
                    BaseUrl = region
                };

                // Act
                var client = new BedrockClient(
                    credentials,
                    "anthropic.claude-3-sonnet-20240229-v1:0",
                    _mockLogger.Object,
                    _mockHttpClientFactory.Object);

                // Assert
                client.Should().NotBeNull();
            }
        }

        #endregion

        #region GetCapabilities Tests

        [Fact]
        public async Task GetCapabilities_ShouldReturnBedrockCapabilities()
        {
            // Act
            var capabilities = await _client.GetCapabilitiesAsync();

            // Assert
                        capabilities.Features.Streaming.Should().BeTrue();
            capabilities.Features.Embeddings.Should().BeTrue();
            capabilities.Features.ImageGeneration.Should().BeTrue();
            capabilities.Features.FunctionCalling.Should().BeFalse(); // Most Bedrock models don't support this
            capabilities.Features.AudioTranscription.Should().BeFalse();
            capabilities.Features.TextToSpeech.Should().BeFalse();
            
            // Verify basic chat parameters are supported
            capabilities.ChatParameters.Temperature.Should().BeTrue();
            capabilities.ChatParameters.MaxTokens.Should().BeTrue();
        }

        #endregion

        #region Helper Methods

        private void SetupBedrockResponse(string modelId, object responseContent)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(responseContent), Encoding.UTF8, "application/json")
            };

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(response);
        }

        private void SetupBedrockStreamResponse(string modelId, List<object> events)
        {
            // Convert events to AWS event stream format
            var eventChunks = events.Select((e, index) => 
            {
                // Determine event type based on the data structure
                string eventType = "chunk"; // Default event type
                if (e is Dictionary<string, object> dict && dict.ContainsKey("type"))
                {
                    var type = dict["type"]?.ToString();
                    if (type != null)
                    {
                        eventType = type.Replace("_", "-"); // AWS uses hyphens
                    }
                }
                
                return StreamingTestResponseFactory.FormatBedrockEvent(eventType, e);
            });
            
            // Use the new streaming infrastructure with a small delay to simulate real streaming
            var response = StreamingTestResponseFactory.CreateBedrockStreamingResponse(
                eventChunks, 
                delay: TimeSpan.FromMilliseconds(5)); // Small delay to simulate network streaming

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(response);
        }

        private object CreateStreamEvent(string eventType, object data)
        {
            // For testing, we'll pass the data as-is since SetupBedrockStreamResponse will format it
            return data;
        }

        private HttpResponseMessage CreateInvokeModelResponse(object content)
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(content), Encoding.UTF8, "application/json")
            };
        }

        #endregion
    }
}