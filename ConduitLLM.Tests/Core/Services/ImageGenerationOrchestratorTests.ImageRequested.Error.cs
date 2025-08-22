using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Configuration.Entities;
using MassTransit;
using Moq;

namespace ConduitLLM.Tests.Core.Services
{
    public partial class ImageGenerationOrchestratorTests
    {
        #region ImageGenerationRequested Error Tests

        [Fact]
        public async Task Consume_ImageGenerationRequested_WithInvalidModel_ShouldUpdateTaskToFailed()
        {
            // Arrange
            var request = new ImageGenerationRequested
            {
                TaskId = "test-task-id",
                VirtualKeyId = 1,
                VirtualKeyHash = "test-virtual-key-hash",
                Request = new ConduitLLM.Core.Events.ImageGenerationRequest
                {
                    Prompt = "A beautiful landscape",
                    Model = "invalid-model",
                    N = 1
                },
                CorrelationId = "test-correlation-id"
            };

            var context = new Mock<ConsumeContext<ImageGenerationRequested>>();
            context.Setup(x => x.Message).Returns(request);
            context.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

            // Setup virtual key
            var virtualKey = new VirtualKey
            {
                Id = 1,
                IsEnabled = true,
                KeyName = "test-virtual-key",
                KeyHash = "test-virtual-key-hash"
            };

            _mockVirtualKeyService.Setup(x => x.ValidateVirtualKeyAsync("test-virtual-key-hash", "invalid-model"))
                .ReturnsAsync(virtualKey);

            // Setup model mapping to return null (invalid model)
            _mockModelMappingService.Setup(x => x.GetMappingByModelAliasAsync("invalid-model"))
                .Returns(Task.FromResult((ModelProviderMapping?)null));

            // Act
            await Assert.ThrowsAsync<InvalidOperationException>(() => 
                _orchestrator.Consume(context.Object));

            // Assert
            _mockTaskService.Verify(x => x.UpdateTaskStatusAsync(
                "test-task-id",
                TaskState.Failed,
                null,
                null,
                It.Is<string>(error => error.Contains("Model invalid-model not found")),
                It.IsAny<CancellationToken>()), Times.Once);

            _mockPublishEndpoint.Verify(x => x.Publish(
                It.Is<ImageGenerationFailed>(e => e.TaskId == "test-task-id"),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Consume_ImageGenerationRequested_WithInvalidVirtualKey_ShouldUpdateTaskToFailed()
        {
            // Arrange
            var request = new ImageGenerationRequested
            {
                TaskId = "test-task-id",
                VirtualKeyId = 1,
                VirtualKeyHash = "invalid-virtual-key-hash",
                Request = new ConduitLLM.Core.Events.ImageGenerationRequest
                {
                    Prompt = "A beautiful landscape",
                    Model = "dall-e-3",
                    N = 1
                },
                CorrelationId = "test-correlation-id"
            };

            var context = new Mock<ConsumeContext<ImageGenerationRequested>>();
            context.Setup(x => x.Message).Returns(request);
            context.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

            // Setup virtual key validation to return null (invalid key)
            _mockVirtualKeyService.Setup(x => x.ValidateVirtualKeyAsync("invalid-virtual-key-hash", "dall-e-3"))
                .ReturnsAsync((VirtualKey)null);

            // Act
            await Assert.ThrowsAsync<InvalidOperationException>(() => 
                _orchestrator.Consume(context.Object));

            // Assert
            _mockTaskService.Verify(x => x.UpdateTaskStatusAsync(
                "test-task-id",
                TaskState.Failed,
                null,
                null,
                It.Is<string>(error => error.Contains("Model dall-e-3 not found")),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Consume_ImageGenerationRequested_WithModelNotSupportingImageGeneration_ShouldUpdateTaskToFailed()
        {
            // Arrange
            var request = new ImageGenerationRequested
            {
                TaskId = "test-task-id",
                VirtualKeyId = 1,
                VirtualKeyHash = "test-virtual-key-hash",
                Request = new ConduitLLM.Core.Events.ImageGenerationRequest
                {
                    Prompt = "A beautiful landscape",
                    Model = "gpt-4",
                    N = 1
                },
                CorrelationId = "test-correlation-id"
            };

            var context = new Mock<ConsumeContext<ImageGenerationRequested>>();
            context.Setup(x => x.Message).Returns(request);
            context.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

            // Setup virtual key
            var virtualKey = new VirtualKey
            {
                Id = 1,
                IsEnabled = true,
                KeyName = "test-virtual-key",
                KeyHash = "test-virtual-key-hash"
            };

            _mockVirtualKeyService.Setup(x => x.ValidateVirtualKeyAsync("test-virtual-key-hash", "gpt-4"))
                .ReturnsAsync(virtualKey);

            // Setup model mapping for a text model
            var modelMapping = new ModelProviderMapping
            {
                ModelAlias = "gpt-4",
                    ModelId = 1,
                ProviderId = 1,
                ProviderModelId = "gpt-4"
            };

            _mockModelMappingService.Setup(x => x.GetMappingByModelAliasAsync("gpt-4"))
                .Returns(Task.FromResult(modelMapping));

            // Act
            await Assert.ThrowsAsync<InvalidOperationException>(() => 
                _orchestrator.Consume(context.Object));

            // Assert
            _mockTaskService.Verify(x => x.UpdateTaskStatusAsync(
                "test-task-id",
                TaskState.Failed,
                null,
                null,
                It.Is<string>(error => error.Contains("Model gpt-4 not found")),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion
    }
}