using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Options;
using ConduitLLM.Core.Caching;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Interfaces.Configuration;
using ConduitLLM.Core.Models;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;

using Xunit;

namespace ConduitLLM.Tests.Caching
{
    public class CachingLLMClientTests
    {
        private readonly Mock<ILLMClient> _innerClientMock;
        private readonly Mock<ICacheService> _cacheServiceMock;
        private readonly Mock<ICacheMetricsService> _metricsServiceMock;
        private readonly Mock<IOptions<CacheOptions>> _cacheOptionsMock;
        private readonly Mock<ILogger<CachingLLMClient>> _loggerMock;
        private readonly CachingLLMClient _cachingClient;
        private readonly CacheOptions _cacheOptions;

        public CachingLLMClientTests()
        {
            _innerClientMock = new Mock<ILLMClient>();
            _cacheServiceMock = new Mock<ICacheService>();
            _metricsServiceMock = new Mock<ICacheMetricsService>();
            _loggerMock = new Mock<ILogger<CachingLLMClient>>();

            _cacheOptions = new CacheOptions
            {
                IsEnabled = true,
                DefaultExpirationMinutes = 60,
                IncludeModelInKey = true,
                IncludeApiKeyInKey = false,
                IncludeProviderInKey = true,
                IncludeTemperatureInKey = true,
                IncludeMaxTokensInKey = true,
                IncludeTopPInKey = true,
                HashAlgorithm = "MD5"
            };

            _cacheOptionsMock = new Mock<IOptions<CacheOptions>>();
            _cacheOptionsMock.Setup(o => o.Value).Returns(_cacheOptions);

            _cachingClient = new CachingLLMClient(
                _innerClientMock.Object,
                _cacheServiceMock.Object,
                _metricsServiceMock.Object,
                _cacheOptionsMock.Object,
                _loggerMock.Object
            );
        }

        [Fact]
        public async Task CreateChatCompletionAsync_ReturnsCachedResponse_WhenCacheHit()
        {
            // Arrange
            var request = new ChatCompletionRequest
            {
                Model = "test-model",
                Messages = new List<Message>
                {
                    new Message { Role = "user", Content = "Hello" }
                },
                Temperature = 0.7,
                MaxTokens = 100
            };

            var expectedResponse = new ChatCompletionResponse
            {
                Id = "test-id",
                Object = "chat.completion",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = "test-model",
                Choices = new List<Choice>
                {
                    new Choice
                    {
                        Index = 0,
                        Message = new Message { Role = "assistant", Content = "Hi there!" },
                        FinishReason = "stop"
                    }
                }
            };

            // Setup cache hit
            _cacheServiceMock
                .Setup(c => c.Get<ChatCompletionResponse>(It.IsAny<string>()))
                .Returns(expectedResponse);

            // Act
            var result = await _cachingClient.CreateChatCompletionAsync(request);

            // Assert
            Assert.Equal(expectedResponse.Id, result.Id);

            // Handle different types of content (string vs object)
            var expectedContent = expectedResponse.Choices[0].Message.Content?.ToString();
            var resultContent = result.Choices[0].Message.Content?.ToString();
            Assert.Equal(expectedContent, resultContent);

            // Verify cache was checked
            _cacheServiceMock.Verify(c => c.Get<ChatCompletionResponse>(It.IsAny<string>()), Times.Once);

            // Verify inner client was not called
            _innerClientMock.Verify(c => c.CreateChatCompletionAsync(
                It.IsAny<ChatCompletionRequest>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
                Times.Never);

            // Verify metrics were recorded
            _metricsServiceMock.Verify(m => m.RecordHit(
                It.IsAny<double>(),
                It.Is<string>(s => s == "test-model")),
                Times.Once);
        }

        [Fact]
        public async Task CreateChatCompletionAsync_CallsInnerClient_WhenCacheMiss()
        {
            // Arrange
            var request = new ChatCompletionRequest
            {
                Model = "test-model",
                Messages = new List<Message>
                {
                    new Message { Role = "user", Content = "Hello" }
                },
                Temperature = 0.7,
                MaxTokens = 100
            };

            var expectedResponse = new ChatCompletionResponse
            {
                Id = "test-id",
                Object = "chat.completion",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = "test-model",
                Choices = new List<Choice>
                {
                    new Choice
                    {
                        Index = 0,
                        Message = new Message { Role = "assistant", Content = "Hi there!" },
                        FinishReason = "stop"
                    }
                }
            };

            // Setup cache miss
            _cacheServiceMock
                .Setup(c => c.Get<ChatCompletionResponse>(It.IsAny<string>()))
                .Returns((ChatCompletionResponse?)null);

            // Setup inner client response
            _innerClientMock
                .Setup(c => c.CreateChatCompletionAsync(
                    It.IsAny<ChatCompletionRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _cachingClient.CreateChatCompletionAsync(request);

            // Assert
            Assert.Equal(expectedResponse.Id, result.Id);

            // Handle different types of content (string vs object)
            var expectedContent = expectedResponse.Choices[0].Message.Content?.ToString();
            var resultContent = result.Choices[0].Message.Content?.ToString();
            Assert.Equal(expectedContent, resultContent);

            // Verify cache was checked
            _cacheServiceMock.Verify(c => c.Get<ChatCompletionResponse>(It.IsAny<string>()), Times.Once);

            // Verify inner client was called
            _innerClientMock.Verify(c => c.CreateChatCompletionAsync(
                It.IsAny<ChatCompletionRequest>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
                Times.Once);

            // Verify response was cached
            _cacheServiceMock.Verify(c => c.Set(
                It.IsAny<string>(),
                It.Is<ChatCompletionResponse>(r => r.Id == expectedResponse.Id),
                It.IsAny<TimeSpan?>(),
                It.IsAny<TimeSpan?>()),
                Times.Once);

            // Verify metrics were recorded
            _metricsServiceMock.Verify(m => m.RecordMiss(
                It.Is<string>(s => s == "test-model")),
                Times.Once);
        }

        [Fact]
        public async Task CreateChatCompletionAsync_BypassesCache_WhenStreamingIsEnabled()
        {
            // Arrange
            var request = new ChatCompletionRequest
            {
                Model = "test-model",
                Messages = new List<Message>
                {
                    new Message { Role = "user", Content = "Hello" }
                },
                Stream = true
            };

            var expectedResponse = new ChatCompletionResponse
            {
                Id = "test-id",
                Object = "chat.completion",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = "test-model",
                Choices = new List<Choice>
                {
                    new Choice
                    {
                        Index = 0,
                        Message = new Message { Role = "assistant", Content = "Hi there!" },
                        FinishReason = "stop"
                    }
                }
            };

            // Setup inner client response
            _innerClientMock
                .Setup(c => c.CreateChatCompletionAsync(
                    It.IsAny<ChatCompletionRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _cachingClient.CreateChatCompletionAsync(request);

            // Assert
            Assert.Equal(expectedResponse.Id, result.Id);

            // Verify cache was not checked
            _cacheServiceMock.Verify(c => c.Get<ChatCompletionResponse>(It.IsAny<string>()), Times.Never);

            // Verify inner client was called directly
            _innerClientMock.Verify(c => c.CreateChatCompletionAsync(
                It.IsAny<ChatCompletionRequest>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
                Times.Once);

            // Verify response was not cached
            _cacheServiceMock.Verify(c => c.Set(
                It.IsAny<string>(),
                It.IsAny<ChatCompletionResponse>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<TimeSpan?>()),
                Times.Never);

            // Verify metrics were not recorded
            _metricsServiceMock.Verify(m => m.RecordHit(
                It.IsAny<double>(),
                It.IsAny<string>()),
                Times.Never);

            _metricsServiceMock.Verify(m => m.RecordMiss(
                It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public async Task CreateChatCompletionAsync_BypassesCache_WhenCachingIsDisabled()
        {
            // Arrange
            var disabledCacheOptions = new CacheOptions
            {
                IsEnabled = false
            };

            var cacheOptionsMock = new Mock<IOptions<CacheOptions>>();
            cacheOptionsMock.Setup(o => o.Value).Returns(disabledCacheOptions);

            var clientWithDisabledCache = new CachingLLMClient(
                _innerClientMock.Object,
                _cacheServiceMock.Object,
                _metricsServiceMock.Object,
                cacheOptionsMock.Object,
                _loggerMock.Object
            );

            var request = new ChatCompletionRequest
            {
                Model = "test-model",
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
                Model = "test-model",
                Choices = new List<Choice>
                {
                    new Choice
                    {
                        Index = 0,
                        Message = new Message { Role = "assistant", Content = "Hi there!" },
                        FinishReason = "stop"
                    }
                }
            };

            // Setup inner client response
            _innerClientMock
                .Setup(c => c.CreateChatCompletionAsync(
                    It.IsAny<ChatCompletionRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await clientWithDisabledCache.CreateChatCompletionAsync(request);

            // Assert
            Assert.Equal(expectedResponse.Id, result.Id);

            // Verify cache was not checked
            _cacheServiceMock.Verify(c => c.Get<ChatCompletionResponse>(It.IsAny<string>()), Times.Never);

            // Verify inner client was called directly
            _innerClientMock.Verify(c => c.CreateChatCompletionAsync(
                It.IsAny<ChatCompletionRequest>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
                Times.Once);

            // Verify response was not cached
            _cacheServiceMock.Verify(c => c.Set(
                It.IsAny<string>(),
                It.IsAny<ChatCompletionResponse>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<TimeSpan?>()),
                Times.Never);
        }

        [Fact]
        public async Task ListModelsAsync_ReturnsCachedResponse_WhenCacheHit()
        {
            // Arrange
            var expectedModels = new List<string> { "model1", "model2" };

            // Setup cache service to return cached models
            _cacheServiceMock
                .Setup(c => c.GetOrCreateAsync<List<string>>(
                    It.IsAny<string>(),
                    It.IsAny<Func<Task<List<string>>>>(),
                    It.IsAny<TimeSpan?>(),
                    It.IsAny<TimeSpan?>()))
                .ReturnsAsync(expectedModels);

            // Act
            var result = await _cachingClient.ListModelsAsync();

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Equal("model1", result[0]);
            Assert.Equal("model2", result[1]);

            // Verify cache was used
            _cacheServiceMock.Verify(c => c.GetOrCreateAsync<List<string>>(
                It.IsAny<string>(),
                It.IsAny<Func<Task<List<string>>>>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<TimeSpan?>()),
                Times.Once);

            // Verify inner client was not called directly
            _innerClientMock.Verify(c => c.ListModelsAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task ListModelsAsync_CallsInnerClient_WhenCachingDisabled()
        {
            // Arrange
            var disabledCacheOptions = new CacheOptions
            {
                IsEnabled = false
            };

            var cacheOptionsMock = new Mock<IOptions<CacheOptions>>();
            cacheOptionsMock.Setup(o => o.Value).Returns(disabledCacheOptions);

            var clientWithDisabledCache = new CachingLLMClient(
                _innerClientMock.Object,
                _cacheServiceMock.Object,
                _metricsServiceMock.Object,
                cacheOptionsMock.Object,
                _loggerMock.Object
            );

            var expectedModels = new List<string> { "model1", "model2" };

            // Setup inner client response
            _innerClientMock
                .Setup(c => c.ListModelsAsync(null, default))
                .ReturnsAsync(expectedModels);

            // Act
            var result = await clientWithDisabledCache.ListModelsAsync();

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Equal("model1", result[0]);
            Assert.Equal("model2", result[1]);

            // Verify cache was not used
            _cacheServiceMock.Verify(c => c.GetOrCreateAsync<List<string>>(
                It.IsAny<string>(),
                It.IsAny<Func<Task<List<string>>>>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<TimeSpan?>()),
                Times.Never);

            // Verify inner client was called directly
            _innerClientMock.Verify(c => c.ListModelsAsync(null, default), Times.Once);
        }

        [Fact]
        public void StreamChatCompletionAsync_AlwaysCallsInnerClient()
        {
            // Arrange
            var request = new ChatCompletionRequest
            {
                Model = "test-model",
                Messages = new List<Message>
                {
                    new Message { Role = "user", Content = "Hello" }
                },
                Stream = true
            };

            // Setup inner client response - just verify the method is called
            _innerClientMock
                .Setup(c => c.StreamChatCompletionAsync(
                    It.IsAny<ChatCompletionRequest>(),
                    null,
                    default));

            // Act - just call the method, don't await the result
            _cachingClient.StreamChatCompletionAsync(request);

            // Assert
            // Verify inner client was called directly
            _innerClientMock.Verify(c => c.StreamChatCompletionAsync(
                It.IsAny<ChatCompletionRequest>(),
                null,
                default),
                Times.Once);

            // Verify cache was not used
            _cacheServiceMock.Verify(c => c.Get<ChatCompletionResponse>(It.IsAny<string>()), Times.Never);
            _cacheServiceMock.Verify(c => c.Set(
                It.IsAny<string>(),
                It.IsAny<ChatCompletionResponse>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<TimeSpan?>()),
                Times.Never);
        }
    }
}
