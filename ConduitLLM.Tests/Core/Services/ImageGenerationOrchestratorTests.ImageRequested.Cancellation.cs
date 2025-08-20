using System;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using MassTransit;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Core.Services
{
    public partial class ImageGenerationOrchestratorTests
    {
        #region ImageGenerationRequested Cancellation Tests

        [Fact]
        public async Task Consume_ImageGenerationRequested_WithCancellation_ShouldUpdateTaskToCancelled()
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
                    Model = "dall-e-3",
                    N = 1
                },
                CorrelationId = "test-correlation-id"
            };

            var context = new Mock<ConsumeContext<ImageGenerationRequested>>();
            var cts = new CancellationTokenSource();
            context.Setup(x => x.Message).Returns(request);
            context.Setup(x => x.CancellationToken).Returns(cts.Token);

            // Setup virtual key
            var virtualKey = new VirtualKey
            {
                Id = 1,
                IsEnabled = true,
                KeyName = "test-virtual-key",
                KeyHash = "test-virtual-key-hash"
            };

            _mockVirtualKeyService.Setup(x => x.ValidateVirtualKeyAsync("test-virtual-key-hash", "dall-e-3"))
                .ReturnsAsync(virtualKey);

            // Setup model with image generation capabilities
            var modelEntity = new Model
            {
                Id = 1,
                Name = "dall-e-3",
                ModelSeriesId = 1,
                ModelCapabilitiesId = 1,
                Capabilities = new ConduitLLM.Configuration.Entities.ModelCapabilities
                {
                    Id = 1,
                    SupportsImageGeneration = true,
                    MaxTokens = 4000,
                    TokenizerType = TokenizerType.Cl100KBase
                }
            };

            // Setup model mapping
            var modelMapping = new ModelProviderMapping
            {
                ModelAlias = "dall-e-3",
                ModelId = 1,
                Model = modelEntity,
                ProviderId = 1,
                ProviderModelId = "dall-e-3",
                Provider = new Provider { ProviderType = ProviderType.OpenAI }
            };

            _mockModelMappingService.Setup(x => x.GetMappingByModelAliasAsync("dall-e-3"))
                .Returns(Task.FromResult(modelMapping));

            // Setup provider service to return the provider
            var providerEntity = new Provider 
            { 
                Id = 1,
                ProviderType = ProviderType.OpenAI
            };

            _mockProviderService.Setup(x => x.GetProviderByIdAsync(1))
                .ReturnsAsync(providerEntity);

            // Setup client to throw cancellation exception
            var mockClient = new Mock<ILLMClient>();
            mockClient.Setup(x => x.CreateImageAsync(
                It.IsAny<ConduitLLM.Core.Models.ImageGenerationRequest>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
                .ThrowsAsync(new OperationCanceledException());

            _mockClientFactory.Setup(x => x.GetClient("dall-e-3"))
                .Returns(mockClient.Object);

            // Cancel the token after setup
            cts.Cancel();

            // Act
            await _orchestrator.Consume(context.Object);

            // Assert
            _mockTaskService.Verify(x => x.UpdateTaskStatusAsync(
                "test-task-id",
                TaskState.Cancelled,
                null,
                null,
                "Task was cancelled by user request",
                It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion
    }
}