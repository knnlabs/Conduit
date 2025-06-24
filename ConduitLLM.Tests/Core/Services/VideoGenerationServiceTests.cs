using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;
using MassTransit;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Core.Services
{
    public class VideoGenerationServiceTests
    {
        private readonly Mock<ILLMClientFactory> _clientFactoryMock;
        private readonly Mock<IModelCapabilityService> _capabilityServiceMock;
        private readonly Mock<ICostCalculationService> _costServiceMock;
        private readonly Mock<IVirtualKeyService> _virtualKeyServiceMock;
        private readonly Mock<IMediaStorageService> _mediaStorageMock;
        private readonly Mock<IAsyncTaskService> _taskServiceMock;
        private readonly Mock<ICancellableTaskRegistry> _taskRegistryMock;
        private readonly Mock<IPublishEndpoint> _publishEndpointMock;
        private readonly Mock<ILogger<VideoGenerationService>> _loggerMock;
        private readonly VideoGenerationService _service;

        public VideoGenerationServiceTests()
        {
            _clientFactoryMock = new Mock<ILLMClientFactory>();
            _capabilityServiceMock = new Mock<IModelCapabilityService>();
            _costServiceMock = new Mock<ICostCalculationService>();
            _virtualKeyServiceMock = new Mock<IVirtualKeyService>();
            _mediaStorageMock = new Mock<IMediaStorageService>();
            _taskServiceMock = new Mock<IAsyncTaskService>();
            _taskRegistryMock = new Mock<ICancellableTaskRegistry>();
            _publishEndpointMock = new Mock<IPublishEndpoint>();
            _loggerMock = new Mock<ILogger<VideoGenerationService>>();

            _service = new VideoGenerationService(
                _clientFactoryMock.Object,
                _capabilityServiceMock.Object,
                _costServiceMock.Object,
                _virtualKeyServiceMock.Object,
                _mediaStorageMock.Object,
                _taskServiceMock.Object,
                _loggerMock.Object,
                _publishEndpointMock.Object,
                _taskRegistryMock.Object);
        }

        [Fact]
        public async Task GetVideoGenerationStatusAsync_PendingTask_ReturnsPendingResponse()
        {
            // Arrange
            var taskId = "test-task-123";
            var virtualKey = "test-key";
            var taskStatus = new AsyncTaskStatus
            {
                TaskId = taskId,
                TaskType = "video_generation",
                State = TaskState.Pending,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Progress = 10,
                ProgressMessage = "Video generation starting..."
            };

            _taskServiceMock.Setup(x => x.GetTaskStatusAsync(taskId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(taskStatus);

            // Act
            var result = await _service.GetVideoGenerationStatusAsync(taskId, virtualKey);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result.Data);
            Assert.Equal($"pending:{taskId}", result.Data[0].Url);
            Assert.Equal("Video generation starting...", result.Data[0].RevisedPrompt);
            Assert.Equal(0, result.Usage?.VideosGenerated);
        }

        [Fact]
        public async Task GetVideoGenerationStatusAsync_ProcessingTask_ReturnsProcessingResponse()
        {
            // Arrange
            var taskId = "test-task-123";
            var virtualKey = "test-key";
            var taskStatus = new AsyncTaskStatus
            {
                TaskId = taskId,
                TaskType = "video_generation",
                State = TaskState.Processing,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Progress = 50,
                ProgressMessage = "Processing video frames..."
            };

            _taskServiceMock.Setup(x => x.GetTaskStatusAsync(taskId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(taskStatus);

            // Act
            var result = await _service.GetVideoGenerationStatusAsync(taskId, virtualKey);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result.Data);
            Assert.Equal($"pending:{taskId}", result.Data[0].Url);
            Assert.Equal("Processing video frames...", result.Data[0].RevisedPrompt);
        }

        [Fact]
        public async Task GetVideoGenerationStatusAsync_CompletedTask_ReturnsVideoResponse()
        {
            // Arrange
            var taskId = "test-task-123";
            var virtualKey = "test-key";
            var expectedResponse = new VideoGenerationResponse
            {
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Data = new List<VideoData>
                {
                    new VideoData
                    {
                        Url = "https://example.com/video.mp4",
                        Metadata = new VideoMetadata
                        {
                            Width = 1920,
                            Height = 1080,
                            Duration = 5.0,
                            Fps = 30,
                            FileSizeBytes = 1024000,
                            MimeType = "video/mp4"
                        }
                    }
                },
                Model = "minimax-video-01",
                Usage = new VideoGenerationUsage
                {
                    VideosGenerated = 1,
                    TotalDurationSeconds = 5.0
                }
            };

            // Serialize to JsonElement to simulate real storage
            var jsonString = JsonSerializer.Serialize(expectedResponse);
            var jsonElement = JsonSerializer.Deserialize<JsonElement>(jsonString);

            var taskStatus = new AsyncTaskStatus
            {
                TaskId = taskId,
                TaskType = "video_generation",
                State = TaskState.Completed,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow,
                Result = jsonElement
            };

            _taskServiceMock.Setup(x => x.GetTaskStatusAsync(taskId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(taskStatus);

            // Act
            var result = await _service.GetVideoGenerationStatusAsync(taskId, virtualKey);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result.Data);
            Assert.Equal("https://example.com/video.mp4", result.Data[0].Url);
            Assert.Equal(1920, result.Data[0].Metadata?.Width);
            Assert.Equal(1080, result.Data[0].Metadata?.Height);
            Assert.Equal(5.0, result.Data[0].Metadata?.Duration);
            Assert.Equal("minimax-video-01", result.Model);
            Assert.Equal(1, result.Usage?.VideosGenerated);
        }

        [Fact]
        public async Task GetVideoGenerationStatusAsync_FailedTask_ThrowsException()
        {
            // Arrange
            var taskId = "test-task-123";
            var virtualKey = "test-key";
            var errorMessage = "Video generation failed: insufficient credits";
            var taskStatus = new AsyncTaskStatus
            {
                TaskId = taskId,
                TaskType = "video_generation",
                State = TaskState.Failed,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Error = errorMessage
            };

            _taskServiceMock.Setup(x => x.GetTaskStatusAsync(taskId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(taskStatus);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _service.GetVideoGenerationStatusAsync(taskId, virtualKey));
            Assert.Contains(errorMessage, exception.Message);
        }

        [Fact]
        public async Task GetVideoGenerationStatusAsync_CancelledTask_ThrowsOperationCancelledException()
        {
            // Arrange
            var taskId = "test-task-123";
            var virtualKey = "test-key";
            var taskStatus = new AsyncTaskStatus
            {
                TaskId = taskId,
                TaskType = "video_generation",
                State = TaskState.Cancelled,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _taskServiceMock.Setup(x => x.GetTaskStatusAsync(taskId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(taskStatus);

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(
                () => _service.GetVideoGenerationStatusAsync(taskId, virtualKey));
        }

        [Fact]
        public async Task GetVideoGenerationStatusAsync_TimedOutTask_ThrowsTimeoutException()
        {
            // Arrange
            var taskId = "test-task-123";
            var virtualKey = "test-key";
            var taskStatus = new AsyncTaskStatus
            {
                TaskId = taskId,
                TaskType = "video_generation",
                State = TaskState.TimedOut,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _taskServiceMock.Setup(x => x.GetTaskStatusAsync(taskId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(taskStatus);

            // Act & Assert
            await Assert.ThrowsAsync<TimeoutException>(
                () => _service.GetVideoGenerationStatusAsync(taskId, virtualKey));
        }

        [Fact]
        public async Task GetVideoGenerationStatusAsync_TaskNotFound_ThrowsException()
        {
            // Arrange
            var taskId = "non-existent-task";
            var virtualKey = "test-key";

            _taskServiceMock.Setup(x => x.GetTaskStatusAsync(taskId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((AsyncTaskStatus?)null);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _service.GetVideoGenerationStatusAsync(taskId, virtualKey));
            Assert.Contains("not found", exception.Message);
        }

        [Fact]
        public async Task GetVideoGenerationStatusAsync_CompletedTaskWithNullResult_ThrowsException()
        {
            // Arrange
            var taskId = "test-task-123";
            var virtualKey = "test-key";
            var taskStatus = new AsyncTaskStatus
            {
                TaskId = taskId,
                TaskType = "video_generation",
                State = TaskState.Completed,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow,
                Result = null
            };

            _taskServiceMock.Setup(x => x.GetTaskStatusAsync(taskId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(taskStatus);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _service.GetVideoGenerationStatusAsync(taskId, virtualKey));
            Assert.Contains("has no result", exception.Message);
        }

        [Fact]
        public async Task CancelVideoGenerationAsync_WithTaskInRegistry_CancelsAndUpdatesStatus()
        {
            // Arrange
            var taskId = "test-task-123";
            var virtualKey = "test-key";

            _taskRegistryMock.Setup(x => x.TryCancel(taskId))
                .Returns(true);

            _taskServiceMock.Setup(x => x.UpdateTaskStatusAsync(
                    taskId,
                    TaskState.Cancelled,
                    It.IsAny<int?>(),
                    It.IsAny<object?>(),
                    "User requested cancellation",
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _service.CancelVideoGenerationAsync(taskId, virtualKey);

            // Assert
            Assert.True(result);
            _taskRegistryMock.Verify(x => x.TryCancel(taskId), Times.Once);
            _taskServiceMock.Verify(x => x.UpdateTaskStatusAsync(
                taskId,
                TaskState.Cancelled,
                It.IsAny<int?>(),
                It.IsAny<object?>(),
                "User requested cancellation",
                It.IsAny<CancellationToken>()), Times.Once);
            _publishEndpointMock.Verify(x => x.Publish(
                It.Is<VideoGenerationCancelled>(evt => 
                    evt.RequestId == taskId &&
                    evt.Reason == "User requested cancellation"),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CancelVideoGenerationAsync_TaskNotInRegistry_StillUpdatesStatus()
        {
            // Arrange
            var taskId = "test-task-123";
            var virtualKey = "test-key";

            _taskRegistryMock.Setup(x => x.TryCancel(taskId))
                .Returns(false);

            _taskServiceMock.Setup(x => x.UpdateTaskStatusAsync(
                    taskId,
                    TaskState.Cancelled,
                    It.IsAny<int?>(),
                    It.IsAny<object?>(),
                    "User requested cancellation",
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _service.CancelVideoGenerationAsync(taskId, virtualKey);

            // Assert
            Assert.True(result);
            _taskRegistryMock.Verify(x => x.TryCancel(taskId), Times.Once);
            _taskServiceMock.Verify(x => x.UpdateTaskStatusAsync(
                taskId,
                TaskState.Cancelled,
                It.IsAny<int?>(),
                It.IsAny<object?>(),
                "User requested cancellation",
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CancelVideoGenerationAsync_NoRegistry_OnlyUpdatesStatus()
        {
            // Arrange
            var taskId = "test-task-123";
            var virtualKey = "test-key";

            // Create service without task registry
            var service = new VideoGenerationService(
                _clientFactoryMock.Object,
                _capabilityServiceMock.Object,
                _costServiceMock.Object,
                _virtualKeyServiceMock.Object,
                _mediaStorageMock.Object,
                _taskServiceMock.Object,
                _loggerMock.Object,
                _publishEndpointMock.Object,
                null); // No task registry

            _taskServiceMock.Setup(x => x.UpdateTaskStatusAsync(
                    taskId,
                    TaskState.Cancelled,
                    It.IsAny<int?>(),
                    It.IsAny<object?>(),
                    "User requested cancellation",
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await service.CancelVideoGenerationAsync(taskId, virtualKey);

            // Assert
            Assert.True(result);
            _taskRegistryMock.Verify(x => x.TryCancel(It.IsAny<string>()), Times.Never);
            _taskServiceMock.Verify(x => x.UpdateTaskStatusAsync(
                taskId,
                TaskState.Cancelled,
                It.IsAny<int?>(),
                It.IsAny<object?>(),
                "User requested cancellation",
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CancelVideoGenerationAsync_UpdateStatusFails_ReturnsFalse()
        {
            // Arrange
            var taskId = "test-task-123";
            var virtualKey = "test-key";

            _taskRegistryMock.Setup(x => x.TryCancel(taskId))
                .Returns(false);

            _taskServiceMock.Setup(x => x.UpdateTaskStatusAsync(
                    taskId,
                    TaskState.Cancelled,
                    It.IsAny<int?>(),
                    It.IsAny<object?>(),
                    "User requested cancellation",
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Update failed"));

            // Act
            var result = await _service.CancelVideoGenerationAsync(taskId, virtualKey);

            // Assert
            Assert.False(result);
            _taskRegistryMock.Verify(x => x.TryCancel(taskId), Times.Once);
            _publishEndpointMock.Verify(x => x.Publish(
                It.Is<VideoGenerationCancelled>(evt => 
                    evt.RequestId == taskId &&
                    evt.Reason == "User requested cancellation"),
                It.IsAny<CancellationToken>()), Times.Once); // Event is still published
        }
    }
}