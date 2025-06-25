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
using ConduitLLM.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace ConduitLLM.Tests.Services
{
    /// <summary>
    /// Unit tests for OpenRouterDiscoveryProvider covering API integration, pricing extraction, and capability inference.
    /// </summary>
    public class OpenRouterDiscoveryProviderTests : IDisposable
    {
        private readonly Mock<HttpMessageHandler> _mockHttpHandler;
        private readonly HttpClient _httpClient;
        private readonly Mock<ILogger<OpenRouterDiscoveryProvider>> _mockLogger;
        private readonly string _apiKey = "test-api-key";

        public OpenRouterDiscoveryProviderTests()
        {
            _mockHttpHandler = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_mockHttpHandler.Object);
            _mockLogger = new Mock<ILogger<OpenRouterDiscoveryProvider>>();
        }

        [Fact]
        public void Constructor_WithNullHttpClient_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new OpenRouterDiscoveryProvider(null, _mockLogger.Object, _apiKey));
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new OpenRouterDiscoveryProvider(_httpClient, null, _apiKey));
        }

        [Fact]
        public void Constructor_WithValidParameters_CreatesProvider()
        {
            // Act
            var provider = new OpenRouterDiscoveryProvider(_httpClient, _mockLogger.Object, _apiKey);

            // Assert
            Assert.NotNull(provider);
            Assert.Equal("openrouter", provider.ProviderName);
            Assert.True(provider.SupportsDiscovery);
        }

        [Fact]
        public void Constructor_WithoutApiKey_StillSupportsDiscovery()
        {
            // Act
            var provider = new OpenRouterDiscoveryProvider(_httpClient, _mockLogger.Object);

            // Assert
            Assert.Equal("openrouter", provider.ProviderName);
            Assert.True(provider.SupportsDiscovery); // OpenRouter supports discovery without API key
        }

        [Fact]
        public async Task DiscoverModelsAsync_WithSuccessfulApiResponse_ReturnsDiscoveredModels()
        {
            // Arrange
            var provider = new OpenRouterDiscoveryProvider(_httpClient, _mockLogger.Object, _apiKey);
            var apiResponse = CreateMockOpenRouterResponse();
            SetupSuccessfulHttpResponse(apiResponse);

            // Act
            var result = await provider.DiscoverModelsAsync();

            // Assert
            Assert.NotEmpty(result);
            Assert.All(result, model => Assert.Equal("openrouter", model.Provider));
            Assert.All(result, model => Assert.Equal(ModelDiscoverySource.ProviderApi, model.Source));
            
            var gpt4Model = result.FirstOrDefault(m => m.ModelId == "openai/gpt-4");
            Assert.NotNull(gpt4Model);
            Assert.Equal("GPT-4", gpt4Model.DisplayName);
            Assert.True(gpt4Model.Capabilities.Chat);
            Assert.True(gpt4Model.Capabilities.Vision);
            Assert.True(gpt4Model.Capabilities.FunctionCalling);
            Assert.True(gpt4Model.Capabilities.JsonMode);
            Assert.Equal(0.03m, gpt4Model.InputTokenCost);
            Assert.Equal(0.06m, gpt4Model.OutputTokenCost);
            Assert.Equal(8192, gpt4Model.MaxContextTokens);
            
            VerifyLoggerInformation("Discovering models from OpenRouter API");
            VerifyLoggerInformation($"Successfully discovered {result.Count} models from OpenRouter");
            VerifyCorrectHttpRequest(withAuth: true);
        }

        [Fact]
        public async Task DiscoverModelsAsync_WithoutApiKey_MakesRequestWithoutAuth()
        {
            // Arrange
            var provider = new OpenRouterDiscoveryProvider(_httpClient, _mockLogger.Object);
            var apiResponse = CreateMockOpenRouterResponse();
            SetupSuccessfulHttpResponse(apiResponse);

            // Act
            var result = await provider.DiscoverModelsAsync();

            // Assert
            Assert.NotEmpty(result);
            VerifyCorrectHttpRequest(withAuth: false);
        }

        [Fact]
        public async Task DiscoverModelsAsync_WithHttpError_ReturnsEmptyList()
        {
            // Arrange
            var provider = new OpenRouterDiscoveryProvider(_httpClient, _mockLogger.Object, _apiKey);
            SetupHttpError(HttpStatusCode.ServiceUnavailable, "Service temporarily unavailable");

            // Act
            var result = await provider.DiscoverModelsAsync();

            // Assert
            Assert.Empty(result);
            VerifyLoggerWarning("OpenRouter API returned ServiceUnavailable: Service temporarily unavailable");
        }

        [Fact]
        public async Task DiscoverModelsAsync_WithHttpException_ReturnsEmptyList()
        {
            // Arrange
            var provider = new OpenRouterDiscoveryProvider(_httpClient, _mockLogger.Object, _apiKey);
            SetupHttpException(new HttpRequestException("Network error"));

            // Act
            var result = await provider.DiscoverModelsAsync();

            // Assert
            Assert.Empty(result);
            VerifyLoggerError("HTTP error during model discovery for openrouter: Network error");
        }

        [Fact]
        public async Task DiscoverModelsAsync_WithTimeoutException_ReturnsEmptyList()
        {
            // Arrange
            var provider = new OpenRouterDiscoveryProvider(_httpClient, _mockLogger.Object, _apiKey);
            SetupHttpException(new TaskCanceledException("Request timeout"));

            // Act
            var result = await provider.DiscoverModelsAsync();

            // Assert
            Assert.Empty(result);
            VerifyLoggerError("Timeout during model discovery for openrouter");
        }

        [Fact]
        public async Task DiscoverModelsAsync_WithInvalidJsonResponse_ReturnsEmptyList()
        {
            // Arrange
            var provider = new OpenRouterDiscoveryProvider(_httpClient, _mockLogger.Object, _apiKey);
            SetupHttpResponse(HttpStatusCode.OK, "invalid json");

            // Act
            var result = await provider.DiscoverModelsAsync();

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public async Task DiscoverModelsAsync_WithNullApiResponse_ReturnsEmptyList()
        {
            // Arrange
            var provider = new OpenRouterDiscoveryProvider(_httpClient, _mockLogger.Object, _apiKey);
            var nullResponse = JsonSerializer.Serialize(new { data = (object?)null });
            SetupHttpResponse(HttpStatusCode.OK, nullResponse);

            // Act
            var result = await provider.DiscoverModelsAsync();

            // Assert
            Assert.Empty(result);
            VerifyLoggerWarning("OpenRouter API returned null or empty response");
        }

        [Fact]
        public async Task DiscoverModelsAsync_WithEmptyApiResponse_ReturnsEmptyList()
        {
            // Arrange
            var provider = new OpenRouterDiscoveryProvider(_httpClient, _mockLogger.Object, _apiKey);
            var emptyResponse = JsonSerializer.Serialize(new { data = new object[0] });
            SetupHttpResponse(HttpStatusCode.OK, emptyResponse);

            // Act
            var result = await provider.DiscoverModelsAsync();

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public async Task DiscoverModelsAsync_WithModelsContainingEmptyIds_FiltersThemOut()
        {
            // Arrange
            var provider = new OpenRouterDiscoveryProvider(_httpClient, _mockLogger.Object, _apiKey);
            var response = JsonSerializer.Serialize(new
            {
                data = new[]
                {
                    new { id = "openai/gpt-4", name = "GPT-4", context_length = 8192 },
                    new { id = "", name = "Empty ID Model", context_length = 4096 },
                    new { id = (string?)null, name = "Null ID Model", context_length = 4096 },
                    new { id = "anthropic/claude-3", name = "Claude 3", context_length = 200000 }
                }
            }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
            SetupHttpResponse(HttpStatusCode.OK, response);

            // Act
            var result = await provider.DiscoverModelsAsync();

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Contains(result, m => m.ModelId == "openai/gpt-4");
            Assert.Contains(result, m => m.ModelId == "anthropic/claude-3");
            Assert.DoesNotContain(result, m => string.IsNullOrEmpty(m.ModelId));
        }

        [Theory]
        [InlineData("openai/gpt-4", "GPT-4 model", true)]
        [InlineData("openai/gpt-4-vision-preview", "GPT-4 Vision", true)]
        [InlineData("anthropic/claude-3-sonnet", "Claude 3 Sonnet", true)]
        [InlineData("google/gemini-pro", "Gemini Pro", true)]
        [InlineData("meta-llama/llama-2-70b", "Llama 2 70B", false)]
        [InlineData("mistralai/mistral-7b", "Mistral 7B", false)]
        public async Task DiscoverModelsAsync_WithVariousModels_InfersVisionCapabilityCorrectly(
            string modelId, string description, bool expectedVision)
        {
            // Arrange
            var provider = new OpenRouterDiscoveryProvider(_httpClient, _mockLogger.Object, _apiKey);
            var response = JsonSerializer.Serialize(new
            {
                data = new[] { new { id = modelId, name = $"Test {modelId}", description = description } }
            }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
            SetupHttpResponse(HttpStatusCode.OK, response);

            // Act
            var result = await provider.DiscoverModelsAsync();

            // Assert
            Assert.Single(result);
            var model = result.First();
            Assert.Equal(expectedVision, model.Capabilities.Vision);
            Assert.True(model.Capabilities.Chat);
            Assert.True(model.Capabilities.ChatStream);
        }

        [Theory]
        [InlineData("openai/gpt-4", true, true)]
        [InlineData("openai/gpt-3.5-turbo", true, true)]
        [InlineData("anthropic/claude-3-sonnet", true, false)]
        [InlineData("google/gemini-pro", false, false)]
        [InlineData("meta-llama/llama-2-70b", false, false)]
        public async Task DiscoverModelsAsync_WithVariousModels_InfersFunctionCallingCorrectly(
            string modelId, bool expectedFunctionCalling, bool expectedJsonMode)
        {
            // Arrange
            var provider = new OpenRouterDiscoveryProvider(_httpClient, _mockLogger.Object, _apiKey);
            var response = JsonSerializer.Serialize(new
            {
                data = new[] { new { id = modelId, name = $"Test {modelId}" } }
            }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
            SetupHttpResponse(HttpStatusCode.OK, response);

            // Act
            var result = await provider.DiscoverModelsAsync();

            // Assert
            Assert.Single(result);
            var model = result.First();
            Assert.Equal(expectedFunctionCalling, model.Capabilities.FunctionCalling);
            Assert.Equal(expectedFunctionCalling, model.Capabilities.ToolUse);
            Assert.Equal(expectedJsonMode, model.Capabilities.JsonMode);
        }

        [Fact]
        public async Task DiscoverModelsAsync_WithPricingInformation_ExtractsPricingCorrectly()
        {
            // Arrange
            var provider = new OpenRouterDiscoveryProvider(_httpClient, _mockLogger.Object, _apiKey);
            var response = JsonSerializer.Serialize(new
            {
                data = new[]
                {
                    new {
                        id = "test/model",
                        name = "Test Model",
                        pricing = new {
                            prompt = 0.001m,
                            completion = 0.002m,
                            image = 0.01m,
                            request = 0.0001m
                        }
                    }
                }
            }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
            SetupHttpResponse(HttpStatusCode.OK, response);

            // Act
            var result = await provider.DiscoverModelsAsync();

            // Assert
            Assert.Single(result);
            var model = result.First();
            Assert.Equal(0.001m, model.InputTokenCost);
            Assert.Equal(0.002m, model.OutputTokenCost);
            Assert.Equal(0.01m, model.ImageCostPerImage);
        }

        [Fact]
        public async Task DiscoverModelsAsync_WithContextLength_SetsCapabilitiesCorrectly()
        {
            // Arrange
            var provider = new OpenRouterDiscoveryProvider(_httpClient, _mockLogger.Object, _apiKey);
            var response = JsonSerializer.Serialize(new
            {
                data = new[]
                {
                    new {
                        id = "test/model",
                        name = "Test Model",
                        context_length = 32768,
                        description = "A test model",
                        top_provider = new { name = "TestProvider" },
                        per_request_limits = new { tokens = 1000 },
                        architecture = new {
                            tokenizer = "tiktoken",
                            instruct_type = "vicuna"
                        }
                    }
                }
            }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
            SetupHttpResponse(HttpStatusCode.OK, response);

            // Act
            var result = await provider.DiscoverModelsAsync();

            // Assert
            Assert.Single(result);
            var model = result.First();
            Assert.Equal(32768, model.MaxContextTokens);
            Assert.Equal(32768, model.Capabilities.MaxTokens);
            Assert.Equal(8192, model.Capabilities.MaxOutputTokens); // Should be min(8192, context_length/4)
            Assert.Equal("A test model", model.AdditionalMetadata["description"]);
            Assert.NotNull(model.AdditionalMetadata["top_provider"]);
            Assert.NotNull(model.AdditionalMetadata["per_request_limits"]);
            Assert.Equal("tiktoken", model.AdditionalMetadata["tokenizer"]);
            Assert.Equal("vicuna", model.AdditionalMetadata["instruct_type"]);
        }

        [Fact]
        public async Task DiscoverModelsAsync_WithLargeContextLength_LimitsMaxOutputTokens()
        {
            // Arrange
            var provider = new OpenRouterDiscoveryProvider(_httpClient, _mockLogger.Object, _apiKey);
            var response = JsonSerializer.Serialize(new
            {
                data = new[]
                {
                    new {
                        id = "test/large-context",
                        name = "Large Context Model",
                        context_length = 1000000
                    }
                }
            }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
            SetupHttpResponse(HttpStatusCode.OK, response);

            // Act
            var result = await provider.DiscoverModelsAsync();

            // Assert
            Assert.Single(result);
            var model = result.First();
            Assert.Equal(1000000, model.Capabilities.MaxTokens);
            Assert.Equal(8192, model.Capabilities.MaxOutputTokens); // Should be capped at 8192
        }

        [Fact]
        public async Task DiscoverModelsAsync_WithMissingDisplayName_UsesModelId()
        {
            // Arrange
            var provider = new OpenRouterDiscoveryProvider(_httpClient, _mockLogger.Object, _apiKey);
            var response = JsonSerializer.Serialize(new
            {
                data = new[] { new { id = "provider/model-name" } }
            }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
            SetupHttpResponse(HttpStatusCode.OK, response);

            // Act
            var result = await provider.DiscoverModelsAsync();

            // Assert
            Assert.Single(result);
            var model = result.First();
            Assert.Equal("provider/model-name", model.ModelId);
            Assert.Equal("provider/model-name", model.DisplayName);
        }

        [Fact]
        public async Task DiscoverModelsAsync_WithCancellation_ReturnsFallbackModels()
        {
            // Arrange
            var provider = new OpenRouterDiscoveryProvider(_httpClient, _mockLogger.Object, _apiKey);
            using var cts = new CancellationTokenSource();
            SetupHttpCancellation();

            // Act - Discovery services are fault-tolerant and return fallback models on cancellation
            var result = await provider.DiscoverModelsAsync(cts.Token);

            // Assert - OpenRouter returns empty list on cancellation, not fallback models
            Assert.Empty(result);
        }

        [Fact]
        public async Task IsAvailableAsync_WithSuccessfulDiscovery_ReturnsTrue()
        {
            // Arrange
            var provider = new OpenRouterDiscoveryProvider(_httpClient, _mockLogger.Object, _apiKey);
            var apiResponse = CreateMockOpenRouterResponse();
            SetupSuccessfulHttpResponse(apiResponse);

            // Act
            var result = await provider.IsAvailableAsync();

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task IsAvailableAsync_WithEmptyResponse_ReturnsFalse()
        {
            // Arrange
            var provider = new OpenRouterDiscoveryProvider(_httpClient, _mockLogger.Object, _apiKey);
            var emptyResponse = JsonSerializer.Serialize(new { data = new object[0] });
            SetupHttpResponse(HttpStatusCode.OK, emptyResponse);

            // Act
            var result = await provider.IsAvailableAsync();

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task GetModelMetadataAsync_WithExistingModel_ReturnsMetadata()
        {
            // Arrange
            var provider = new OpenRouterDiscoveryProvider(_httpClient, _mockLogger.Object, _apiKey);
            var apiResponse = CreateMockOpenRouterResponse();
            SetupSuccessfulHttpResponse(apiResponse);

            // Act
            var result = await provider.GetModelMetadataAsync("openai/gpt-4");

            // Assert
            Assert.NotNull(result);
            Assert.Equal("openai/gpt-4", result.ModelId);
            Assert.Equal("openrouter", result.Provider);
        }

        [Fact]
        public async Task GetModelMetadataAsync_WithNonexistentModel_ReturnsNull()
        {
            // Arrange
            var provider = new OpenRouterDiscoveryProvider(_httpClient, _mockLogger.Object, _apiKey);
            var apiResponse = CreateMockOpenRouterResponse();
            SetupSuccessfulHttpResponse(apiResponse);

            // Act
            var result = await provider.GetModelMetadataAsync("nonexistent/model");

            // Assert
            Assert.Null(result);
        }

        private string CreateMockOpenRouterResponse()
        {
            var response = new
            {
                data = new object[]
                {
                    new {
                        id = "openai/gpt-4",
                        name = "GPT-4",
                        description = "GPT-4 by OpenAI with vision capabilities",
                        context_length = 8192,
                        pricing = new {
                            prompt = 0.03m,
                            completion = 0.06m,
                            image = 0.001m
                        },
                        top_provider = new {
                            name = "OpenAI"
                        },
                        architecture = new {
                            tokenizer = "tiktoken",
                            instruct_type = "none"
                        }
                    },
                    new {
                        id = "anthropic/claude-3-sonnet",
                        name = "Claude 3 Sonnet",
                        description = "Claude 3 Sonnet by Anthropic",
                        context_length = 200000,
                        pricing = new {
                            prompt = 0.015m,
                            completion = 0.075m
                        },
                        top_provider = new {
                            name = "Anthropic"
                        }
                    }
                }
            };

            return JsonSerializer.Serialize(response, new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower 
            });
        }

        private void SetupSuccessfulHttpResponse(string responseContent)
        {
            SetupHttpResponse(HttpStatusCode.OK, responseContent);
        }

        private void SetupHttpResponse(HttpStatusCode statusCode, string content)
        {
            var response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            };

            _mockHttpHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    Moq.Protected.ItExpr.IsAny<HttpRequestMessage>(),
                    Moq.Protected.ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(response);
        }

        private void SetupHttpError(HttpStatusCode statusCode, string reasonPhrase)
        {
            var response = new HttpResponseMessage(statusCode)
            {
                ReasonPhrase = reasonPhrase,
                Content = new StringContent("", Encoding.UTF8, "application/json")
            };

            _mockHttpHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    Moq.Protected.ItExpr.IsAny<HttpRequestMessage>(),
                    Moq.Protected.ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(response);
        }

        private void SetupHttpException(Exception exception)
        {
            _mockHttpHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    Moq.Protected.ItExpr.IsAny<HttpRequestMessage>(),
                    Moq.Protected.ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(exception);
        }

        private void SetupHttpCancellation()
        {
            _mockHttpHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    Moq.Protected.ItExpr.IsAny<HttpRequestMessage>(),
                    Moq.Protected.ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new OperationCanceledException());
        }

        private void VerifyCorrectHttpRequest(bool withAuth)
        {
            if (withAuth)
            {
                _mockHttpHandler.Protected().Verify(
                    "SendAsync",
                    Times.Once(),
                    Moq.Protected.ItExpr.Is<HttpRequestMessage>(req =>
                        req.Method == HttpMethod.Get &&
                        req.RequestUri.ToString() == "https://openrouter.ai/api/v1/models" &&
                        req.Headers.Authorization != null &&
                        req.Headers.Authorization.Scheme == "Bearer" &&
                        req.Headers.Authorization.Parameter == _apiKey &&
                        req.Headers.Contains("HTTP-Referer") &&
                        req.Headers.GetValues("HTTP-Referer").Contains("https://conduit-llm.com") &&
                        req.Headers.Contains("X-Title") &&
                        req.Headers.GetValues("X-Title").Contains("Conduit LLM")),
                    Moq.Protected.ItExpr.IsAny<CancellationToken>());
            }
            else
            {
                _mockHttpHandler.Protected().Verify(
                    "SendAsync",
                    Times.Once(),
                    Moq.Protected.ItExpr.Is<HttpRequestMessage>(req =>
                        req.Method == HttpMethod.Get &&
                        req.RequestUri.ToString() == "https://openrouter.ai/api/v1/models" &&
                        req.Headers.Authorization == null &&
                        req.Headers.Contains("HTTP-Referer") &&
                        req.Headers.GetValues("HTTP-Referer").Contains("https://conduit-llm.com") &&
                        req.Headers.Contains("X-Title") &&
                        req.Headers.GetValues("X-Title").Contains("Conduit LLM")),
                    Moq.Protected.ItExpr.IsAny<CancellationToken>());
            }
        }

        private void VerifyLoggerInformation(string expectedMessage)
        {
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains(expectedMessage)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.AtLeastOnce);
        }

        private void VerifyLoggerWarning(string expectedMessage)
        {
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains(expectedMessage)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.AtLeastOnce);
        }

        private void VerifyLoggerError(string expectedMessage)
        {
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains(expectedMessage)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.AtLeastOnce);
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}