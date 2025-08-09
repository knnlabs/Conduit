using System;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using MassTransit;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Core.Services
{
    public partial class VideoGenerationOrchestratorTests
    {
        #region VideoGenerationCancelled Event Tests

        [Fact]
        public async Task Consume_VideoGenerationCancelled_WithExistingTask_ShouldUpdateStatusToCancelled()
        {
            // Arrange
            var cancellation = new VideoGenerationCancelled
            {
                RequestId = "test-request-id",
                Reason = "User requested cancellation",
                CorrelationId = "test-correlation-id"
            };

            var context = new Mock<ConsumeContext<VideoGenerationCancelled>>();
            context.Setup(x => x.Message).Returns(cancellation);

            _mockTaskRegistry.Setup(x => x.TryCancel("test-request-id"))
                .Returns(true);

            // Act
            await _orchestrator.Consume(context.Object);

            // Assert
            _mockTaskRegistry.Verify(x => x.TryCancel("test-request-id"), Times.Once);
            _mockTaskService.Verify(x => x.UpdateTaskStatusAsync(
                "test-request-id",
                TaskState.Cancelled,
                null,
                null,
                "User requested cancellation",
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Consume_VideoGenerationCancelled_WithNonExistentTask_ShouldUpdateStatusToCancelled()
        {
            // Arrange
            var cancellation = new VideoGenerationCancelled
            {
                RequestId = "non-existent-request-id",
                Reason = "User requested cancellation",
                CorrelationId = "test-correlation-id"
            };

            var context = new Mock<ConsumeContext<VideoGenerationCancelled>>();
            context.Setup(x => x.Message).Returns(cancellation);

            _mockTaskRegistry.Setup(x => x.TryCancel("non-existent-request-id"))
                .Returns(false);

            // Act
            await _orchestrator.Consume(context.Object);

            // Assert
            _mockTaskRegistry.Verify(x => x.TryCancel("non-existent-request-id"), Times.Once);
            _mockTaskService.Verify(x => x.UpdateTaskStatusAsync(
                "non-existent-request-id",
                TaskState.Cancelled,
                null,
                null,
                "User requested cancellation",
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Consume_VideoGenerationCancelled_WithDefaultReason_ShouldUseDefaultMessage()
        {
            // Arrange
            var cancellation = new VideoGenerationCancelled
            {
                RequestId = "test-request-id",
                Reason = null, // No reason provided
                CorrelationId = "test-correlation-id"
            };

            var context = new Mock<ConsumeContext<VideoGenerationCancelled>>();
            context.Setup(x => x.Message).Returns(cancellation);

            _mockTaskRegistry.Setup(x => x.TryCancel("test-request-id"))
                .Returns(true);

            // Act
            await _orchestrator.Consume(context.Object);

            // Assert
            _mockTaskService.Verify(x => x.UpdateTaskStatusAsync(
                "test-request-id",
                TaskState.Cancelled,
                null,
                null,
                "User requested cancellation",
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Consume_VideoGenerationCancelled_WithTaskServiceException_ShouldLogError()
        {
            // Arrange
            var cancellation = new VideoGenerationCancelled
            {
                RequestId = "test-request-id",
                Reason = "User requested cancellation",
                CorrelationId = "test-correlation-id"
            };

            var context = new Mock<ConsumeContext<VideoGenerationCancelled>>();
            context.Setup(x => x.Message).Returns(cancellation);

            _mockTaskRegistry.Setup(x => x.TryCancel("test-request-id"))
                .Returns(true);

            _mockTaskService.Setup(x => x.UpdateTaskStatusAsync(
                "test-request-id",
                TaskState.Cancelled,
                null,
                null,
                "User requested cancellation",
                It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Task service error"));

            // Act
            await _orchestrator.Consume(context.Object);

            // Assert
            _mockTaskRegistry.Verify(x => x.TryCancel("test-request-id"), Times.Once);
            _mockTaskService.Verify(x => x.UpdateTaskStatusAsync(
                "test-request-id",
                TaskState.Cancelled,
                null,
                null,
                "User requested cancellation",
                It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion
    }
}