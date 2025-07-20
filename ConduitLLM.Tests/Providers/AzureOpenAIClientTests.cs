using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Providers;
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
    /// Unit tests for the AzureOpenAIClient class.
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Component", "Provider")]
    public class AzureOpenAIClientTests
    {
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
        private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
        private readonly HttpClient _httpClient;
        private readonly Mock<IMemoryCache> _mockCache;
        private readonly Mock<ILogger> _mockLogger;
        private readonly Mock<ITokenCounter> _mockTokenCounter;
        private readonly AzureOpenAIClient _client;
        private readonly ITestOutputHelper _output;

        public AzureOpenAIClientTests(ITestOutputHelper output)
        {
            _output = output;
            _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_mockHttpMessageHandler.Object)
            {
                BaseAddress = new Uri("https://test.openai.azure.com/")
            };
            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _mockHttpClientFactory.Setup(x => x.CreateClient("AzureOpenAI")).Returns(_httpClient);
            
            _mockCache = new Mock<IMemoryCache>();
            _mockLogger = new Mock<ILogger>();
            _mockTokenCounter = new Mock<ITokenCounter>();
            
            // Setup default token counting
            _mockTokenCounter.Setup(x => x.CountTokens(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(10);
            _mockTokenCounter.Setup(x => x.CountMessageTokens(It.IsAny<List<Message>>(), It.IsAny<string>()))
                .Returns(20);

            _client = new AzureOpenAIClient(
                _mockHttpClientFactory.Object,
                _mockCache.Object,
                _mockLogger.Object,
                _mockTokenCounter.Object);
        }

        #region Authentication Tests

        [Fact]
        public void SetAuthentication_WithValidCredentials_ShouldSetApiKey()
        {
            // Arrange
            var credentials = new ProviderCredentials
            {
                ApiKey = "test-azure-key",
                ApiBase = "https://myresource.openai.azure.com",
                DeploymentId = "gpt-4-deployment"
            };

            // Act
            _client.SetAuthentication(credentials);

            // Assert
            _httpClient.DefaultRequestHeaders.Should().ContainKey("api-key");
            _httpClient.BaseAddress!.ToString().Should().Be("https://myresource.openai.azure.com/");
        }

        [Fact]
        public void SetAuthentication_WithNullCredentials_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => _client.SetAuthentication(null!));
        }

        [Fact]
        public void SetAuthentication_WithEmptyApiKey_ShouldThrowArgumentException()
        {
            // Arrange
            var credentials = new ProviderCredentials
            {
                ApiKey = "",
                ApiBase = "https://test.openai.azure.com"
            };

            // Act & Assert
            Assert.Throws<ArgumentException>(() => _client.SetAuthentication(credentials));
        }

        #endregion

        #region Chat Completion Tests

        [Fact]
        public async Task CompleteAsync_WithValidRequest_ShouldReturnResponse()
        {
            // Arrange
            var request = new ChatCompletionRequest
            {
                Model = "gpt-4",
                Messages = new List<Message>
                {
                    new() { Role = "user", Content = "Hello Azure!" }
                },
                Temperature = 0.7
            };

            var responseContent = new
            {
                id = "chatcmpl-123",
                @object = "chat.completion",
                created = 1677652288,
                model = "gpt-4",
                choices = new[]
                {
                    new
                    {
                        index = 0,
                        message = new { role = "assistant", content = "Hello from Azure OpenAI!" },
                        finish_reason = "stop"
                    }
                },
                usage = new
                {
                    prompt_tokens = 10,
                    completion_tokens = 15,
                    total_tokens = 25
                }
            };

            SetupHttpResponse(HttpStatusCode.OK, responseContent);

            // Act
            var response = await _client.CompleteAsync(request);

            // Assert
            response.Should().NotBeNull();
            response.Choices.Should().HaveCount(1);
            response.Choices[0].Message.Content.Should().Be("Hello from Azure OpenAI!");
            response.Usage.TotalTokens.Should().Be(25);
            response.Model.Should().Be("gpt-4");

            // Verify API version was included
            VerifyHttpRequest(req =>
                req.RequestUri!.Query.Contains("api-version="));
        }

        [Fact]
        public async Task CompleteAsync_WithSystemMessage_ShouldIncludeInRequest()
        {
            // Arrange
            var request = new ChatCompletionRequest
            {
                Model = "gpt-4",
                Messages = new List<Message>
                {
                    new() { Role = "system", Content = "You are a helpful assistant." },
                    new() { Role = "user", Content = "What is Azure?" }
                }
            };

            var responseContent = CreateChatCompletionResponse("Azure is Microsoft's cloud platform.");
            SetupHttpResponse(HttpStatusCode.OK, responseContent);

            // Act
            var response = await _client.CompleteAsync(request);

            // Assert
            response.Choices[0].Message.Content.Should().Contain("Azure is Microsoft's cloud platform");
            
            // Verify system message was sent
            VerifyHttpRequest(req =>
            {
                var content = GetRequestContent<dynamic>(req);
                var messages = content.messages as IEnumerable<dynamic>;
                messages!.Should().HaveCount(2);
                messages!.First().role.Should().Be("system");
            });
        }

        [Fact]
        public async Task CompleteAsync_WithFunctionCalling_ShouldIncludeTools()
        {
            // Arrange
            var request = new ChatCompletionRequest
            {
                Model = "gpt-4",
                Messages = new List<Message>
                {
                    new() { Role = "user", Content = "What's the weather?" }
                },
                Tools = new List<Tool>
                {
                    new()
                    {
                        Type = "function",
                        Function = new FunctionDefinition
                        {
                            Name = "get_weather",
                            Description = "Get weather information",
                            Parameters = new
                            {
                                type = "object",
                                properties = new
                                {
                                    location = new { type = "string" }
                                }
                            }
                        }
                    }
                }
            };

            var responseContent = new
            {
                id = "chatcmpl-123",
                @object = "chat.completion",
                created = 1677652288,
                model = "gpt-4",
                choices = new[]
                {
                    new
                    {
                        index = 0,
                        message = new
                        {
                            role = "assistant",
                            content = (string?)null,
                            tool_calls = new[]
                            {
                                new
                                {
                                    id = "call_123",
                                    type = "function",
                                    function = new
                                    {
                                        name = "get_weather",
                                        arguments = "{\"location\": \"Seattle\"}"
                                    }
                                }
                            }
                        },
                        finish_reason = "tool_calls"
                    }
                },
                usage = new { prompt_tokens = 20, completion_tokens = 10, total_tokens = 30 }
            };

            SetupHttpResponse(HttpStatusCode.OK, responseContent);

            // Act
            var response = await _client.CompleteAsync(request);

            // Assert
            response.Choices[0].Message.ToolCalls.Should().HaveCount(1);
            response.Choices[0].Message.ToolCalls![0].Function.Name.Should().Be("get_weather");
            response.Choices[0].FinishReason.Should().Be("tool_calls");
        }

        [Fact]
        public async Task CompleteAsync_WithMaxTokens_ShouldSetInRequest()
        {
            // Arrange
            var request = new ChatCompletionRequest
            {
                Model = "gpt-4",
                Messages = new List<Message>
                {
                    new() { Role = "user", Content = "Tell me a story" }
                },
                MaxTokens = 100
            };

            var responseContent = CreateChatCompletionResponse("Once upon a time...");
            SetupHttpResponse(HttpStatusCode.OK, responseContent);

            // Act
            await _client.CompleteAsync(request);

            // Assert
            VerifyHttpRequest(req =>
            {
                var content = GetRequestContent<dynamic>(req);
                ((int)content.max_tokens).Should().Be(100);
            });
        }

        #endregion

        #region Streaming Tests

        [Fact]
        public async Task StreamCompletionAsync_WithValidRequest_ShouldStreamChunks()
        {
            // Arrange
            var request = new ChatCompletionRequest
            {
                Model = "gpt-4",
                Messages = new List<Message>
                {
                    new() { Role = "user", Content = "Stream this response" }
                },
                Stream = true
            };

            var streamContent = @"data: {""id"":""1"",""object"":""chat.completion.chunk"",""created"":1234567890,""model"":""gpt-4"",""choices"":[{""index"":0,""delta"":{""content"":""Hello ""},""finish_reason"":null}]}

data: {""id"":""1"",""object"":""chat.completion.chunk"",""created"":1234567890,""model"":""gpt-4"",""choices"":[{""index"":0,""delta"":{""content"":""Azure!""},""finish_reason"":null}]}

data: {""id"":""1"",""object"":""chat.completion.chunk"",""created"":1234567890,""model"":""gpt-4"",""choices"":[{""index"":0,""delta"":{},""finish_reason"":""stop""}]}

data: [DONE]
";

            SetupStreamingResponse(streamContent);

            // Act
            var chunks = new List<ChatCompletionChunk>();
            await foreach (var chunk in _client.StreamCompletionAsync(request))
            {
                chunks.Add(chunk);
            }

            // Assert
            chunks.Should().HaveCount(3);
            chunks[0].Choices[0].Delta.Content.Should().Be("Hello ");
            chunks[1].Choices[0].Delta.Content.Should().Be("Azure!");
            chunks[2].Choices[0].FinishReason.Should().Be("stop");
        }

        [Fact]
        public async Task StreamCompletionAsync_WithFunctionCall_ShouldStreamToolCalls()
        {
            // Arrange
            var request = new ChatCompletionRequest
            {
                Model = "gpt-4",
                Messages = new List<Message>
                {
                    new() { Role = "user", Content = "What's the weather?" }
                },
                Tools = new List<Tool>
                {
                    new()
                    {
                        Type = "function",
                        Function = new FunctionDefinition
                        {
                            Name = "get_weather",
                            Description = "Get weather"
                        }
                    }
                },
                Stream = true
            };

            var streamContent = @"data: {""id"":""1"",""choices"":[{""index"":0,""delta"":{""tool_calls"":[{""index"":0,""id"":""call_123"",""type"":""function"",""function"":{""name"":""get_weather"",""arguments"":""""}}]}}]}

data: {""id"":""1"",""choices"":[{""index"":0,""delta"":{""tool_calls"":[{""index"":0,""function"":{""arguments"":""{""""location"""": """"Seattle""""}""}}]}}]}

data: {""id"":""1"",""choices"":[{""index"":0,""delta"":{},""finish_reason"":""tool_calls""}]}

data: [DONE]
";

            SetupStreamingResponse(streamContent);

            // Act
            var chunks = new List<ChatCompletionChunk>();
            await foreach (var chunk in _client.StreamCompletionAsync(request))
            {
                chunks.Add(chunk);
            }

            // Assert
            chunks.Should().HaveCount(3);
            chunks[0].Choices[0].Delta.ToolCalls.Should().NotBeNull();
            chunks[0].Choices[0].Delta.ToolCalls![0].Function.Name.Should().Be("get_weather");
            chunks[2].Choices[0].FinishReason.Should().Be("tool_calls");
        }

        #endregion

        #region Embeddings Tests

        [Fact]
        public async Task CreateEmbeddingAsync_WithValidRequest_ShouldReturnEmbedding()
        {
            // Arrange
            var request = new EmbeddingRequest
            {
                Model = "text-embedding-ada-002",
                Input = "Test embedding input"
            };

            var responseContent = new
            {
                @object = "list",
                data = new[]
                {
                    new
                    {
                        @object = "embedding",
                        index = 0,
                        embedding = new[] { 0.1, 0.2, 0.3, 0.4, 0.5 }
                    }
                },
                model = "text-embedding-ada-002-v2",
                usage = new
                {
                    prompt_tokens = 5,
                    total_tokens = 5
                }
            };

            SetupHttpResponse(HttpStatusCode.OK, responseContent);

            // Act
            var response = await _client.CreateEmbeddingAsync(request);

            // Assert
            response.Should().NotBeNull();
            response.Data.Should().HaveCount(1);
            response.Data[0].Embedding.Should().HaveCount(5);
            response.Data[0].Embedding[0].Should().Be(0.1f);
            response.Usage.TotalTokens.Should().Be(5);
        }

        [Fact]
        public async Task CreateEmbeddingAsync_WithMultipleInputs_ShouldReturnMultipleEmbeddings()
        {
            // Arrange
            var request = new EmbeddingRequest
            {
                Model = "text-embedding-ada-002",
                Input = new List<string> { "First text", "Second text" }
            };

            var responseContent = new
            {
                @object = "list",
                data = new[]
                {
                    new
                    {
                        @object = "embedding",
                        index = 0,
                        embedding = new[] { 0.1, 0.2, 0.3 }
                    },
                    new
                    {
                        @object = "embedding",
                        index = 1,
                        embedding = new[] { 0.4, 0.5, 0.6 }
                    }
                },
                model = "text-embedding-ada-002-v2",
                usage = new { prompt_tokens = 10, total_tokens = 10 }
            };

            SetupHttpResponse(HttpStatusCode.OK, responseContent);

            // Act
            var response = await _client.CreateEmbeddingAsync(request);

            // Assert
            response.Data.Should().HaveCount(2);
            response.Data[0].Index.Should().Be(0);
            response.Data[1].Index.Should().Be(1);
        }

        #endregion

        #region Audio Tests

        [Fact]
        public async Task TranscribeAudioAsync_WithValidRequest_ShouldReturnTranscription()
        {
            // Arrange
            var request = new AudioTranscriptionRequest
            {
                Model = "whisper-1",
                File = new byte[] { 0x00, 0x01, 0x02 },
                FileName = "audio.mp3",
                Language = "en"
            };

            var responseContent = new
            {
                text = "This is the transcribed text from Azure."
            };

            SetupHttpResponse(HttpStatusCode.OK, responseContent);

            // Act
            var response = await _client.TranscribeAudioAsync(request);

            // Assert
            response.Should().NotBeNull();
            response.Text.Should().Be("This is the transcribed text from Azure.");
            
            // Verify multipart form data was sent
            VerifyHttpRequest(req =>
                req.Content.Should().BeOfType<MultipartFormDataContent>());
        }

        [Fact]
        public async Task CreateSpeechAsync_WithValidRequest_ShouldReturnAudioData()
        {
            // Arrange
            var request = new TextToSpeechRequest
            {
                Model = "tts-1",
                Input = "Hello Azure Speech!",
                Voice = "alloy",
                ResponseFormat = "mp3"
            };

            var audioData = new byte[] { 0xFF, 0xFB, 0x90, 0x00 }; // MP3 header
            SetupHttpResponse(HttpStatusCode.OK, audioData, "audio/mpeg");

            // Act
            var response = await _client.CreateSpeechAsync(request);

            // Assert
            response.Should().NotBeNull();
            response.AudioData.Should().BeEquivalentTo(audioData);
            response.ContentType.Should().Be("audio/mpeg");
        }

        #endregion

        #region Image Generation Tests

        [Fact]
        public async Task GenerateImageAsync_WithValidRequest_ShouldReturnImages()
        {
            // Arrange
            var request = new ImageGenerationRequest
            {
                Model = "dall-e-3",
                Prompt = "A beautiful sunset over mountains",
                Size = "1024x1024",
                Quality = "hd",
                N = 1
            };

            var responseContent = new
            {
                created = 1677652288,
                data = new[]
                {
                    new
                    {
                        url = "https://example.com/image1.png",
                        revised_prompt = "A stunning sunset over mountain peaks"
                    }
                }
            };

            SetupHttpResponse(HttpStatusCode.OK, responseContent);

            // Act
            var response = await _client.GenerateImageAsync(request);

            // Assert
            response.Should().NotBeNull();
            response.Data.Should().HaveCount(1);
            response.Data[0].Url.Should().Be("https://example.com/image1.png");
            response.Data[0].RevisedPrompt.Should().Be("A stunning sunset over mountain peaks");
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public async Task CompleteAsync_WithRateLimitError_ShouldThrowWithRetryAfter()
        {
            // Arrange
            var request = new ChatCompletionRequest
            {
                Model = "gpt-4",
                Messages = new List<Message> { new() { Role = "user", Content = "Test" } }
            };

            var errorResponse = new
            {
                error = new
                {
                    message = "Rate limit exceeded",
                    type = "rate_limit_error",
                    code = "rate_limit_exceeded"
                }
            };

            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Content = new StringContent(JsonSerializer.Serialize(errorResponse)),
                Headers = { RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(60)) }
            };

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(response);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<HttpRequestException>(() => 
                _client.CompleteAsync(request));
            
            ex.Message.Should().Contain("Rate limit exceeded");
        }

        [Fact]
        public async Task CompleteAsync_WithInvalidApiKey_ShouldThrowUnauthorized()
        {
            // Arrange
            var request = new ChatCompletionRequest
            {
                Model = "gpt-4",
                Messages = new List<Message> { new() { Role = "user", Content = "Test" } }
            };

            var errorResponse = new
            {
                error = new
                {
                    message = "Invalid API key provided",
                    type = "invalid_request_error",
                    code = "invalid_api_key"
                }
            };

            SetupHttpResponse(HttpStatusCode.Unauthorized, errorResponse);

            // Act & Assert
            await Assert.ThrowsAsync<HttpRequestException>(() => 
                _client.CompleteAsync(request));
        }

        #endregion

        #region Azure-Specific Features Tests

        [Fact]
        public async Task CompleteAsync_WithDeploymentId_ShouldUseDeploymentInUrl()
        {
            // Arrange
            var credentials = new ProviderCredentials
            {
                ApiKey = "test-key",
                ApiBase = "https://myresource.openai.azure.com",
                DeploymentId = "my-gpt4-deployment"
            };
            
            _client.SetAuthentication(credentials);

            var request = new ChatCompletionRequest
            {
                Model = "gpt-4", // This will be overridden by deployment
                Messages = new List<Message> { new() { Role = "user", Content = "Test" } }
            };

            var responseContent = CreateChatCompletionResponse("Response");
            SetupHttpResponse(HttpStatusCode.OK, responseContent);

            // Act
            await _client.CompleteAsync(request);

            // Assert
            VerifyHttpRequest(req =>
                req.RequestUri!.AbsolutePath.Should().Contain("/deployments/my-gpt4-deployment/"));
        }

        [Fact]
        public async Task GetCapabilities_ShouldReturnAzureSpecificCapabilities()
        {
            // Act
            var capabilities = await _client.GetCapabilitiesAsync();

            // Assert
            capabilities.Features.Streaming.Should().BeTrue();
            capabilities.Features.Embeddings.Should().BeTrue();
            capabilities.Features.ImageGeneration.Should().BeTrue();
            capabilities.Features.AudioTranscription.Should().BeTrue();
            capabilities.Features.TextToSpeech.Should().BeTrue();
            capabilities.Features.FunctionCalling.Should().BeTrue();
            capabilities.ChatParameters.MaxTokens.Should().BeTrue();
            capabilities.ChatParameters.Temperature.Should().BeTrue();
        }

        #endregion

        #region Helper Methods

        private void SetupHttpResponse(HttpStatusCode statusCode, object responseContent)
        {
            var response = new HttpResponseMessage(statusCode)
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

        private void SetupHttpResponse(HttpStatusCode statusCode, byte[] responseContent, string contentType)
        {
            var response = new HttpResponseMessage(statusCode)
            {
                Content = new ByteArrayContent(responseContent)
            };
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(response);
        }

        private void SetupStreamingResponse(string streamContent)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(streamContent, Encoding.UTF8, "text/event-stream")
            };

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(response);
        }

        private void VerifyHttpRequest(Action<HttpRequestMessage> verification)
        {
            _mockHttpMessageHandler.Protected()
                .Verify(
                    "SendAsync",
                    Times.Once(),
                    ItExpr.Is<HttpRequestMessage>(req => VerifyRequest(req, verification)),
                    ItExpr.IsAny<CancellationToken>());
        }

        private bool VerifyRequest(HttpRequestMessage request, Action<HttpRequestMessage> verification)
        {
            verification(request);
            return true;
        }

        private T GetRequestContent<T>(HttpRequestMessage request)
        {
            var content = request.Content!.ReadAsStringAsync().Result;
            return JsonSerializer.Deserialize<T>(content)!;
        }

        private object CreateChatCompletionResponse(string content)
        {
            return new
            {
                id = "chatcmpl-123",
                @object = "chat.completion",
                created = 1677652288,
                model = "gpt-4",
                choices = new[]
                {
                    new
                    {
                        index = 0,
                        message = new { role = "assistant", content },
                        finish_reason = "stop"
                    }
                },
                usage = new
                {
                    prompt_tokens = 10,
                    completion_tokens = 15,
                    total_tokens = 25
                }
            };
        }

        #endregion
    }
}