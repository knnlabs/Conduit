using System;
using System.Threading.Tasks;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Http.EventHandlers;
using MassTransit;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ConduitLLM.Http.Tests.EventHandlers
{
    public class ImageGenerationFailedHandlerTests
    {
        private readonly Mock<IMemoryCache> _mockCache;
        private readonly Mock<IMediaStorageService> _mockStorageService;
        private readonly Mock<IPublishEndpoint> _mockPublishEndpoint;
        private readonly Mock<ILogger<ImageGenerationFailedHandler>> _mockLogger;

        public ImageGenerationFailedHandlerTests()
        {
            _mockCache = new Mock<IMemoryCache>();
            _mockStorageService = new Mock<IMediaStorageService>();
            _mockPublishEndpoint = new Mock<IPublishEndpoint>();
            _mockLogger = new Mock<ILogger<ImageGenerationFailedHandler>>();
        }
        
        private ImageGenerationFailedHandler CreateHandler()
        {
            return new ImageGenerationFailedHandler(
                _mockCache.Object,
                _mockStorageService.Object,
                _mockPublishEndpoint.Object,
                _mockLogger.Object);
        }

        [Fact]
        public async Task Consume_ShouldRemoveProgressCache_WhenFailed()
        {
            // Arrange
            var @event = new ImageGenerationFailed
            {
                TaskId = "test-task-123",
                VirtualKeyId = 42,
                Error = "Rate limit exceeded",
                ErrorCode = "RateLimitError",
                Provider = "openai",
                IsRetryable = true,
                AttemptCount = 1,
                CorrelationId = Guid.NewGuid().ToString()
            };

            var context = new Mock<ConsumeContext<ImageGenerationFailed>>();
            context.Setup(x => x.Message).Returns(@event);

            // Setup cache
            object cacheValue = 0;
            var failureCacheKey = $"image_generation_failures_{@event.Provider}";
            _mockCache.Setup(x => x.TryGetValue(failureCacheKey, out cacheValue)).Returns(false);
            
            var mockCacheEntry = new Mock<ICacheEntry>();
            mockCacheEntry.SetupSet(x => x.Value = It.IsAny<object>());
            mockCacheEntry.SetupSet(x => x.AbsoluteExpirationRelativeToNow = It.IsAny<TimeSpan?>());
            _mockCache.Setup(x => x.CreateEntry(failureCacheKey)).Returns(mockCacheEntry.Object);

            // Act
            var handler = CreateHandler();
            await handler.Consume(context.Object);

            // Assert
            _mockCache.Verify(x => x.Remove($"image_generation_progress_{@event.TaskId}"), Times.Once);
        }

        [Fact]
        public async Task Consume_ShouldTrackFailureMetrics_WhenFailed()
        {
            // Arrange
            var @event = new ImageGenerationFailed
            {
                TaskId = "test-task-123",
                VirtualKeyId = 42,
                Error = "API error",
                Provider = "minimax",
                IsRetryable = false,
                AttemptCount = 1
            };

            var context = new Mock<ConsumeContext<ImageGenerationFailed>>();
            context.Setup(x => x.Message).Returns(@event);

            // Setup cache to return existing failure count
            object failureCount = 5;
            var cacheKey = $"image_generation_failures_{@event.Provider}";
            _mockCache.Setup(x => x.TryGetValue(cacheKey, out failureCount)).Returns(true);
            
            var mockCacheEntry = new Mock<ICacheEntry>();
            mockCacheEntry.SetupSet(x => x.Value = It.IsAny<object>());
            mockCacheEntry.SetupSet(x => x.AbsoluteExpirationRelativeToNow = It.IsAny<TimeSpan?>());
            _mockCache.Setup(x => x.CreateEntry(cacheKey)).Returns(mockCacheEntry.Object);

            // Act
            var handler = CreateHandler();
            await handler.Consume(context.Object);

            // Assert
            mockCacheEntry.VerifySet(x => x.Value = 6, Times.Once);
            mockCacheEntry.VerifySet(x => x.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1), Times.Once);
        }

        [Fact]
        public async Task Consume_ShouldLogRetryIntention_WhenErrorIsRetryable()
        {
            // Arrange
            var @event = new ImageGenerationFailed
            {
                TaskId = "test-task-123",
                VirtualKeyId = 42,
                Error = "Temporary network error",
                Provider = "openai",
                IsRetryable = true,
                AttemptCount = 1
            };

            var context = new Mock<ConsumeContext<ImageGenerationFailed>>();
            context.Setup(x => x.Message).Returns(@event);

            object cacheValue = 0;
            var failureCacheKey = $"image_generation_failures_{@event.Provider}";
            _mockCache.Setup(x => x.TryGetValue(failureCacheKey, out cacheValue)).Returns(false);
            
            var mockCacheEntry = new Mock<ICacheEntry>();
            mockCacheEntry.SetupSet(x => x.Value = It.IsAny<object>());
            mockCacheEntry.SetupSet(x => x.AbsoluteExpirationRelativeToNow = It.IsAny<TimeSpan?>());
            _mockCache.Setup(x => x.CreateEntry(failureCacheKey)).Returns(mockCacheEntry.Object);

            // Act
            var handler = CreateHandler();
            await handler.Consume(context.Object);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Scheduling retry")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task Consume_ShouldLogPermanentFailure_WhenMaxAttemptsReached()
        {
            // Arrange
            var @event = new ImageGenerationFailed
            {
                TaskId = "test-task-123",
                VirtualKeyId = 42,
                Error = "Model not found",
                Provider = "replicate",
                IsRetryable = true,
                AttemptCount = 3 // Max attempts
            };

            var context = new Mock<ConsumeContext<ImageGenerationFailed>>();
            context.Setup(x => x.Message).Returns(@event);

            object cacheValue = 0;
            var failureCacheKey = $"image_generation_failures_{@event.Provider}";
            _mockCache.Setup(x => x.TryGetValue(failureCacheKey, out cacheValue)).Returns(false);
            
            var mockCacheEntry = new Mock<ICacheEntry>();
            mockCacheEntry.SetupSet(x => x.Value = It.IsAny<object>());
            mockCacheEntry.SetupSet(x => x.AbsoluteExpirationRelativeToNow = It.IsAny<TimeSpan?>());
            _mockCache.Setup(x => x.CreateEntry(failureCacheKey)).Returns(mockCacheEntry.Object);

            // Act
            var handler = CreateHandler();
            await handler.Consume(context.Object);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("permanently failed")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task Consume_ShouldAnalyzeErrorPatterns_WhenKnownErrorOccurs()
        {
            // Arrange
            var knownErrors = new[]
            {
                ("Rate limit exceeded", "rate limit"),
                ("Request timeout after 30s", "timeout"),
                ("Invalid API key provided", "invalid api key"),
                ("Insufficient credits in account", "insufficient credits"),
                ("Content policy violation", "content policy"),
                ("Model not found: dall-e-4", "model not found"),
                ("Invalid size 2048x2048", "invalid size")
            };

            var patternsDetected = new List<string>();

            foreach (var (error, expectedPattern) in knownErrors)
            {
                // Reset mocks for each iteration
                _mockCache.Reset();
                _mockLogger.Reset();
                
                // Setup logger to capture pattern detection
                _mockLogger.Setup(x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Error pattern detected")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
                    .Callback(() => patternsDetected.Add(expectedPattern));
                
                var @event = new ImageGenerationFailed
                {
                    TaskId = $"test-task-{expectedPattern}",
                    VirtualKeyId = 42,
                    Error = error,
                    Provider = "testprovider",
                    IsRetryable = false,
                    AttemptCount = 1
                };

                var context = new Mock<ConsumeContext<ImageGenerationFailed>>();
                context.Setup(x => x.Message).Returns(@event);

                object cacheValue = 0;
                var failureCacheKey = $"image_generation_failures_{@event.Provider}";
                _mockCache.Setup(x => x.TryGetValue(failureCacheKey, out cacheValue)).Returns(false);
                
                var mockCacheEntry = new Mock<ICacheEntry>();
                mockCacheEntry.SetupSet(x => x.Value = It.IsAny<object>());
                mockCacheEntry.SetupSet(x => x.AbsoluteExpirationRelativeToNow = It.IsAny<TimeSpan?>());
                _mockCache.Setup(x => x.CreateEntry(failureCacheKey)).Returns(mockCacheEntry.Object);

                // Act
                var handler = CreateHandler();
                await handler.Consume(context.Object);
            }

            // Assert
            Assert.Equal(knownErrors.Length, patternsDetected.Count);
        }

        [Fact]
        public async Task Consume_ShouldLogCriticalFailure_WhenAuthenticationFails()
        {
            // Arrange
            var criticalErrors = new[]
            {
                "Invalid API key",
                "Authentication failed",
                "Unauthorized access",
                "Forbidden resource",
                "Account suspended",
                "Insufficient credits"
            };

            foreach (var error in criticalErrors)
            {
                // Reset mocks for each iteration
                _mockCache.Reset();
                _mockLogger.Reset();
                
                var @event = new ImageGenerationFailed
                {
                    TaskId = $"test-task-critical",
                    VirtualKeyId = 42,
                    Error = error,
                    Provider = "openai",
                    IsRetryable = false,
                    AttemptCount = 1
                };

                var context = new Mock<ConsumeContext<ImageGenerationFailed>>();
                context.Setup(x => x.Message).Returns(@event);

                object cacheValue = 0;
                var failureCacheKey = $"image_generation_failures_{@event.Provider}";
                _mockCache.Setup(x => x.TryGetValue(failureCacheKey, out cacheValue)).Returns(false);
                
                var mockCacheEntry = new Mock<ICacheEntry>();
                mockCacheEntry.SetupSet(x => x.Value = It.IsAny<object>());
                mockCacheEntry.SetupSet(x => x.AbsoluteExpirationRelativeToNow = It.IsAny<TimeSpan?>());
                _mockCache.Setup(x => x.CreateEntry(failureCacheKey)).Returns(mockCacheEntry.Object);

                // Act
                var handler = CreateHandler();
                await handler.Consume(context.Object);

                // Assert
                _mockLogger.Verify(
                    x => x.Log(
                        LogLevel.Critical,
                        It.IsAny<EventId>(),
                        It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Critical image generation failure")),
                        It.IsAny<Exception>(),
                        It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                    Times.AtLeastOnce);
            }
        }

        [Fact]
        public async Task Consume_ShouldNotThrow_WhenCleanupFails()
        {
            // Arrange
            var @event = new ImageGenerationFailed
            {
                TaskId = "test-task-123",
                VirtualKeyId = 42,
                Error = "Generation failed",
                Provider = "minimax",
                IsRetryable = false,
                AttemptCount = 3
            };

            var context = new Mock<ConsumeContext<ImageGenerationFailed>>();
            context.Setup(x => x.Message).Returns(@event);

            object cacheValue = 0;
            var failureCacheKey = $"image_generation_failures_{@event.Provider}";
            _mockCache.Setup(x => x.TryGetValue(failureCacheKey, out cacheValue)).Returns(false);
            
            var mockCacheEntry = new Mock<ICacheEntry>();
            mockCacheEntry.SetupSet(x => x.Value = It.IsAny<object>());
            mockCacheEntry.SetupSet(x => x.AbsoluteExpirationRelativeToNow = It.IsAny<TimeSpan?>());
            _mockCache.Setup(x => x.CreateEntry(failureCacheKey)).Returns(mockCacheEntry.Object);

            // Setup storage service to throw during cleanup
            _mockStorageService.Setup(x => x.DeleteAsync(It.IsAny<string>()))
                .ThrowsAsync(new Exception("Storage error"));

            // Act - should not throw
            var handler = CreateHandler();
            await handler.Consume(context.Object);

            // Assert - verify error was logged but not thrown
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("permanently failed")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task Consume_ShouldThrowAndLogError_WhenExceptionOccurs()
        {
            // Arrange
            var @event = new ImageGenerationFailed
            {
                TaskId = "test-task-123",
                Error = "Test error"
            };

            var context = new Mock<ConsumeContext<ImageGenerationFailed>>();
            context.Setup(x => x.Message).Returns(@event);

            _mockCache.Setup(x => x.Remove(It.IsAny<object>()))
                .Throws(new InvalidOperationException("Cache error"));

            // Act & Assert
            var handler = CreateHandler();
            await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Consume(context.Object));

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Error processing image generation failure")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }
    }
}