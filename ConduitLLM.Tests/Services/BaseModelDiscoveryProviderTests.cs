using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
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
    /// Unit tests for BaseModelDiscoveryProvider covering base functionality, fallback capabilities, and error handling.
    /// </summary>
    public class BaseModelDiscoveryProviderTests : IDisposable
    {
        private readonly Mock<HttpMessageHandler> _mockHttpHandler;
        private readonly HttpClient _httpClient;
        private readonly Mock<ILogger> _mockLogger;
        private readonly TestableBaseModelDiscoveryProvider _provider;

        public BaseModelDiscoveryProviderTests()
        {
            _mockHttpHandler = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_mockHttpHandler.Object);
            _mockLogger = new Mock<ILogger>();
            _provider = new TestableBaseModelDiscoveryProvider(_httpClient, _mockLogger.Object);
        }

        [Fact]
        public void Constructor_WithNullHttpClient_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new TestableBaseModelDiscoveryProvider(null, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new TestableBaseModelDiscoveryProvider(_httpClient, null));
        }

        [Fact]
        public void Constructor_WithValidParameters_CreatesProvider()
        {
            // Act
            var provider = new TestableBaseModelDiscoveryProvider(_httpClient, _mockLogger.Object);

            // Assert
            Assert.NotNull(provider);
            Assert.Equal("test-provider", provider.ProviderName);
            Assert.True(provider.SupportsDiscovery);
        }

        [Fact]
        public async Task GetModelMetadataAsync_WithValidModelId_ReturnsMetadata()
        {
            // Arrange
            var modelId = "test-model";
            var expectedMetadata = new ModelMetadata { ModelId = modelId, Provider = "test-provider" };
            _provider.SetDiscoverModelsResult(new List<ModelMetadata> { expectedMetadata });

            // Act
            var result = await _provider.GetModelMetadataAsync(modelId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(modelId, result.ModelId);
            Assert.Equal("test-provider", result.Provider);
        }

        [Fact]
        public async Task GetModelMetadataAsync_WithNonexistentModelId_ReturnsNull()
        {
            // Arrange
            var existingModel = new ModelMetadata { ModelId = "existing-model", Provider = "test-provider" };
            _provider.SetDiscoverModelsResult(new List<ModelMetadata> { existingModel });

            // Act
            var result = await _provider.GetModelMetadataAsync("nonexistent-model");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetModelMetadataAsync_WithDiscoveryException_ReturnsNull()
        {
            // Arrange
            _provider.SetDiscoverModelsException(new HttpRequestException("API not available"));

            // Act
            var result = await _provider.GetModelMetadataAsync("test-model");

            // Assert
            Assert.Null(result);
            VerifyLoggerError("Error getting metadata for model test-model from provider test-provider");
        }

        [Fact]
        public async Task IsAvailableAsync_WithSuccessfulDiscovery_ReturnsTrue()
        {
            // Arrange
            var models = new List<ModelMetadata>
            {
                new ModelMetadata { ModelId = "model1", Provider = "test-provider" },
                new ModelMetadata { ModelId = "model2", Provider = "test-provider" }
            };
            _provider.SetDiscoverModelsResult(models);

            // Act
            var result = await _provider.IsAvailableAsync();

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task IsAvailableAsync_WithEmptyModelList_ReturnsFalse()
        {
            // Arrange
            _provider.SetDiscoverModelsResult(new List<ModelMetadata>());

            // Act
            var result = await _provider.IsAvailableAsync();

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task IsAvailableAsync_WithDiscoveryException_ReturnsFalse()
        {
            // Arrange
            _provider.SetDiscoverModelsException(new HttpRequestException("Service unavailable"));

            // Act
            var result = await _provider.IsAvailableAsync();

            // Assert
            Assert.False(result);
            VerifyLoggerWarning("Provider test-provider discovery API is not available");
        }

        [Fact]
        public void CreateFallbackMetadata_WithValidParameters_ReturnsCorrectMetadata()
        {
            // Arrange
            var modelId = "gpt-4";
            var reason = "API unavailable";

            // Act
            var result = _provider.PublicCreateFallbackMetadata(modelId, reason);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(modelId, result.ModelId);
            Assert.Equal(modelId, result.DisplayName);
            Assert.Equal("test-provider", result.Provider);
            Assert.Equal(ModelDiscoverySource.HardcodedPattern, result.Source);
            Assert.Single(result.Warnings);
            Assert.Contains($"Using fallback metadata: {reason}", result.Warnings);
            Assert.True(result.LastUpdated > DateTime.UtcNow.AddMinutes(-1));
            VerifyLoggerWarning($"Creating fallback metadata for {modelId} from test-provider: {reason}");
        }

        [Theory]
        [InlineData("gpt-4", true, true, true, true, true, 8192, 4096)]
        [InlineData("gpt-4-turbo", true, true, true, true, true, 128000, 4096)]
        [InlineData("gpt-4-vision-preview", true, true, true, true, true, 128000, 4096)]
        [InlineData("gpt-4-32k", true, true, false, true, true, 32768, 4096)]
        [InlineData("gpt-3.5-turbo", true, true, false, true, true, 16385, 4096)]
        [InlineData("dall-e-3", false, false, false, false, false, null, null)]
        [InlineData("text-embedding-ada-002", false, false, false, false, false, null, null)]
        public void GetOpenAIFallbackCapabilities_ReturnsCorrectCapabilities(
            string modelId, bool expectedChat, bool expectedChatStream, bool expectedVision,
            bool expectedFunctionCalling, bool expectedToolUse, int? expectedMaxTokens, int? expectedMaxOutputTokens)
        {
            // Arrange
            _provider.SetTestProviderName("openai");

            // Act
            var result = _provider.PublicGetFallbackCapabilities(modelId);

            // Assert
            Assert.Equal(expectedChat, result.Chat);
            Assert.Equal(expectedChatStream, result.ChatStream);
            Assert.Equal(expectedVision, result.Vision);
            Assert.Equal(expectedFunctionCalling, result.FunctionCalling);
            Assert.Equal(expectedToolUse, result.ToolUse);
            Assert.Equal(expectedMaxTokens, result.MaxTokens);
            Assert.Equal(expectedMaxOutputTokens, result.MaxOutputTokens);
        }

        [Theory]
        [InlineData("claude-3-sonnet-20240229", true, true, true, true, false, 200000, 4096)]
        [InlineData("claude-3-haiku-20240307", true, true, true, true, false, 200000, 4096)]
        [InlineData("claude-2.1", true, true, false, true, false, 200000, 4096)]
        [InlineData("claude-instant-1.2", true, true, false, true, false, 100000, 4096)]
        [InlineData("claude-vision-test", true, true, true, true, false, 100000, 4096)]
        public void GetAnthropicFallbackCapabilities_ReturnsCorrectCapabilities(
            string modelId, bool expectedChat, bool expectedChatStream, bool expectedVision,
            bool expectedToolUse, bool expectedJsonMode, int expectedMaxTokens, int expectedMaxOutputTokens)
        {
            // Arrange
            _provider.SetTestProviderName("anthropic");

            // Act
            var result = _provider.PublicGetFallbackCapabilities(modelId);

            // Assert
            Assert.Equal(expectedChat, result.Chat);
            Assert.Equal(expectedChatStream, result.ChatStream);
            Assert.Equal(expectedVision, result.Vision);
            Assert.Equal(expectedToolUse, result.ToolUse);
            Assert.Equal(expectedJsonMode, result.JsonMode);
            Assert.Equal(expectedMaxTokens, result.MaxTokens);
            Assert.Equal(expectedMaxOutputTokens, result.MaxOutputTokens);
        }

        [Theory]
        [InlineData("gemini-1.5-pro", true, true, true, true, false, false, 1048576, 8192)]
        [InlineData("gemini-pro", true, true, true, false, false, false, 32768, 8192)]
        [InlineData("gemini-pro-vision", true, true, true, false, false, false, 32768, 8192)]
        public void GetGoogleFallbackCapabilities_ReturnsCorrectCapabilities(
            string modelId, bool expectedChat, bool expectedChatStream, bool expectedVision, 
            bool expectedVideoUnderstanding, bool expectedFunctionCalling, bool expectedToolUse,
            int expectedMaxTokens, int expectedMaxOutputTokens)
        {
            // Arrange
            _provider.SetTestProviderName("google");

            // Act
            var result = _provider.PublicGetFallbackCapabilities(modelId);

            // Assert
            Assert.Equal(expectedChat, result.Chat);
            Assert.Equal(expectedChatStream, result.ChatStream);
            Assert.Equal(expectedVision, result.Vision);
            Assert.Equal(expectedVideoUnderstanding, result.VideoUnderstanding);
            Assert.Equal(expectedFunctionCalling, result.FunctionCalling);
            Assert.Equal(expectedToolUse, result.ToolUse);
            Assert.Equal(expectedMaxTokens, result.MaxTokens);
            Assert.Equal(expectedMaxOutputTokens, result.MaxOutputTokens);
        }

        [Fact]
        public void GetOpenRouterFallbackCapabilities_ReturnsCorrectCapabilities()
        {
            // Arrange
            _provider.SetTestProviderName("openrouter");
            var modelId = "openrouter/auto";

            // Act
            var result = _provider.PublicGetFallbackCapabilities(modelId);

            // Assert
            Assert.True(result.Chat);
            Assert.True(result.ChatStream);
            Assert.True(result.FunctionCalling);
            Assert.True(result.ToolUse);
        }

        [Fact]
        public void GetGenericFallbackCapabilities_ReturnsMinimalCapabilities()
        {
            // Arrange
            _provider.SetTestProviderName("unknown-provider");
            var modelId = "unknown-model";

            // Act
            var result = _provider.PublicGetFallbackCapabilities(modelId);

            // Assert
            Assert.True(result.Chat);
            Assert.True(result.ChatStream);
            Assert.False(result.Vision);
            Assert.False(result.FunctionCalling);
            Assert.False(result.ToolUse);
        }

        [Fact]
        public void LogHttpError_WithHttpRequestException_LogsCorrectMessage()
        {
            // Arrange
            var httpEx = new HttpRequestException("Connection failed");
            var operation = "model discovery";

            // Act
            _provider.PublicLogHttpError(httpEx, operation);

            // Assert
            VerifyLoggerError($"HTTP error during {operation} for test-provider: Connection failed");
        }

        [Fact]
        public void LogHttpError_WithTaskCanceledException_LogsTimeoutMessage()
        {
            // Arrange
            var timeoutEx = new TaskCanceledException("Operation timed out");
            var operation = "availability check";

            // Act
            _provider.PublicLogHttpError(timeoutEx, operation);

            // Assert
            VerifyLoggerError($"Timeout during {operation} for test-provider");
        }

        [Fact]
        public void LogHttpError_WithGenericException_LogsUnexpectedError()
        {
            // Arrange
            var genericEx = new InvalidOperationException("Something went wrong");
            var operation = "metadata retrieval";

            // Act
            _provider.PublicLogHttpError(genericEx, operation);

            // Assert
            VerifyLoggerError($"Unexpected error during {operation} for test-provider: Something went wrong");
        }

        [Fact]
        public async Task GetModelMetadataAsync_WithCaseSensitiveModelId_HandlesCorrectly()
        {
            // Arrange
            var models = new List<ModelMetadata>
            {
                new ModelMetadata { ModelId = "GPT-4", Provider = "test-provider" },
                new ModelMetadata { ModelId = "gpt-4", Provider = "test-provider" }
            };
            _provider.SetDiscoverModelsResult(models);

            // Act
            var upperResult = await _provider.GetModelMetadataAsync("GPT-4");
            var lowerResult = await _provider.GetModelMetadataAsync("gpt-4");

            // Assert
            Assert.NotNull(upperResult);
            Assert.Equal("GPT-4", upperResult.ModelId);
            Assert.NotNull(lowerResult);
            Assert.Equal("gpt-4", lowerResult.ModelId);
        }

        [Fact]
        public async Task IsAvailableAsync_WithCancellation_HandlesGracefully()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            _provider.SetDiscoverModelsDelay(TimeSpan.FromMilliseconds(100));
            cts.CancelAfter(TimeSpan.FromMilliseconds(50));

            // Act
            var result = await _provider.IsAvailableAsync(cts.Token);

            // Assert
            Assert.False(result);
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
                Times.Once);
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
                Times.Once);
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }

    /// <summary>
    /// Testable implementation of BaseModelDiscoveryProvider for testing abstract functionality.
    /// </summary>
    public class TestableBaseModelDiscoveryProvider : BaseModelDiscoveryProvider
    {
        private List<ModelMetadata>? _discoverModelsResult;
        private Exception? _discoverModelsException;
        private TimeSpan _discoverModelsDelay = TimeSpan.Zero;
        private string _providerName = "test-provider";

        public TestableBaseModelDiscoveryProvider(HttpClient httpClient, ILogger logger) 
            : base(httpClient, logger)
        {
        }

        public override string ProviderName => _providerName;
        public override bool SupportsDiscovery => true;

        public override async Task<List<ModelMetadata>> DiscoverModelsAsync(CancellationToken cancellationToken = default)
        {
            if (_discoverModelsDelay > TimeSpan.Zero)
            {
                await Task.Delay(_discoverModelsDelay, cancellationToken);
            }

            if (_discoverModelsException != null)
            {
                throw _discoverModelsException;
            }

            return _discoverModelsResult ?? new List<ModelMetadata>();
        }

        // Test helper methods
        public void SetDiscoverModelsResult(List<ModelMetadata> result) => _discoverModelsResult = result;
        public void SetDiscoverModelsException(Exception exception) => _discoverModelsException = exception;
        public void SetDiscoverModelsDelay(TimeSpan delay) => _discoverModelsDelay = delay;
        public void SetTestProviderName(string providerName) => _providerName = providerName;

        // Public wrappers for protected methods to test them
        public ModelMetadata PublicCreateFallbackMetadata(string modelId, string reason) =>
            CreateFallbackMetadata(modelId, reason);

        public ModelCapabilities PublicGetFallbackCapabilities(string modelId) =>
            GetFallbackCapabilities(modelId);

        public void PublicLogHttpError(Exception ex, string operation) =>
            LogHttpError(ex, operation);
    }
}