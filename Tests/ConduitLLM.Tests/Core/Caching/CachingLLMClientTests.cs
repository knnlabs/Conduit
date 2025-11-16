using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Options;
using ConduitLLM.Core.Caching;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Core.Caching
{
    [Trait("Category", "Unit")]
    public class CachingLLMClientTests
    {
        private readonly Mock<ILLMClient> _mockInnerClient;
        private readonly Mock<ICacheManager> _mockCacheManager;
        private readonly Mock<ICacheMetricsService> _mockMetricsService;
        private readonly Mock<IOptionsMonitor<CacheOptions>> _mockOptions;
        private readonly Mock<ILogger<CachingLLMClient>> _mockLogger;
        private readonly CacheOptions _cacheOptions;

        public CachingLLMClientTests()
        {
            _mockInnerClient = new Mock<ILLMClient>();
            _mockCacheManager = new Mock<ICacheManager>();
            _mockMetricsService = new Mock<ICacheMetricsService>();
            _mockOptions = new Mock<IOptionsMonitor<CacheOptions>>();
            _mockLogger = new Mock<ILogger<CachingLLMClient>>();

            _cacheOptions = new CacheOptions
            {
                LLMCachingEnabled = true,
                IsEnabled = true,
                DefaultExpirationMinutes = 60
            };

            _mockOptions.Setup(x => x.CurrentValue).Returns(_cacheOptions);
        }

        private CachingLLMClient CreateClient()
        {
            return new CachingLLMClient(
                _mockInnerClient.Object,
                _mockCacheManager.Object,
                _mockMetricsService.Object,
                _mockOptions.Object,
                _mockLogger.Object);
        }

        #region Runtime Toggle Tests

        [Fact]
        public async Task CreateChatCompletionAsync_CachingDisabled_BypassesCache()
        {
            // Arrange
            _cacheOptions.LLMCachingEnabled = false;
            var client = CreateClient();

            var request = new ChatCompletionRequest
            {
                Model = "gpt-4",
                Messages = new List<Message>
                {
                    new Message { Role = "user", Content = "Hello" }
                }
            };

            var expectedResponse = new ChatCompletionResponse
            {
                Id = "test-id",
                Object = "chat.completion",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = "gpt-4",
                Choices = new List<Choice>()
            };

            _mockInnerClient
                .Setup(x => x.CreateChatCompletionAsync(request, null, default))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await client.CreateChatCompletionAsync(request);

            // Assert
            Assert.Same(expectedResponse, result);
            _mockInnerClient.Verify(x => x.CreateChatCompletionAsync(request, null, default), Times.Once);
            _mockCacheManager.Verify(
                x => x.GetAsync<ChatCompletionResponse>(It.IsAny<string>(), It.IsAny<CacheRegion>(), default),
                Times.Never);
            _mockMetricsService.Verify(x => x.RecordHit(It.IsAny<long>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task CreateChatCompletionAsync_CachingEnabled_UsesCacheOnHit()
        {
            // Arrange
            _cacheOptions.LLMCachingEnabled = true;
            var client = CreateClient();

            var request = new ChatCompletionRequest
            {
                Model = "gpt-4",
                Messages = new List<Message>
                {
                    new Message { Role = "user", Content = "Hello" }
                }
            };

            var cachedResponse = new ChatCompletionResponse
            {
                Id = "cached-id",
                Object = "chat.completion",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = "gpt-4",
                Choices = new List<Choice>
                {
                    new Choice
                    {
                        Index = 0,
                        Message = new Message { Role = "assistant", Content = "Cached response" },
                        FinishReason = "stop"
                    }
                }
            };

            _mockCacheManager
                .Setup(x => x.GetAsync<ChatCompletionResponse>(It.IsAny<string>(), CacheRegion.LLMCompletion, default))
                .ReturnsAsync(cachedResponse);

            // Act
            var result = await client.CreateChatCompletionAsync(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("cached-id", result.Id);
            _mockInnerClient.Verify(
                x => x.CreateChatCompletionAsync(It.IsAny<ChatCompletionRequest>(), It.IsAny<string>(), default),
                Times.Never);
            _mockMetricsService.Verify(x => x.RecordHit(It.IsAny<double>(), "gpt-4"), Times.Once);
        }

        [Fact]
        public async Task CreateChatCompletionAsync_CachingEnabled_CallsProviderOnMiss()
        {
            // Arrange
            _cacheOptions.LLMCachingEnabled = true;
            var client = CreateClient();

            var request = new ChatCompletionRequest
            {
                Model = "gpt-4",
                Messages = new List<Message>
                {
                    new Message { Role = "user", Content = "Hello" }
                }
            };

            var providerResponse = new ChatCompletionResponse
            {
                Id = "provider-id",
                Object = "chat.completion",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = "gpt-4",
                Choices = new List<Choice>()
            };

            _mockCacheManager
                .Setup(x => x.GetAsync<ChatCompletionResponse>(It.IsAny<string>(), CacheRegion.LLMCompletion, default))
                .ReturnsAsync((ChatCompletionResponse?)null);

            _mockInnerClient
                .Setup(x => x.CreateChatCompletionAsync(request, null, default))
                .ReturnsAsync(providerResponse);

            // Act
            var result = await client.CreateChatCompletionAsync(request);

            // Assert
            Assert.Same(providerResponse, result);
            _mockInnerClient.Verify(x => x.CreateChatCompletionAsync(request, null, default), Times.Once);
            _mockMetricsService.Verify(x => x.RecordMiss("gpt-4"), Times.Once);
            _mockCacheManager.Verify(
                x => x.SetAsync(It.IsAny<string>(), providerResponse, CacheRegion.LLMCompletion, It.IsAny<TimeSpan?>(), default),
                Times.Once);
        }

        [Fact]
        public async Task CreateChatCompletionAsync_RuntimeToggle_RespectsCurrentValue()
        {
            // Arrange
            _cacheOptions.LLMCachingEnabled = false;
            var client = CreateClient();

            var request = new ChatCompletionRequest
            {
                Model = "gpt-4",
                Messages = new List<Message>
                {
                    new Message { Role = "user", Content = "Hello" }
                }
            };

            var response = new ChatCompletionResponse
            {
                Id = "test-id",
                Object = "chat.completion",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = "gpt-4",
                Choices = new List<Choice>()
            };

            _mockInnerClient
                .Setup(x => x.CreateChatCompletionAsync(request, null, default))
                .ReturnsAsync(response);

            // Act: First call with caching disabled
            await client.CreateChatCompletionAsync(request);

            // Change configuration at runtime
            _cacheOptions.LLMCachingEnabled = true;

            _mockCacheManager
                .Setup(x => x.GetAsync<ChatCompletionResponse>(It.IsAny<string>(), CacheRegion.LLMCompletion, default))
                .ReturnsAsync((ChatCompletionResponse?)null);

            // Act: Second call with caching enabled
            await client.CreateChatCompletionAsync(request);

            // Assert: First call bypassed cache, second used cache
            _mockCacheManager.Verify(
                x => x.GetAsync<ChatCompletionResponse>(It.IsAny<string>(), CacheRegion.LLMCompletion, default),
                Times.Once); // Only called on second request
            _mockMetricsService.Verify(x => x.RecordMiss("gpt-4"), Times.Once);
        }

        [Fact]
        public async Task CreateChatCompletionAsync_StreamingRequest_AlwaysBypassesCache()
        {
            // Arrange
            _cacheOptions.LLMCachingEnabled = true;
            var client = CreateClient();

            var request = new ChatCompletionRequest
            {
                Model = "gpt-4",
                Messages = new List<Message>
                {
                    new Message { Role = "user", Content = "Hello" }
                },
                Stream = true // Streaming request
            };

            var response = new ChatCompletionResponse
            {
                Id = "stream-id",
                Object = "chat.completion",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = "gpt-4",
                Choices = new List<Choice>()
            };

            _mockInnerClient
                .Setup(x => x.CreateChatCompletionAsync(request, null, default))
                .ReturnsAsync(response);

            // Act
            var result = await client.CreateChatCompletionAsync(request);

            // Assert: Cache should be bypassed even though caching is enabled
            Assert.Same(response, result);
            _mockInnerClient.Verify(x => x.CreateChatCompletionAsync(request, null, default), Times.Once);
            _mockCacheManager.Verify(
                x => x.GetAsync<ChatCompletionResponse>(It.IsAny<string>(), It.IsAny<CacheRegion>(), default),
                Times.Never);
        }

        #endregion

        #region ListModelsAsync Tests

        [Fact]
        public async Task ListModelsAsync_CachingDisabled_CallsInnerClient()
        {
            // Arrange
            _cacheOptions.LLMCachingEnabled = false;
            var client = CreateClient();

            var models = new List<string> { "gpt-4", "gpt-3.5-turbo" };
            _mockInnerClient
                .Setup(x => x.ListModelsAsync(null, default))
                .ReturnsAsync(models);

            // Act
            var result = await client.ListModelsAsync();

            // Assert
            Assert.Same(models, result);
            _mockInnerClient.Verify(x => x.ListModelsAsync(null, default), Times.Once);
            _mockCacheManager.Verify(
                x => x.GetOrCreateAsync(It.IsAny<string>(), It.IsAny<Func<Task<List<string>>>>(), It.IsAny<CacheRegion>(), It.IsAny<TimeSpan?>(), default),
                Times.Never);
        }

        [Fact]
        public async Task ListModelsAsync_CachingEnabled_UsesCacheWithLongTTL()
        {
            // Arrange
            _cacheOptions.LLMCachingEnabled = true;
            var client = CreateClient();

            var models = new List<string> { "gpt-4", "gpt-3.5-turbo" };

            _mockCacheManager
                .Setup(x => x.GetOrCreateAsync(
                    It.IsAny<string>(),
                    It.IsAny<Func<Task<List<string>>>>(),
                    CacheRegion.ModelMetadata,
                    It.Is<TimeSpan?>(t => t.HasValue && t.Value == TimeSpan.FromHours(1)),
                    default))
                .ReturnsAsync(models);

            // Act
            var result = await client.ListModelsAsync();

            // Assert
            Assert.Same(models, result);
            _mockCacheManager.Verify(
                x => x.GetOrCreateAsync(
                    It.IsAny<string>(),
                    It.IsAny<Func<Task<List<string>>>>(),
                    CacheRegion.ModelMetadata,
                    TimeSpan.FromHours(1),
                    default),
                Times.Once);
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public async Task CreateChatCompletionAsync_CacheError_FallsBackToProvider()
        {
            // Arrange
            _cacheOptions.LLMCachingEnabled = true;
            var client = CreateClient();

            var request = new ChatCompletionRequest
            {
                Model = "gpt-4",
                Messages = new List<Message>
                {
                    new Message { Role = "user", Content = "Hello" }
                }
            };

            var providerResponse = new ChatCompletionResponse
            {
                Id = "provider-id",
                Object = "chat.completion",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = "gpt-4",
                Choices = new List<Choice>()
            };

            _mockCacheManager
                .Setup(x => x.GetAsync<ChatCompletionResponse>(It.IsAny<string>(), CacheRegion.LLMCompletion, default))
                .ThrowsAsync(new Exception("Cache error"));

            _mockInnerClient
                .Setup(x => x.CreateChatCompletionAsync(request, null, default))
                .ReturnsAsync(providerResponse);

            // Act
            var result = await client.CreateChatCompletionAsync(request);

            // Assert: Should fall back to provider despite cache error
            Assert.Same(providerResponse, result);
            _mockInnerClient.Verify(x => x.CreateChatCompletionAsync(request, null, default), Times.Once);
        }

        #endregion
    }
}
