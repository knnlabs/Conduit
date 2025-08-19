using System;
using ConduitLLM.Core.Services;
using Xunit;

namespace ConduitLLM.Tests.Core.Services
{
    public partial class VideoGenerationOrchestratorTests
    {
        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullClientFactory_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new VideoGenerationOrchestrator(
                null,
                _mockTaskService.Object,
                _mockStorageService.Object,
                _mockPublishEndpoint.Object,
                _mockModelMappingService.Object,
                _mockVirtualKeyService.Object,
                _mockCostService.Object,
                _mockTaskRegistry.Object,
                _mockWebhookService.Object,
                _mockRetryOptions.Object,
                _mockHttpClientFactory.Object,
                _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullTaskService_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new VideoGenerationOrchestrator(
                _mockClientFactory.Object,
                null,
                _mockStorageService.Object,
                _mockPublishEndpoint.Object,
                _mockModelMappingService.Object,
                _mockVirtualKeyService.Object,
                _mockCostService.Object,
                _mockTaskRegistry.Object,
                _mockWebhookService.Object,
                _mockRetryOptions.Object,
                _mockHttpClientFactory.Object,
                _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullRetryOptions_ShouldUseDefaultConfiguration()
        {
            // Act
            var orchestrator = new VideoGenerationOrchestrator(
                _mockClientFactory.Object,
                _mockTaskService.Object,
                _mockStorageService.Object,
                _mockPublishEndpoint.Object,
                _mockModelMappingService.Object,
                _mockVirtualKeyService.Object,
                _mockCostService.Object,
                _mockTaskRegistry.Object,
                _mockWebhookService.Object,
                null,
                _mockHttpClientFactory.Object,
                _mockLogger.Object);

            // Assert - Should not throw and use default configuration
            Assert.NotNull(orchestrator);
        }

        #endregion
    }
}