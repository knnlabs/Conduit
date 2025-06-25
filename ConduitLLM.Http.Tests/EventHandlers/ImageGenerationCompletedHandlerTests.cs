using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ConduitLLM.Core.Events;
using ConduitLLM.Http.EventHandlers;
using ConduitLLM.Http.Services;
using MassTransit;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ConduitLLM.Http.Tests.EventHandlers
{
    public class ImageGenerationCompletedHandlerTests
    {
        private readonly Mock<IMemoryCache> _mockCache;
        private readonly Mock<IImageGenerationNotificationService> _mockNotificationService;
        private readonly Mock<ILogger<ImageGenerationCompletedHandler>> _mockLogger;
        private readonly ImageGenerationCompletedHandler _handler;

        public ImageGenerationCompletedHandlerTests()
        {
            _mockCache = new Mock<IMemoryCache>();
            _mockNotificationService = new Mock<IImageGenerationNotificationService>();
            _mockLogger = new Mock<ILogger<ImageGenerationCompletedHandler>>();
            _handler = new ImageGenerationCompletedHandler(_mockCache.Object, _mockNotificationService.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task Consume_ShouldRemoveProgressCache_WhenCompleted()
        {
            // Arrange
            var @event = new ImageGenerationCompleted
            {
                TaskId = "test-task-123",
                VirtualKeyId = 42,
                Images = new List<ImageData>
                {
                    new ImageData { Url = "https://example.com/image1.png" },
                    new ImageData { Url = "https://example.com/image2.png" }
                },
                Duration = TimeSpan.FromSeconds(30),
                Cost = 0.08m,
                Provider = "openai",
                Model = "dall-e-3",
                CorrelationId = Guid.NewGuid().ToString()
            };

            var context = new Mock<ConsumeContext<ImageGenerationCompleted>>();
            context.Setup(x => x.Message).Returns(@event);

            // Setup cache mocks
            object cacheValue = null;
            _mockCache.Setup(x => x.TryGetValue(It.IsAny<object>(), out cacheValue)).Returns(false);
            
            var mockCacheEntry = new Mock<ICacheEntry>();
            _mockCache.Setup(x => x.CreateEntry(It.IsAny<object>())).Returns(mockCacheEntry.Object);

            // Act
            await _handler.Consume(context.Object);

            // Assert
            _mockCache.Verify(x => x.Remove($"image_generation_progress_{@event.TaskId}"), Times.Once);
        }

        [Fact]
        public async Task Consume_ShouldUpdateCompletedTasksCache_WhenCompleted()
        {
            // Arrange
            var @event = new ImageGenerationCompleted
            {
                TaskId = "test-task-123",
                VirtualKeyId = 42,
                Images = new List<ImageData> { new ImageData { Url = "https://example.com/image.png" } },
                Duration = TimeSpan.FromSeconds(15),
                Cost = 0.04m,
                Provider = "minimax",
                Model = "image-01"
            };

            var context = new Mock<ConsumeContext<ImageGenerationCompleted>>();
            context.Setup(x => x.Message).Returns(@event);

            // Setup cache to return empty list initially
            object cacheValue = new List<object>();
            _mockCache.Setup(x => x.TryGetValue("completed_image_tasks", out cacheValue)).Returns(true);
            
            var mockCacheEntry = new Mock<ICacheEntry>();
            object capturedValue = null;
            mockCacheEntry.SetupSet(x => x.Value = It.IsAny<object>())
                .Callback<object>(value => capturedValue = value);
            
            _mockCache.Setup(x => x.CreateEntry("completed_image_tasks")).Returns(mockCacheEntry.Object);

            // Act
            await _handler.Consume(context.Object);

            // Assert
            _mockCache.Verify(x => x.CreateEntry("completed_image_tasks"), Times.Once);
            mockCacheEntry.VerifySet(x => x.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24), Times.Once);
            Assert.NotNull(capturedValue);
            Assert.IsType<List<object>>(capturedValue);
        }

        [Fact]
        public async Task Consume_ShouldLogPerformanceMetrics_WhenCompleted()
        {
            // Arrange
            var @event = new ImageGenerationCompleted
            {
                TaskId = "test-task-123",
                VirtualKeyId = 42,
                Images = new List<ImageData>
                {
                    new ImageData { Url = "url1" },
                    new ImageData { Url = "url2" },
                    new ImageData { Url = "url3" }
                },
                Duration = TimeSpan.FromSeconds(60),
                Cost = 0.12m,
                Provider = "openai",
                Model = "dall-e-3"
            };

            var context = new Mock<ConsumeContext<ImageGenerationCompleted>>();
            context.Setup(x => x.Message).Returns(@event);

            // Setup cache mocks
            object cacheValue = null;
            _mockCache.Setup(x => x.TryGetValue(It.IsAny<object>(), out cacheValue)).Returns(false);
            var mockCacheEntry = new Mock<ICacheEntry>();
            _mockCache.Setup(x => x.CreateEntry(It.IsAny<object>())).Returns(mockCacheEntry.Object);

            // Act
            await _handler.Consume(context.Object);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Image generation performance")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
            
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Image generation metrics")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task Consume_ShouldMaintainRollingListLimit_WhenManyTasksComplete()
        {
            // Arrange
            var @event = new ImageGenerationCompleted
            {
                TaskId = "test-task-999",
                VirtualKeyId = 42,
                Images = new List<ImageData> { new ImageData { Url = "url" } },
                Duration = TimeSpan.FromSeconds(10),
                Cost = 0.02m,
                Provider = "replicate",
                Model = "flux"
            };

            var context = new Mock<ConsumeContext<ImageGenerationCompleted>>();
            context.Setup(x => x.Message).Returns(@event);

            // Setup cache to return list with 100 items
            var existingTasks = Enumerable.Range(1, 100).Select(i => new { TaskId = $"task-{i}" }).Cast<object>().ToList();
            object cacheValue = existingTasks;
            _mockCache.Setup(x => x.TryGetValue("completed_image_tasks", out cacheValue)).Returns(true);
            
            var mockCacheEntry = new Mock<ICacheEntry>();
            object capturedValue = null;
            mockCacheEntry.SetupSet(x => x.Value = It.IsAny<object>())
                .Callback<object>(value => capturedValue = value);
            
            _mockCache.Setup(x => x.CreateEntry("completed_image_tasks")).Returns(mockCacheEntry.Object);

            // Act
            await _handler.Consume(context.Object);

            // Assert
            Assert.NotNull(capturedValue);
            var resultList = capturedValue as List<object>;
            Assert.NotNull(resultList);
            Assert.Equal(100, resultList.Count); // Should maintain limit of 100
        }

        [Fact]
        public async Task Consume_ShouldThrowAndLogError_WhenExceptionOccurs()
        {
            // Arrange
            var @event = new ImageGenerationCompleted
            {
                TaskId = "test-task-123",
                Images = new List<ImageData>()
            };

            var context = new Mock<ConsumeContext<ImageGenerationCompleted>>();
            context.Setup(x => x.Message).Returns(@event);

            _mockCache.Setup(x => x.Remove(It.IsAny<object>()))
                .Throws(new InvalidOperationException("Cache error"));

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.Consume(context.Object));

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Error processing image generation completion")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }
    }
}