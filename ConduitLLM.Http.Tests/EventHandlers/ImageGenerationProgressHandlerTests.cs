using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Http.EventHandlers;
using MassTransit;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ConduitLLM.Http.Tests.EventHandlers
{
    public class ImageGenerationProgressHandlerTests
    {
        private readonly Mock<IMemoryCache> _mockCache;
        private readonly Mock<IAsyncTaskService> _mockTaskService;
        private readonly Mock<ILogger<ImageGenerationProgressHandler>> _mockLogger;
        private readonly ImageGenerationProgressHandler _handler;

        public ImageGenerationProgressHandlerTests()
        {
            _mockCache = new Mock<IMemoryCache>();
            _mockTaskService = new Mock<IAsyncTaskService>();
            _mockLogger = new Mock<ILogger<ImageGenerationProgressHandler>>();
            _handler = new ImageGenerationProgressHandler(_mockCache.Object, _mockTaskService.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task Consume_ShouldCacheProgressData_WhenEventReceived()
        {
            // Arrange
            var @event = new ImageGenerationProgress
            {
                TaskId = "test-task-123",
                Status = "processing",
                ImagesCompleted = 2,
                TotalImages = 5,
                Message = "Processing image 3 of 5",
                CorrelationId = Guid.NewGuid().ToString()
            };

            var context = new Mock<ConsumeContext<ImageGenerationProgress>>();
            context.Setup(x => x.Message).Returns(@event);

            object cacheEntry = null;
            var mockCacheEntry = new Mock<ICacheEntry>();
            mockCacheEntry.SetupSet(x => x.Value = It.IsAny<object>()).Callback<object>(value => cacheEntry = value);
            mockCacheEntry.SetupSet(x => x.AbsoluteExpirationRelativeToNow = It.IsAny<TimeSpan?>());
            
            _mockCache.Setup(x => x.CreateEntry(It.IsAny<object>())).Returns(mockCacheEntry.Object);

            var taskStatus = new AsyncTaskStatus
            {
                TaskId = @event.TaskId,
                State = TaskState.Running,
                Result = new Dictionary<string, object>()
            };
            _mockTaskService.Setup(x => x.GetTaskStatusAsync(@event.TaskId, default)).ReturnsAsync(taskStatus);

            // Act
            await _handler.Consume(context.Object);

            // Assert
            _mockCache.Verify(x => x.CreateEntry($"image_generation_progress_{@event.TaskId}"), Times.Once);
            mockCacheEntry.VerifySet(x => x.Value = It.IsAny<object>(), Times.Once);
            mockCacheEntry.VerifySet(x => x.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1), Times.Once);
        }

        [Fact]
        public async Task Consume_ShouldUpdateTaskMetadata_WhenTaskExists()
        {
            // Arrange
            var @event = new ImageGenerationProgress
            {
                TaskId = "test-task-123",
                Status = "storing",
                ImagesCompleted = 3,
                TotalImages = 5
            };

            var context = new Mock<ConsumeContext<ImageGenerationProgress>>();
            context.Setup(x => x.Message).Returns(@event);

            var mockCacheEntry = new Mock<ICacheEntry>();
            _mockCache.Setup(x => x.CreateEntry(It.IsAny<object>())).Returns(mockCacheEntry.Object);

            var resultDict = new Dictionary<string, object>();
            var taskStatus = new AsyncTaskStatus
            {
                TaskId = @event.TaskId,
                State = TaskState.Running,
                Result = resultDict
            };
            _mockTaskService.Setup(x => x.GetTaskStatusAsync(@event.TaskId, default)).ReturnsAsync(taskStatus);

            // Act
            await _handler.Consume(context.Object);

            // Assert
            _mockTaskService.Verify(x => x.UpdateTaskStatusAsync(
                @event.TaskId,
                TaskState.Running,
                It.Is<IDictionary<string, object>>(d => d.ContainsKey("progress")),
                null,
                default),
                Times.Once);
        }

        [Fact]
        public async Task Consume_ShouldLogStartMessage_WhenProcessingBegins()
        {
            // Arrange
            var @event = new ImageGenerationProgress
            {
                TaskId = "test-task-123",
                Status = "processing",
                ImagesCompleted = 0,
                TotalImages = 10
            };

            var context = new Mock<ConsumeContext<ImageGenerationProgress>>();
            context.Setup(x => x.Message).Returns(@event);

            var mockCacheEntry = new Mock<ICacheEntry>();
            _mockCache.Setup(x => x.CreateEntry(It.IsAny<object>())).Returns(mockCacheEntry.Object);

            // Act
            await _handler.Consume(context.Object);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Image generation started")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task Consume_ShouldLogStoringMessage_WhenStoringImages()
        {
            // Arrange
            var @event = new ImageGenerationProgress
            {
                TaskId = "test-task-123",
                Status = "storing",
                ImagesCompleted = 2,
                TotalImages = 5
            };

            var context = new Mock<ConsumeContext<ImageGenerationProgress>>();
            context.Setup(x => x.Message).Returns(@event);

            var mockCacheEntry = new Mock<ICacheEntry>();
            _mockCache.Setup(x => x.CreateEntry(It.IsAny<object>())).Returns(mockCacheEntry.Object);

            // Act
            await _handler.Consume(context.Object);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Storing image")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task Consume_ShouldCalculateProgressPercentage_Correctly()
        {
            // Arrange
            var @event = new ImageGenerationProgress
            {
                TaskId = "test-task-123",
                Status = "processing",
                ImagesCompleted = 3,
                TotalImages = 4
            };

            var context = new Mock<ConsumeContext<ImageGenerationProgress>>();
            context.Setup(x => x.Message).Returns(@event);

            object capturedCacheValue = null;
            var mockCacheEntry = new Mock<ICacheEntry>();
            mockCacheEntry.SetupSet(x => x.Value = It.IsAny<object>())
                .Callback<object>(value => capturedCacheValue = value);
            
            _mockCache.Setup(x => x.CreateEntry(It.IsAny<object>())).Returns(mockCacheEntry.Object);

            // Act
            await _handler.Consume(context.Object);

            // Assert
            Assert.Equal(75, @event.ProgressPercentage);
        }

        [Fact]
        public async Task Consume_ShouldThrowAndLogError_WhenExceptionOccurs()
        {
            // Arrange
            var @event = new ImageGenerationProgress
            {
                TaskId = "test-task-123",
                Status = "processing"
            };

            var context = new Mock<ConsumeContext<ImageGenerationProgress>>();
            context.Setup(x => x.Message).Returns(@event);

            _mockCache.Setup(x => x.CreateEntry(It.IsAny<object>()))
                .Throws(new InvalidOperationException("Cache error"));

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.Consume(context.Object));

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Error processing image generation progress")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }
    }
}