using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;
using MassTransit;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Core.Services
{
    public partial class ImageGenerationOrchestratorTests
    {
        #region ImageGenerationCancelled Event Tests

        [Fact]
        public async Task Consume_ImageGenerationCancelled_WithExistingTask_ShouldCancelTask()
        {
            // Arrange
            var cancellation = new ImageGenerationCancelled
            {
                TaskId = "test-task-id",
                Reason = "User requested cancellation"
            };

            var context = new Mock<ConsumeContext<ImageGenerationCancelled>>();
            context.Setup(x => x.Message).Returns(cancellation);

            _mockTaskRegistry.Setup(x => x.TryCancel("test-task-id"))
                .Returns(true);

            // Act
            await _orchestrator.Consume(context.Object);

            // Assert
            _mockTaskRegistry.Verify(x => x.TryCancel("test-task-id"), Times.Once);
            _mockTaskService.Verify(x => x.UpdateTaskStatusAsync(
                "test-task-id",
                TaskState.Cancelled,
                null,
                null,
                "User requested cancellation",
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Consume_ImageGenerationCancelled_WithNonExistentTask_ShouldUpdateTaskStatus()
        {
            // Arrange
            var cancellation = new ImageGenerationCancelled
            {
                TaskId = "non-existent-task-id",
                Reason = "User requested cancellation"
            };

            var context = new Mock<ConsumeContext<ImageGenerationCancelled>>();
            context.Setup(x => x.Message).Returns(cancellation);

            _mockTaskRegistry.Setup(x => x.TryCancel("non-existent-task-id"))
                .Returns(false);

            // Act
            await _orchestrator.Consume(context.Object);

            // Assert
            _mockTaskRegistry.Verify(x => x.TryCancel("non-existent-task-id"), Times.Once);
            _mockTaskService.Verify(x => x.UpdateTaskStatusAsync(
                "non-existent-task-id",
                TaskState.Cancelled,
                null,
                null,
                "User requested cancellation",
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Consume_ImageGenerationCancelled_WithNullReason_ShouldUseDefaultReason()
        {
            // Arrange
            var cancellation = new ImageGenerationCancelled
            {
                TaskId = "test-task-id",
                Reason = null
            };

            var context = new Mock<ConsumeContext<ImageGenerationCancelled>>();
            context.Setup(x => x.Message).Returns(cancellation);

            _mockTaskRegistry.Setup(x => x.TryCancel("test-task-id"))
                .Returns(true);

            // Act
            await _orchestrator.Consume(context.Object);

            // Assert
            _mockTaskService.Verify(x => x.UpdateTaskStatusAsync(
                "test-task-id",
                TaskState.Cancelled,
                null,
                null,
                "Cancelled by user request",
                It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion
    }
}