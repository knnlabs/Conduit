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
    /// Unit tests for AnthropicDiscoveryProvider covering API integration, capability inference, and error handling.
    /// </summary>
    public class AnthropicDiscoveryProviderTests : IDisposable
    {
        private readonly Mock<HttpMessageHandler> _mockHttpHandler;
        private readonly HttpClient _httpClient;
        private readonly Mock<ILogger<AnthropicDiscoveryProvider>> _mockLogger;
        private readonly string _apiKey = "test-api-key";

        public AnthropicDiscoveryProviderTests()
        {
            _mockHttpHandler = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_mockHttpHandler.Object);
            _mockLogger = new Mock<ILogger<AnthropicDiscoveryProvider>>();
        }

        [Fact]
        public void Constructor_WithNullHttpClient_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new AnthropicDiscoveryProvider(null, _mockLogger.Object, _apiKey));
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new AnthropicDiscoveryProvider(_httpClient, null, _apiKey));
        }

        [Fact]
        public void Constructor_WithValidParameters_CreatesProvider()
        {
            // Act
            var provider = new AnthropicDiscoveryProvider(_httpClient, _mockLogger.Object, _apiKey);

            // Assert
            Assert.NotNull(provider);
            Assert.Equal("anthropic", provider.ProviderName);
            Assert.True(provider.SupportsDiscovery);
        }

        [Fact]
        public void Constructor_WithoutApiKey_DisablesDiscovery()
        {
            // Act
            var provider = new AnthropicDiscoveryProvider(_httpClient, _mockLogger.Object);

            // Assert
            Assert.Equal("anthropic", provider.ProviderName);
            Assert.False(provider.SupportsDiscovery);
        }

        [Fact]
        public void Constructor_WithEmptyApiKey_DisablesDiscovery()
        {
            // Act
            var provider = new AnthropicDiscoveryProvider(_httpClient, _mockLogger.Object, "");

            // Assert
            Assert.Equal("anthropic", provider.ProviderName);
            Assert.False(provider.SupportsDiscovery);
        }

        [Fact]
        public async Task DiscoverModelsAsync_WithoutApiKey_ReturnsFallbackModels()
        {
            // Arrange
            var provider = new AnthropicDiscoveryProvider(_httpClient, _mockLogger.Object);

            // Act
            var result = await provider.DiscoverModelsAsync();

            // Assert
            Assert.NotEmpty(result);
            Assert.All(result, model => Assert.Equal("anthropic", model.Provider));
            Assert.All(result, model => Assert.Equal(ModelDiscoverySource.HardcodedPattern, model.Source));
            Assert.Contains(result, m => m.ModelId == "claude-3-5-sonnet-20241022");
            Assert.Contains(result, m => m.ModelId == "claude-3-opus-20240229");
            VerifyLoggerWarning("No API key available for Anthropic discovery, using fallback patterns");
        }

        [Fact]
        public async Task DiscoverModelsAsync_WithSuccessfulApiResponse_ReturnsDiscoveredModels()
        {
            // Arrange
            var provider = new AnthropicDiscoveryProvider(_httpClient, _mockLogger.Object, _apiKey);
            var apiResponse = CreateMockAnthropicResponse();
            SetupSuccessfulHttpResponse(apiResponse);

            // Act
            var result = await provider.DiscoverModelsAsync();

            // Assert
            Assert.NotEmpty(result);
            Assert.All(result, model => Assert.Equal("anthropic", model.Provider));
            Assert.All(result, model => Assert.Equal(ModelDiscoverySource.ProviderApi, model.Source));
            
            var claude3Model = result.FirstOrDefault(m => m.ModelId == "claude-3-sonnet-20240229");
            Assert.NotNull(claude3Model);
            Assert.Equal("Claude 3 Sonnet", claude3Model.DisplayName);
            Assert.True(claude3Model.Capabilities.Chat);
            Assert.True(claude3Model.Capabilities.Vision);
            Assert.True(claude3Model.Capabilities.ToolUse);
            Assert.False(claude3Model.Capabilities.JsonMode);
            
            VerifyLoggerInformation("Discovering models from Anthropic API");
            VerifyLoggerInformation($"Successfully discovered {result.Count} models from Anthropic");
            VerifyCorrectHttpRequest();
        }

        [Fact]
        public async Task DiscoverModelsAsync_WithHttpError_ReturnsFallbackModels()
        {
            // Arrange
            var provider = new AnthropicDiscoveryProvider(_httpClient, _mockLogger.Object, _apiKey);
            SetupHttpError(HttpStatusCode.Unauthorized, "Invalid API key");

            // Act
            var result = await provider.DiscoverModelsAsync();

            // Assert
            Assert.NotEmpty(result);
            Assert.All(result, model => Assert.Equal(ModelDiscoverySource.HardcodedPattern, model.Source));
            VerifyLoggerWarning("Anthropic API returned Unauthorized: Invalid API key");
        }

        [Fact]
        public async Task DiscoverModelsAsync_WithHttpException_ReturnsFallbackModels()
        {
            // Arrange
            var provider = new AnthropicDiscoveryProvider(_httpClient, _mockLogger.Object, _apiKey);
            SetupHttpException(new HttpRequestException("Network error"));

            // Act
            var result = await provider.DiscoverModelsAsync();

            // Assert
            Assert.NotEmpty(result);
            Assert.All(result, model => Assert.Equal(ModelDiscoverySource.HardcodedPattern, model.Source));
            VerifyLoggerError("HTTP error during model discovery for anthropic: Network error");
        }

        [Fact]
        public async Task DiscoverModelsAsync_WithTimeoutException_ReturnsFallbackModels()
        {
            // Arrange
            var provider = new AnthropicDiscoveryProvider(_httpClient, _mockLogger.Object, _apiKey);
            SetupHttpException(new TaskCanceledException("Request timeout"));

            // Act
            var result = await provider.DiscoverModelsAsync();

            // Assert
            Assert.NotEmpty(result);
            Assert.All(result, model => Assert.Equal(ModelDiscoverySource.HardcodedPattern, model.Source));
            VerifyLoggerError("Timeout during model discovery for anthropic");
        }

        [Fact]
        public async Task DiscoverModelsAsync_WithInvalidJsonResponse_ReturnsFallbackModels()
        {
            // Arrange
            var provider = new AnthropicDiscoveryProvider(_httpClient, _mockLogger.Object, _apiKey);
            SetupHttpResponse(HttpStatusCode.OK, "invalid json");

            // Act
            var result = await provider.DiscoverModelsAsync();

            // Assert
            Assert.NotEmpty(result);
            Assert.All(result, model => Assert.Equal(ModelDiscoverySource.HardcodedPattern, model.Source));
        }

        [Fact]
        public async Task DiscoverModelsAsync_WithNullApiResponse_ReturnsFallbackModels()
        {
            // Arrange
            var provider = new AnthropicDiscoveryProvider(_httpClient, _mockLogger.Object, _apiKey);
            var nullResponse = JsonSerializer.Serialize(new { data = (object?)null });
            SetupHttpResponse(HttpStatusCode.OK, nullResponse);

            // Act
            var result = await provider.DiscoverModelsAsync();

            // Assert
            Assert.NotEmpty(result);
            Assert.All(result, model => Assert.Equal(ModelDiscoverySource.HardcodedPattern, model.Source));
            VerifyLoggerWarning("Anthropic API returned null or empty response");
        }

        [Fact]
        public async Task DiscoverModelsAsync_WithEmptyApiResponse_ReturnsFallbackModels()
        {
            // Arrange
            var provider = new AnthropicDiscoveryProvider(_httpClient, _mockLogger.Object, _apiKey);
            var emptyResponse = JsonSerializer.Serialize(new { data = new object[0] });
            SetupHttpResponse(HttpStatusCode.OK, emptyResponse);

            // Act
            var result = await provider.DiscoverModelsAsync();

            // Assert
            Assert.NotEmpty(result);
            Assert.All(result, model => Assert.Equal(ModelDiscoverySource.HardcodedPattern, model.Source));
        }

        [Fact]
        public async Task DiscoverModelsAsync_WithModelsContainingEmptyIds_FiltersThemOut()
        {
            // Arrange
            var provider = new AnthropicDiscoveryProvider(_httpClient, _mockLogger.Object, _apiKey);
            var response = JsonSerializer.Serialize(new
            {
                data = new[]
                {
                    new { id = "claude-3-sonnet", display_name = "Claude 3 Sonnet", max_tokens = 200000 },
                    new { id = "", display_name = "Empty ID Model", max_tokens = 100000 },
                    new { id = (string?)null, display_name = "Null ID Model", max_tokens = 100000 },
                    new { id = "claude-3-haiku", display_name = "Claude 3 Haiku", max_tokens = 200000 }
                }
            }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
            SetupHttpResponse(HttpStatusCode.OK, response);

            // Act
            var result = await provider.DiscoverModelsAsync();

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Contains(result, m => m.ModelId == "claude-3-sonnet");
            Assert.Contains(result, m => m.ModelId == "claude-3-haiku");
            Assert.DoesNotContain(result, m => string.IsNullOrEmpty(m.ModelId));
        }

        [Theory]
        [InlineData("claude-3-sonnet-20240229", true)]
        [InlineData("claude-3-haiku-20240307", true)]
        [InlineData("claude-3-opus-20240229", true)]
        [InlineData("claude-2.1", false)]
        [InlineData("claude-instant-1.2", false)]
        public async Task DiscoverModelsAsync_WithVariousModels_InfersVisionCapabilityCorrectly(string modelId, bool expectedVision)
        {
            // Arrange
            var provider = new AnthropicDiscoveryProvider(_httpClient, _mockLogger.Object, _apiKey);
            var response = JsonSerializer.Serialize(new
            {
                data = new[] { new { id = modelId, display_name = $"Test {modelId}" } }
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
            Assert.True(model.Capabilities.ToolUse);
            Assert.False(model.Capabilities.JsonMode);
            Assert.False(model.Capabilities.FunctionCalling);
        }

        [Fact]
        public async Task DiscoverModelsAsync_WithContextLimits_SetsCapabilitiesCorrectly()
        {
            // Arrange
            var provider = new AnthropicDiscoveryProvider(_httpClient, _mockLogger.Object, _apiKey);
            var response = JsonSerializer.Serialize(new
            {
                data = new[]
                {
                    new { 
                        id = "claude-test", 
                        display_name = "Test Claude",
                        max_tokens = 150000,
                        max_output_tokens = 8192,
                        type = "text-completion",
                        created_at = "2024-01-01T00:00:00Z",
                        owned_by = "anthropic"
                    }
                }
            }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
            SetupHttpResponse(HttpStatusCode.OK, response);

            // Act
            var result = await provider.DiscoverModelsAsync();

            // Assert
            Assert.Single(result);
            var model = result.First();
            Assert.Equal(150000, model.MaxContextTokens);
            Assert.Equal(8192, model.MaxOutputTokens);
            Assert.Equal(150000, model.Capabilities.MaxTokens);
            Assert.Equal(8192, model.Capabilities.MaxOutputTokens);
            Assert.Equal("text-completion", model.AdditionalMetadata["type"]);
            Assert.Equal("anthropic", model.AdditionalMetadata["owned_by"]);
            Assert.True(model.AdditionalMetadata.ContainsKey("created_at"));
        }

        [Fact]
        public async Task DiscoverModelsAsync_WithMissingDisplayName_UsesModelId()
        {
            // Arrange
            var provider = new AnthropicDiscoveryProvider(_httpClient, _mockLogger.Object, _apiKey);
            var response = JsonSerializer.Serialize(new
            {
                data = new[] { new { id = "claude-test" } }
            }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
            SetupHttpResponse(HttpStatusCode.OK, response);

            // Act
            var result = await provider.DiscoverModelsAsync();

            // Assert
            Assert.Single(result);
            var model = result.First();
            Assert.Equal("claude-test", model.ModelId);
            Assert.Equal("claude-test", model.DisplayName);
        }

        [Fact]
        public async Task DiscoverModelsAsync_WithCancellation_ReturnsFallbackModels()
        {
            // Arrange
            var provider = new AnthropicDiscoveryProvider(_httpClient, _mockLogger.Object, _apiKey);
            using var cts = new CancellationTokenSource();
            SetupHttpCancellation();

            // Act - Discovery services are fault-tolerant and return fallback models on cancellation
            var result = await provider.DiscoverModelsAsync(cts.Token);

            // Assert - Should return fallback models, not throw exception
            Assert.NotEmpty(result);
            Assert.All(result, model => Assert.Equal(ModelDiscoverySource.HardcodedPattern, model.Source));
        }

        [Fact]
        public async Task IsAvailableAsync_WithSuccessfulDiscovery_ReturnsTrue()
        {
            // Arrange
            var provider = new AnthropicDiscoveryProvider(_httpClient, _mockLogger.Object, _apiKey);
            var apiResponse = CreateMockAnthropicResponse();
            SetupSuccessfulHttpResponse(apiResponse);

            // Act
            var result = await provider.IsAvailableAsync();

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task IsAvailableAsync_WithoutApiKey_ReturnsFalse()
        {
            // Arrange
            var provider = new AnthropicDiscoveryProvider(_httpClient, _mockLogger.Object);

            // Act
            var result = await provider.IsAvailableAsync();

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task GetModelMetadataAsync_WithExistingModel_ReturnsMetadata()
        {
            // Arrange
            var provider = new AnthropicDiscoveryProvider(_httpClient, _mockLogger.Object, _apiKey);
            var apiResponse = CreateMockAnthropicResponse();
            SetupSuccessfulHttpResponse(apiResponse);

            // Act
            var result = await provider.GetModelMetadataAsync("claude-3-sonnet-20240229");

            // Assert
            Assert.NotNull(result);
            Assert.Equal("claude-3-sonnet-20240229", result.ModelId);
            Assert.Equal("anthropic", result.Provider);
        }

        [Fact]
        public async Task GetModelMetadataAsync_WithNonexistentModel_ReturnsNull()
        {
            // Arrange
            var provider = new AnthropicDiscoveryProvider(_httpClient, _mockLogger.Object, _apiKey);
            var apiResponse = CreateMockAnthropicResponse();
            SetupSuccessfulHttpResponse(apiResponse);

            // Act
            var result = await provider.GetModelMetadataAsync("nonexistent-model");

            // Assert
            Assert.Null(result);
        }

        private string CreateMockAnthropicResponse()
        {
            var response = new
            {
                data = new[]
                {
                    new {
                        id = "claude-3-sonnet-20240229",
                        display_name = "Claude 3 Sonnet",
                        type = "text",
                        max_tokens = 200000,
                        max_output_tokens = 4096,
                        created_at = "2024-02-29T00:00:00Z",
                        owned_by = "anthropic"
                    },
                    new {
                        id = "claude-3-haiku-20240307",
                        display_name = "Claude 3 Haiku",
                        type = "text",
                        max_tokens = 200000,
                        max_output_tokens = 4096,
                        created_at = "2024-03-07T00:00:00Z",
                        owned_by = "anthropic"
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

        private void VerifyCorrectHttpRequest()
        {
            _mockHttpHandler.Protected().Verify(
                "SendAsync",
                Times.Once(),
                Moq.Protected.ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Get &&
                    req.RequestUri.ToString() == "https://api.anthropic.com/v1/models" &&
                    req.Headers.Contains("x-api-key") &&
                    req.Headers.GetValues("x-api-key").Contains(_apiKey) &&
                    req.Headers.Contains("anthropic-version") &&
                    req.Headers.GetValues("anthropic-version").Contains("2023-06-01")),
                Moq.Protected.ItExpr.IsAny<CancellationToken>());
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