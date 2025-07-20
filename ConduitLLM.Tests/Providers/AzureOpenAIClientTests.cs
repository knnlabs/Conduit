using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Models.Audio;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Providers;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;
using Xunit.Abstractions;
using ConduitLLM.Tests.TestHelpers;
using ConduitLLM.Configuration;

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
            _httpClient = new HttpClient(_mockHttpMessageHandler.Object);
            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _mockHttpClientFactory.Setup(x => x.CreateClient("azureLLMClient")).Returns(_httpClient);
            
            _mockCache = new Mock<IMemoryCache>();
            _mockLogger = new Mock<ILogger>();
            _mockTokenCounter = new Mock<ITokenCounter>();
            
            // Setup default token counting
            _mockTokenCounter.SetupDefaultTokenCounting(10, 20);

            var credentials = new ProviderCredentials
            {
                ProviderName = "azure",
                ApiKey = "test-key",
                ApiBase = "https://test.openai.azure.com",
                ApiVersion = "2023-05-15"
            };

            _client = new AzureOpenAIClient(
                credentials,
                "test-deployment",
                _mockLogger.Object,
                _mockHttpClientFactory.Object);
        }

        #region Authentication Tests

        [Fact]
        public void SetAuthentication_WithValidCredentials_ShouldSetApiKey()
        {
            // Arrange
            var credentials = new ProviderCredentials
            {
                ProviderName = "azure",
                ApiKey = "test-azure-key",
                ApiBase = "https://myresource.openai.azure.com",
                ApiVersion = "2023-05-15"
            };

            // Act
            // Authentication is set in constructor, so we need to create a new client
            var client = new AzureOpenAIClient(
                credentials,
                "gpt-4-deployment",
                _mockLogger.Object,
                _mockHttpClientFactory.Object);

            // Assert
            // Client was created successfully with the credentials
            client.Should().NotBeNull();
        }

        [Fact]
        public void SetAuthentication_WithNullCredentials_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new AzureOpenAIClient(
                null!,
                "test-deployment",
                _mockLogger.Object,
                _mockHttpClientFactory.Object));
        }

        [Fact]
        public void SetAuthentication_WithEmptyApiKey_ShouldThrowArgumentException()
        {
            // Arrange
            var credentials = new ProviderCredentials
            {
                ProviderName = "azure",
                ApiKey = "",
                ApiBase = "https://test.openai.azure.com",
                ApiVersion = "2023-05-15"
            };

            // Act & Assert
            Assert.Throws<ConfigurationException>(() => new AzureOpenAIClient(
                credentials,
                "test-deployment",
                _mockLogger.Object,
                _mockHttpClientFactory.Object));
        }

        #endregion

        #region Chat Completion Tests

        [Fact]
        public async Task CreateChatCompletionAsync_WithValidRequest_ShouldReturnResponse()
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
            var response = await _client.CreateChatCompletionAsync(request);

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
        public async Task CreateChatCompletionAsync_WithSystemMessage_ShouldIncludeInRequest()
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
            var response = await _client.CreateChatCompletionAsync(request);

            // Assert
            response.Choices[0].Message.Content?.ToString().Should().Contain("Azure is Microsoft's cloud platform");
            
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
        public async Task CreateChatCompletionAsync_WithFunctionCalling_ShouldIncludeTools()
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
                            Parameters = JsonTestHelpers.CreateSimpleLocationParameters()
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
            var response = await _client.CreateChatCompletionAsync(request);

            // Assert
            response.Choices[0].Message.ToolCalls.Should().HaveCount(1);
            response.Choices[0].Message.ToolCalls![0].Function.Name.Should().Be("get_weather");
            response.Choices[0].FinishReason.Should().Be("tool_calls");
        }

        [Fact]
        public async Task CreateChatCompletionAsync_WithMaxTokens_ShouldSetInRequest()
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
            await _client.CreateChatCompletionAsync(request);

            // Assert
            VerifyHttpRequest(req =>
            {
                var json = req.Content!.ReadAsStringAsync().Result;
                var doc = JsonDocument.Parse(json);
                doc.RootElement.GetProperty("max_tokens").GetInt32().Should().Be(100);
            });
        }

        #endregion

        #region Streaming Tests

        [Fact]
        public async Task StreamChatCompletionAsync_WithValidRequest_ShouldStreamChunks()
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
            await foreach (var chunk in _client.StreamChatCompletionAsync(request))
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
        public async Task StreamChatCompletionAsync_WithFunctionCall_ShouldStreamToolCalls()
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
            await foreach (var chunk in _client.StreamChatCompletionAsync(request))
            {
                chunks.Add(chunk);
            }
            
            // Debug output
            _output.WriteLine($"Total chunks received: {chunks.Count}");
            for (int i = 0; i < chunks.Count; i++)
            {
                _output.WriteLine($"Chunk {i}:");
                _output.WriteLine($"  - Has ToolCalls: {chunks[i].Choices[0].Delta.ToolCalls != null}");
                if (chunks[i].Choices[0].Delta.ToolCalls != null)
                {
                    _output.WriteLine($"  - ToolCalls Count: {chunks[i].Choices[0].Delta.ToolCalls.Count}");
                    if (chunks[i].Choices[0].Delta.ToolCalls.Count > 0)
                    {
                        var tc = chunks[i].Choices[0].Delta.ToolCalls[0];
                        _output.WriteLine($"  - Function Name: {tc.Function?.Name}");
                        _output.WriteLine($"  - Function Arguments: {tc.Function?.Arguments}");
                    }
                }
                _output.WriteLine($"  - FinishReason: {chunks[i].Choices[0].FinishReason}");
            }

            // Assert
            chunks.Should().HaveCount(3);
            chunks[0].Choices[0].Delta.ToolCalls.Should().NotBeNull();
            chunks[0].Choices[0].Delta.ToolCalls![0].Function.Name.Should().Be("get_weather");
            chunks[0].Choices[0].Delta.ToolCalls![0].Function.Arguments.Should().Be("");
            
            // Second chunk should have arguments update
            chunks[1].Choices[0].Delta.ToolCalls.Should().NotBeNull();
            chunks[1].Choices[0].Delta.ToolCalls![0].Function.Arguments.Should().Be("{\"location\": \"Seattle\"}");
            
            // Third chunk should have finish reason
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
                Input = "Test embedding input",
                EncodingFormat = "float"
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
                Input = new List<string> { "First text", "Second text" },
                EncodingFormat = "float"
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

        // Audio transcription is not currently supported by AzureOpenAIClient
        //[Fact]
        //public async Task TranscribeAudioAsync_WithValidRequest_ShouldReturnTranscription()
        //{
        //    // Arrange
        //    var request = new AudioTranscriptionRequest
        //    {
        //        Model = "whisper-1",
        //        AudioData = new byte[] { 0x00, 0x01, 0x02 },
        //        FileName = "audio.mp3",
        //        Language = "en"
        //    };
        //
        //    var responseContent = new
        //    {
        //        text = "This is the transcribed text from Azure."
        //    };
        //
        //    SetupHttpResponse(HttpStatusCode.OK, responseContent);
        //
        //    // Act
        //    var response = await _client.CreateAudioTranscriptionAsync(request);
        //
        //    // Assert
        //    response.Should().NotBeNull();
        //    response.Text.Should().Be("This is the transcribed text from Azure.");
        //    
        //    // Verify multipart form data was sent
        //    VerifyHttpRequest(req =>
        //        req.Content.Should().BeOfType<MultipartFormDataContent>());
        //}

        // Audio speech generation is not currently supported by AzureOpenAIClient
        //[Fact]
        //public async Task CreateSpeechAsync_WithValidRequest_ShouldReturnAudioData()
        //{
        //    // Arrange
        //    var request = new TextToSpeechRequest
        //    {
        //        Model = "tts-1",
        //        Input = "Hello Azure Speech!",
        //        Voice = "alloy",
        //        ResponseFormat = AudioFormat.Mp3
        //    };
        //
        //    var audioData = new byte[] { 0xFF, 0xFB, 0x90, 0x00 }; // MP3 header
        //    SetupHttpResponse(HttpStatusCode.OK, audioData, "audio/mpeg");
        //
        //    // Act
        //    var response = await _client.CreateAudioSpeechAsync(request);
        //
        //    // Assert
        //    response.Should().NotBeNull();
        //    response.AudioData.Should().BeEquivalentTo(audioData);
        //    response.ContentType.Should().Be("audio/mpeg");
        //}

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
            var response = await _client.CreateImageAsync(request);

            // Assert
            response.Should().NotBeNull();
            response.Data.Should().HaveCount(1);
            response.Data[0].Url.Should().Be("https://example.com/image1.png");
            // Note: RevisedPrompt is not available in the standard ImageData model
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public async Task CreateChatCompletionAsync_WithRateLimitError_ShouldThrowWithRetryAfter()
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
            var ex = await Assert.ThrowsAsync<LLMCommunicationException>(() => 
                _client.CreateChatCompletionAsync(request));
            
            ex.Message.Should().Contain("429");
        }

        [Fact]
        public async Task CreateChatCompletionAsync_WithInvalidApiKey_ShouldThrowUnauthorized()
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
            await Assert.ThrowsAsync<LLMCommunicationException>(() => 
                _client.CreateChatCompletionAsync(request));
        }

        #endregion

        #region Azure-Specific Features Tests

        [Fact]
        public async Task CreateChatCompletionAsync_WithDeploymentId_ShouldUseDeploymentInUrl()
        {
            // Arrange
            var credentials = new ProviderCredentials
            {
                ProviderName = "azure",
                ApiKey = "test-key",
                ApiBase = "https://myresource.openai.azure.com",
                ApiVersion = "2023-05-15"
            };
            
            var client = new AzureOpenAIClient(
                credentials,
                "my-gpt4-deployment",
                _mockLogger.Object,
                _mockHttpClientFactory.Object);

            var request = new ChatCompletionRequest
            {
                Model = "gpt-4", // This will be overridden by deployment
                Messages = new List<Message> { new() { Role = "user", Content = "Test" } }
            };

            var responseContent = CreateChatCompletionResponse("Response");
            SetupHttpResponse(HttpStatusCode.OK, responseContent);

            // Act
            await client.CreateChatCompletionAsync(request);

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
            // Parse the SSE format into individual chunks
            // Split by lines first, then extract data lines
            var chunks = streamContent
                .Split('\n')
                .Where(line => line.StartsWith("data: ") && !line.Contains("[DONE]"))
                .Select(line => line.Substring(6)) // Remove "data: " prefix
                .ToList();
                
            _output?.WriteLine($"SetupStreamingResponse: Found {chunks.Count} chunks");
            for (int i = 0; i < chunks.Count; i++)
            {
                _output?.WriteLine($"  Chunk {i}: {chunks[i].Substring(0, Math.Min(100, chunks[i].Length))}...");
            }
            
            // Use the new streaming infrastructure with SSE format
            var response = StreamingTestResponseFactory.CreateOpenAIStreamingResponse(
                chunks,
                delay: TimeSpan.FromMilliseconds(50)); // Increased delay to ensure chunks are processed separately

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