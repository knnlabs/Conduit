using System;
using System.Collections.Generic;
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
    /// Unit tests for the VertexAIClient class.
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Component", "Provider")]
    public class VertexAIClientTests
    {
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
        private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
        private readonly HttpClient _httpClient;
        private readonly Mock<ILogger> _mockLogger;
        private readonly ITestOutputHelper _output;

        public VertexAIClientTests(ITestOutputHelper output)
        {
            _output = output;
            _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_mockHttpMessageHandler.Object)
            {
                BaseAddress = new Uri("https://us-central1-aiplatform.googleapis.com/")
            };
            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _mockHttpClientFactory.Setup(x => x.CreateClient("VertexAI")).Returns(_httpClient);
            
            _mockLogger = new Mock<ILogger>();
        }

        #region Constructor and Authentication Tests

        [Fact]
        public void Constructor_WithValidCredentials_ShouldInitialize()
        {
            // Arrange
            var serviceAccountJson = @"{
                ""type"": ""service_account"",
                ""project_id"": ""my-project"",
                ""private_key_id"": ""key123"",
                ""private_key"": ""-----BEGIN PRIVATE KEY-----\nMIIEvQ...\n-----END PRIVATE KEY-----"",
                ""client_email"": ""test@my-project.iam.gserviceaccount.com"",
                ""client_id"": ""123456789"",
                ""auth_uri"": ""https://accounts.google.com/o/oauth2/auth"",
                ""token_uri"": ""https://oauth2.googleapis.com/token""
            }";

            var credentials = new ProviderCredentials
            {
                ApiKey = serviceAccountJson,
                ProjectId = "my-project",
                Region = "us-central1"
            };

            _mockGoogleCredential.Setup(x => x.GetAccessTokenAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync("test-access-token");

            // Act
            _client.SetAuthentication(credentials);

            // Assert
            _client.IsAuthenticated.Should().BeTrue();
            _httpClient.BaseAddress!.ToString().Should().Contain("us-central1");
            _httpClient.BaseAddress!.ToString().Should().Contain("my-project");
        }

        [Fact]
        public void SetAuthentication_WithApplicationDefaultCredentials_ShouldAuthenticate()
        {
            // Arrange
            var credentials = new ProviderCredentials
            {
                ApiKey = "APPLICATION_DEFAULT_CREDENTIALS",
                ProjectId = "my-project",
                Region = "europe-west4"
            };

            // Act
            _client.SetAuthentication(credentials);

            // Assert
            _client.IsAuthenticated.Should().BeTrue();
            _httpClient.BaseAddress!.ToString().Should().Contain("europe-west4");
        }

        [Fact]
        public void SetAuthentication_WithMissingProjectId_ShouldThrowArgumentException()
        {
            // Arrange
            var credentials = new ProviderCredentials
            {
                ApiKey = "some-key",
                ProjectId = "",
                Region = "us-central1"
            };

            // Act & Assert
            Assert.Throws<ArgumentException>(() => _client.SetAuthentication(credentials));
        }

        #endregion

        #region Gemini Model Tests

        [Fact]
        public async Task CompleteAsync_WithGeminiPro_ShouldReturnResponse()
        {
            // Arrange
            var request = new ChatCompletionRequest
            {
                Model = "gemini-1.5-pro",
                Messages = new List<Message>
                {
                    new() { Role = "user", Content = "Hello Gemini!" }
                },
                Temperature = 0.7,
                MaxTokens = 100
            };

            var geminiResponse = new
            {
                candidates = new[]
                {
                    new
                    {
                        content = new
                        {
                            parts = new[]
                            {
                                new { text = "Hello! I'm Gemini Pro on Vertex AI." }
                            },
                            role = "model"
                        },
                        finishReason = "STOP",
                        safetyRatings = new[]
                        {
                            new { category = "HARM_CATEGORY_HARASSMENT", probability = "NEGLIGIBLE" }
                        }
                    }
                },
                usageMetadata = new
                {
                    promptTokenCount = 10,
                    candidatesTokenCount = 15,
                    totalTokenCount = 25
                }
            };

            SetupHttpResponse(HttpStatusCode.OK, geminiResponse);
            SetupAuthToken();

            // Act
            var response = await _client.CompleteAsync(request);

            // Assert
            response.Should().NotBeNull();
            response.Choices.Should().HaveCount(1);
            response.Choices[0].Message.Content.Should().Be("Hello! I'm Gemini Pro on Vertex AI.");
            response.Usage.PromptTokens.Should().Be(10);
            response.Usage.CompletionTokens.Should().Be(15);
            response.Model.Should().Be(request.Model);
        }

        [Fact]
        public async Task CompleteAsync_WithGeminiSystemInstruction_ShouldFormatCorrectly()
        {
            // Arrange
            var request = new ChatCompletionRequest
            {
                Model = "gemini-1.5-flash",
                Messages = new List<Message>
                {
                    new() { Role = "system", Content = "You are a helpful coding assistant." },
                    new() { Role = "user", Content = "Write a Python function" }
                },
                MaxTokens = 200
            };

            string? capturedRequestBody = null;
            SetupHttpResponse(HttpStatusCode.OK, CreateGeminiResponse("def hello_world():"));
            SetupAuthToken();
            
            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((req, _) =>
                {
                    capturedRequestBody = req.Content?.ReadAsStringAsync().Result;
                })
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(CreateGeminiResponse("def hello():")))
                });

            // Act
            await _client.CompleteAsync(request);

            // Assert
            capturedRequestBody.Should().NotBeNull();
            var requestJson = JsonDocument.Parse(capturedRequestBody!);
            requestJson.RootElement.GetProperty("systemInstruction").GetProperty("parts")[0]
                .GetProperty("text").GetString().Should().Be("You are a helpful coding assistant.");
        }

        [Fact]
        public async Task CompleteAsync_WithGeminiVision_ShouldHandleImages()
        {
            // Arrange
            var request = new ChatCompletionRequest
            {
                Model = "gemini-1.5-pro-vision",
                Messages = new List<Message>
                {
                    new()
                    {
                        Role = "user",
                        Content = new List<ContentPart>
                        {
                            new() { Type = "text", Text = "What's in this image?" },
                            new() 
                            { 
                                Type = "image_url", 
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

            SetupHttpResponse(HttpStatusCode.OK, CreateGeminiResponse("I can see an image..."));
            SetupAuthToken();

            // Act
            var response = await _client.CompleteAsync(request);

            // Assert
            response.Choices[0].Message.Content.Should().Contain("I can see an image");
        }

        [Fact]
        public async Task CompleteAsync_WithFunctionCalling_ShouldIncludeTools()
        {
            // Arrange
            var request = new ChatCompletionRequest
            {
                Model = "gemini-1.5-pro",
                Messages = new List<Message>
                {
                    new() { Role = "user", Content = "What's the weather in Seattle?" }
                },
                Tools = new List<Tool>
                {
                    new()
                    {
                        Type = "function",
                        Function = new FunctionDefinition
                        {
                            Name = "get_weather",
                            Description = "Get weather information for a location",
                            Parameters = new
                            {
                                type = "object",
                                properties = new
                                {
                                    location = new { type = "string", description = "City name" },
                                    unit = new { type = "string", @enum = new[] { "celsius", "fahrenheit" } }
                                },
                                required = new[] { "location" }
                            }
                        }
                    }
                }
            };

            var geminiResponse = new
            {
                candidates = new[]
                {
                    new
                    {
                        content = new
                        {
                            parts = new[]
                            {
                                new
                                {
                                    functionCall = new
                                    {
                                        name = "get_weather",
                                        args = new { location = "Seattle", unit = "fahrenheit" }
                                    }
                                }
                            },
                            role = "model"
                        },
                        finishReason = "STOP"
                    }
                },
                usageMetadata = new { promptTokenCount = 20, candidatesTokenCount = 10, totalTokenCount = 30 }
            };

            SetupHttpResponse(HttpStatusCode.OK, geminiResponse);
            SetupAuthToken();

            // Act
            var response = await _client.CompleteAsync(request);

            // Assert
            response.Choices[0].Message.ToolCalls.Should().HaveCount(1);
            response.Choices[0].Message.ToolCalls![0].Function.Name.Should().Be("get_weather");
            var args = JsonSerializer.Deserialize<Dictionary<string, object>>(
                response.Choices[0].Message.ToolCalls[0].Function.Arguments);
            args!["location"].ToString().Should().Be("Seattle");
        }

        #endregion

        #region Palm Model Tests

        [Fact]
        public async Task CompleteAsync_WithPalmModel_ShouldReturnResponse()
        {
            // Arrange
            var request = new ChatCompletionRequest
            {
                Model = "text-bison",
                Messages = new List<Message>
                {
                    new() { Role = "user", Content = "Hello PaLM!" }
                },
                Temperature = 0.8,
                MaxTokens = 50
            };

            var palmResponse = new
            {
                predictions = new[]
                {
                    new
                    {
                        content = "Hello! I'm PaLM 2 on Vertex AI.",
                        safetyAttributes = new
                        {
                            blocked = false,
                            scores = new[] { 0.1, 0.1, 0.1 },
                            categories = new[] { "Finance", "Politics", "Health" }
                        }
                    }
                },
                metadata = new
                {
                    tokenMetadata = new
                    {
                        inputTokenCount = new { totalTokens = 10 },
                        outputTokenCount = new { totalTokens = 12 }
                    }
                }
            };

            SetupHttpResponse(HttpStatusCode.OK, palmResponse);
            SetupAuthToken();

            // Act
            var response = await _client.CompleteAsync(request);

            // Assert
            response.Choices[0].Message.Content.Should().Be("Hello! I'm PaLM 2 on Vertex AI.");
            response.Usage.PromptTokens.Should().Be(10);
            response.Usage.CompletionTokens.Should().Be(12);
        }

        #endregion

        #region Streaming Tests

        [Fact]
        public async Task StreamCompletionAsync_WithGemini_ShouldStreamChunks()
        {
            // Arrange
            var request = new ChatCompletionRequest
            {
                Model = "gemini-1.5-pro",
                Messages = new List<Message>
                {
                    new() { Role = "user", Content = "Stream a response" }
                },
                Stream = true,
                MaxTokens = 100
            };

            var streamContent = @"data: {""candidates"":[{""content"":{""parts"":[{""text"":""Hello ""}],""role"":""model""}}]}

data: {""candidates"":[{""content"":{""parts"":[{""text"":""from ""}],""role"":""model""}}]}

data: {""candidates"":[{""content"":{""parts"":[{""text"":""Vertex AI!""}],""role"":""model""},""finishReason"":""STOP""}],""usageMetadata"":{""promptTokenCount"":10,""candidatesTokenCount"":5,""totalTokenCount"":15}}

";

            SetupStreamingResponse(streamContent);
            SetupAuthToken();

            // Act
            var chunks = new List<ChatCompletionChunk>();
            await foreach (var chunk in _client.StreamCompletionAsync(request))
            {
                chunks.Add(chunk);
            }

            // Assert
            chunks.Should().HaveCount(3);
            chunks[0].Choices[0].Delta.Content.Should().Be("Hello ");
            chunks[1].Choices[0].Delta.Content.Should().Be("from ");
            chunks[2].Choices[0].Delta.Content.Should().Be("Vertex AI!");
            chunks[2].Choices[0].FinishReason.Should().Be("STOP");
        }

        #endregion

        #region Embeddings Tests

        [Fact]
        public async Task CreateEmbeddingAsync_WithGeckoModel_ShouldReturnEmbedding()
        {
            // Arrange
            var request = new EmbeddingRequest
            {
                Model = "textembedding-gecko",
                Input = "Test embedding input"
            };

            var embeddingResponse = new
            {
                predictions = new[]
                {
                    new
                    {
                        embeddings = new
                        {
                            values = Enumerable.Range(0, 768).Select(i => i * 0.001f).ToArray()
                        }
                    }
                },
                metadata = new
                {
                    billableCharacterCount = 20
                }
            };

            SetupHttpResponse(HttpStatusCode.OK, embeddingResponse);
            SetupAuthToken();

            // Act
            var response = await _client.CreateEmbeddingAsync(request);

            // Assert
            response.Should().NotBeNull();
            response.Data.Should().HaveCount(1);
            response.Data[0].Embedding.Should().HaveCount(768);
            response.Data[0].Embedding[0].Should().Be(0.0f);
        }

        [Fact]
        public async Task CreateEmbeddingAsync_WithMultipleInputs_ShouldReturnMultipleEmbeddings()
        {
            // Arrange
            var request = new EmbeddingRequest
            {
                Model = "textembedding-gecko-multilingual",
                Input = new List<string> { "First text", "Second text", "Third text" }
            };

            var embeddingResponse = new
            {
                predictions = new[]
                {
                    new { embeddings = new { values = Enumerable.Range(0, 768).Select(i => 0.001f).ToArray() } },
                    new { embeddings = new { values = Enumerable.Range(0, 768).Select(i => 0.002f).ToArray() } },
                    new { embeddings = new { values = Enumerable.Range(0, 768).Select(i => 0.003f).ToArray() } }
                }
            };

            SetupHttpResponse(HttpStatusCode.OK, embeddingResponse);
            SetupAuthToken();

            // Act
            var response = await _client.CreateEmbeddingAsync(request);

            // Assert
            response.Data.Should().HaveCount(3);
            response.Data[0].Index.Should().Be(0);
            response.Data[1].Index.Should().Be(1);
            response.Data[2].Index.Should().Be(2);
        }

        #endregion

        #region Image Generation Tests

        [Fact]
        public async Task GenerateImageAsync_WithImagenModel_ShouldReturnImages()
        {
            // Arrange
            var request = new ImageGenerationRequest
            {
                Model = "imagegeneration@005",
                Prompt = "A futuristic cityscape at night",
                N = 2,
                Size = "1024x1024"
            };

            var imagenResponse = new
            {
                predictions = new[]
                {
                    new
                    {
                        bytesBase64Encoded = Convert.ToBase64String(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }),
                        mimeType = "image/jpeg"
                    },
                    new
                    {
                        bytesBase64Encoded = Convert.ToBase64String(new byte[] { 0xFF, 0xD8, 0xFF, 0xE1 }),
                        mimeType = "image/jpeg"
                    }
                }
            };

            SetupHttpResponse(HttpStatusCode.OK, imagenResponse);
            SetupAuthToken();

            // Act
            var response = await _client.GenerateImageAsync(request);

            // Assert
            response.Should().NotBeNull();
            response.Data.Should().HaveCount(2);
            response.Data[0].B64Json.Should().NotBeNullOrEmpty();
            response.Data[1].B64Json.Should().NotBeNullOrEmpty();
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public async Task CompleteAsync_WithQuotaExceeded_ShouldThrowWithRetryInfo()
        {
            // Arrange
            var request = new ChatCompletionRequest
            {
                Model = "gemini-1.5-pro",
                Messages = new List<Message> { new() { Role = "user", Content = "Test" } }
            };

            var errorResponse = new
            {
                error = new
                {
                    code = 429,
                    message = "Quota exceeded for quota metric 'GenerateContent requests' and limit 'GenerateContent requests per minute'",
                    status = "RESOURCE_EXHAUSTED",
                    details = new[]
                    {
                        new
                        {
                            @type = "type.googleapis.com/google.rpc.ErrorInfo",
                            reason = "RATE_LIMIT_EXCEEDED",
                            metadata = new { quota_limit = "60", quota_metric = "requests_per_minute" }
                        }
                    }
                }
            };

            SetupHttpResponse(HttpStatusCode.TooManyRequests, errorResponse);
            SetupAuthToken();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<HttpRequestException>(() => 
                _client.CompleteAsync(request));
            
            ex.Message.Should().Contain("Quota exceeded");
        }

        [Fact]
        public async Task CompleteAsync_WithInvalidAuthentication_ShouldThrowUnauthorized()
        {
            // Arrange
            var request = new ChatCompletionRequest
            {
                Model = "gemini-1.5-pro",
                Messages = new List<Message> { new() { Role = "user", Content = "Test" } }
            };

            var errorResponse = new
            {
                error = new
                {
                    code = 401,
                    message = "Request had invalid authentication credentials",
                    status = "UNAUTHENTICATED"
                }
            };

            SetupHttpResponse(HttpStatusCode.Unauthorized, errorResponse);
            _mockGoogleCredential.Setup(x => x.GetAccessTokenAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync((string?)null);

            // Act & Assert
            await Assert.ThrowsAsync<HttpRequestException>(() => 
                _client.CompleteAsync(request));
        }

        [Fact]
        public async Task CompleteAsync_WithSafetyBlock_ShouldReturnBlockedResponse()
        {
            // Arrange
            var request = new ChatCompletionRequest
            {
                Model = "gemini-1.5-pro",
                Messages = new List<Message> { new() { Role = "user", Content = "Harmful content" } }
            };

            var blockedResponse = new
            {
                candidates = new[]
                {
                    new
                    {
                        content = new
                        {
                            parts = Array.Empty<object>(),
                            role = "model"
                        },
                        finishReason = "SAFETY",
                        safetyRatings = new[]
                        {
                            new 
                            { 
                                category = "HARM_CATEGORY_HARASSMENT", 
                                probability = "HIGH",
                                blocked = true
                            }
                        }
                    }
                },
                promptFeedback = new
                {
                    blockReason = "SAFETY",
                    safetyRatings = new[]
                    {
                        new { category = "HARM_CATEGORY_HARASSMENT", probability = "HIGH" }
                    }
                }
            };

            SetupHttpResponse(HttpStatusCode.OK, blockedResponse);
            SetupAuthToken();

            // Act
            var response = await _client.CompleteAsync(request);

            // Assert
            response.Choices[0].FinishReason.Should().Be("SAFETY");
            response.Choices[0].Message.Content.Should().BeNullOrEmpty();
        }

        #endregion

        #region Region Support Tests

        [Fact]
        public void SetAuthentication_WithDifferentRegions_ShouldConfigureCorrectly()
        {
            // Test various GCP regions
            var regions = new[] 
            { 
                "us-central1", "us-east1", "us-west1", 
                "europe-west4", "asia-northeast1", "australia-southeast1" 
            };

            foreach (var region in regions)
            {
                // Arrange
                var credentials = new ProviderCredentials
                {
                    ApiKey = "APPLICATION_DEFAULT_CREDENTIALS",
                    ProjectId = "test-project",
                    Region = region
                };

                // Act
                _client.SetAuthentication(credentials);

                // Assert
                _client.IsAuthenticated.Should().BeTrue();
                _httpClient.BaseAddress!.ToString().Should().Contain(region);
            }
        }

        #endregion

        #region GetCapabilities Tests

        [Fact]
        public async Task GetCapabilities_ShouldReturnVertexAICapabilities()
        {
            // Act
            var capabilities = await _client.GetCapabilitiesAsync();

            // Assert
            capabilities.SupportsChatCompletion.Should().BeTrue();
            capabilities.SupportsEmbeddings.Should().BeTrue();
            capabilities.SupportsImageGeneration.Should().BeTrue();
            capabilities.SupportsStreaming.Should().BeTrue();
            capabilities.SupportsFunctionCalling.Should().BeTrue();
            capabilities.SupportsAudioTranscription.Should().BeFalse(); // Not directly supported
            capabilities.SupportsTextToSpeech.Should().BeFalse(); // Not directly supported
            
            // Verify model list includes expected models
            capabilities.SupportedModels.Should().Contain(m => m.Contains("gemini"));
            capabilities.SupportedModels.Should().Contain(m => m.Contains("bison"));
            capabilities.SupportedModels.Should().Contain(m => m.Contains("gecko"));
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

        private void SetupAuthToken()
        {
            _mockGoogleCredential.Setup(x => x.GetAccessTokenAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync("test-access-token");
        }

        private object CreateGeminiResponse(string content)
        {
            return new
            {
                candidates = new[]
                {
                    new
                    {
                        content = new
                        {
                            parts = new[]
                            {
                                new { text = content }
                            },
                            role = "model"
                        },
                        finishReason = "STOP"
                    }
                },
                usageMetadata = new
                {
                    promptTokenCount = 10,
                    candidatesTokenCount = 15,
                    totalTokenCount = 25
                }
            };
        }

        #endregion
    }

    // Mock interface for Google credentials
    public interface IGoogleCredential
    {
        Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken);
    }
}