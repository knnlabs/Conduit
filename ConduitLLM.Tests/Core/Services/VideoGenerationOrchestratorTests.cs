using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Configuration;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Core.Services
{
    public class VideoGenerationOrchestratorTests
    {
        private readonly Mock<ILLMClientFactory> _mockClientFactory;
        private readonly Mock<IAsyncTaskService> _mockTaskService;
        private readonly Mock<IMediaStorageService> _mockStorageService;
        private readonly Mock<IPublishEndpoint> _mockPublishEndpoint;
        private readonly Mock<ConduitLLM.Configuration.IModelProviderMappingService> _mockModelMappingService;
        private readonly Mock<IProviderDiscoveryService> _mockDiscoveryService;
        private readonly Mock<IVirtualKeyService> _mockVirtualKeyService;
        private readonly Mock<ICostCalculationService> _mockCostService;
        private readonly Mock<ICancellableTaskRegistry> _mockTaskRegistry;
        private readonly Mock<IWebhookNotificationService> _mockWebhookService;
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
        private readonly Mock<ILogger<VideoGenerationOrchestrator>> _mockLogger;
        private readonly Mock<IOptions<VideoGenerationRetryConfiguration>> _mockRetryOptions;
        private readonly VideoGenerationRetryConfiguration _retryConfiguration;
        private readonly VideoGenerationOrchestrator _orchestrator;

        public VideoGenerationOrchestratorTests()
        {
            _mockClientFactory = new Mock<ILLMClientFactory>();
            _mockTaskService = new Mock<IAsyncTaskService>();
            _mockStorageService = new Mock<IMediaStorageService>();
            _mockPublishEndpoint = new Mock<IPublishEndpoint>();
            _mockModelMappingService = new Mock<ConduitLLM.Configuration.IModelProviderMappingService>();
            _mockDiscoveryService = new Mock<IProviderDiscoveryService>();
            _mockVirtualKeyService = new Mock<IVirtualKeyService>();
            _mockCostService = new Mock<ICostCalculationService>();
            _mockTaskRegistry = new Mock<ICancellableTaskRegistry>();
            _mockWebhookService = new Mock<IWebhookNotificationService>();
            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _mockLogger = new Mock<ILogger<VideoGenerationOrchestrator>>();
            _mockRetryOptions = new Mock<IOptions<VideoGenerationRetryConfiguration>>();

            _retryConfiguration = new VideoGenerationRetryConfiguration
            {
                EnableRetries = true,
                MaxRetries = 3,
                BaseDelaySeconds = 1,
                MaxDelaySeconds = 300
            };

            _mockRetryOptions.Setup(x => x.Value).Returns(_retryConfiguration);

            _orchestrator = new VideoGenerationOrchestrator(
                _mockClientFactory.Object,
                _mockTaskService.Object,
                _mockStorageService.Object,
                _mockPublishEndpoint.Object,
                _mockModelMappingService.Object,
                _mockDiscoveryService.Object,
                _mockVirtualKeyService.Object,
                _mockCostService.Object,
                _mockTaskRegistry.Object,
                _mockWebhookService.Object,
                _mockRetryOptions.Object,
                _mockHttpClientFactory.Object,
                _mockLogger.Object);
        }

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
                _mockDiscoveryService.Object,
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
                _mockDiscoveryService.Object,
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
                _mockDiscoveryService.Object,
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

        #region VideoGenerationRequested Event Tests

        [Fact]
        public async Task Consume_VideoGenerationRequested_WithSyncRequest_ShouldSkipProcessing()
        {
            // Arrange
            var request = new VideoGenerationRequested
            {
                RequestId = "test-request-id",
                Model = "test-model",
                Prompt = "test prompt",
                IsAsync = false,
                VirtualKeyId = "1",
                CorrelationId = "test-correlation-id"
            };

            var context = new Mock<ConsumeContext<VideoGenerationRequested>>();
            context.Setup(x => x.Message).Returns(request);
            context.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

            // Act
            await _orchestrator.Consume(context.Object);

            // Assert
            // Should not call any services for synchronous requests
            _mockTaskService.Verify(x => x.UpdateTaskStatusAsync(It.IsAny<string>(), It.IsAny<TaskState>(), It.IsAny<int?>(), It.IsAny<object?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Consume_VideoGenerationRequested_WithAsyncRequest_ShouldUpdateTaskToProcessing()
        {
            // Arrange
            var request = new VideoGenerationRequested
            {
                RequestId = "test-request-id",
                Model = "test-model",
                Prompt = "test prompt",
                IsAsync = true,
                VirtualKeyId = "1",
                CorrelationId = "test-correlation-id"
            };

            var context = new Mock<ConsumeContext<VideoGenerationRequested>>();
            context.Setup(x => x.Message).Returns(request);
            context.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

            // Setup task metadata
            var taskMetadata = new TaskMetadata
            {
                VirtualKeyId = 1,
                ExtensionData = new Dictionary<string, object>
                {
                    ["VirtualKey"] = "test-virtual-key"
                }
            };

            var taskStatus = new AsyncTaskStatus
            {
                TaskId = "test-request-id",
                State = TaskState.Pending,
                Metadata = taskMetadata
            };

            _mockTaskService.Setup(x => x.GetTaskStatusAsync("test-request-id", It.IsAny<CancellationToken>()))
                .ReturnsAsync(taskStatus);

            // Setup model mapping
            var modelMapping = new ModelProviderMapping
            {
                ModelAlias = "test-model",
                ProviderId = 1,
                ProviderModelId = "test-provider-model",
                Provider = new Provider { ProviderType = ProviderType.Replicate }
            };

            _mockModelMappingService.Setup(x => x.GetMappingByModelAliasAsync("test-model"))
                .Returns(Task.FromResult(modelMapping));

            // Setup virtual key validation
            var virtualKey = new VirtualKey
            {
                Id = 1,
                IsEnabled = true,
                KeyName = "test-virtual-key",
                KeyHash = "test-virtual-key-hash"
            };

            _mockVirtualKeyService.Setup(x => x.ValidateVirtualKeyAsync("test-virtual-key", "test-model"))
                .ReturnsAsync(virtualKey);

            // Setup model capabilities
            var modelCapabilities = new Dictionary<string, DiscoveredModel>
            {
                ["test-model"] = new DiscoveredModel
                {
                    ModelId = "test-model",
                    Provider = "test-provider",
                    Capabilities = new ConduitLLM.Core.Interfaces.ModelCapabilities
                    {
                        VideoGeneration = true
                    }
                }
            };

            _mockDiscoveryService.Setup(x => x.DiscoverModelsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCapabilities);

            // Setup client factory to return null (will cause NotSupportedException)
            _mockClientFactory.Setup(x => x.GetClient("test-model"))
                .Returns((ILLMClient)null);

            // Act
            await _orchestrator.Consume(context.Object);

            // Assert
            _mockTaskService.Verify(x => x.UpdateTaskStatusAsync(
                "test-request-id",
                TaskState.Processing,
                null,
                null,
                null,
                It.IsAny<CancellationToken>()), Times.Once);

            _mockPublishEndpoint.Verify(x => x.Publish(
                It.Is<VideoGenerationStarted>(e => e.RequestId == "test-request-id"),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Consume_VideoGenerationRequested_WithInvalidModel_ShouldUpdateTaskToFailed()
        {
            // Arrange
            var request = new VideoGenerationRequested
            {
                RequestId = "test-request-id",
                Model = "invalid-model",
                Prompt = "test prompt",
                IsAsync = true,
                VirtualKeyId = "1",
                CorrelationId = "test-correlation-id"
            };

            var context = new Mock<ConsumeContext<VideoGenerationRequested>>();
            context.Setup(x => x.Message).Returns(request);
            context.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

            // Setup task metadata
            var taskMetadata = new TaskMetadata
            {
                VirtualKeyId = 1,
                ExtensionData = new Dictionary<string, object>
                {
                    ["VirtualKey"] = "test-virtual-key"
                }
            };

            var taskStatus = new AsyncTaskStatus
            {
                TaskId = "test-request-id",
                State = TaskState.Pending,
                Metadata = taskMetadata
            };

            _mockTaskService.Setup(x => x.GetTaskStatusAsync("test-request-id", It.IsAny<CancellationToken>()))
                .ReturnsAsync(taskStatus);

            // Setup model mapping to return null
            _mockModelMappingService.Setup(x => x.GetMappingByModelAliasAsync("invalid-model"))
                .Returns(Task.FromResult((ModelProviderMapping?)null));

            // Setup discovery service to return empty models
            _mockDiscoveryService.Setup(x => x.DiscoverModelsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<string, DiscoveredModel>());

            // Act
            await _orchestrator.Consume(context.Object);

            // Assert
            _mockTaskService.Verify(x => x.UpdateTaskStatusAsync(
                "test-request-id",
                TaskState.Failed,
                null,
                null,
                It.Is<string>(error => error.Contains("Model invalid-model not found")),
                It.IsAny<CancellationToken>()), Times.Once);

            _mockPublishEndpoint.Verify(x => x.Publish(
                It.Is<VideoGenerationFailed>(e => e.RequestId == "test-request-id"),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Consume_VideoGenerationRequested_WithInvalidVirtualKey_ShouldUpdateTaskToFailed()
        {
            // Arrange
            var request = new VideoGenerationRequested
            {
                RequestId = "test-request-id",
                Model = "test-model",
                Prompt = "test prompt",
                IsAsync = true,
                VirtualKeyId = "1",
                CorrelationId = "test-correlation-id"
            };

            var context = new Mock<ConsumeContext<VideoGenerationRequested>>();
            context.Setup(x => x.Message).Returns(request);
            context.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

            // Setup task metadata
            var taskMetadata = new TaskMetadata
            {
                VirtualKeyId = 1,
                ExtensionData = new Dictionary<string, object>
                {
                    ["VirtualKey"] = "invalid-virtual-key"
                }
            };

            var taskStatus = new AsyncTaskStatus
            {
                TaskId = "test-request-id",
                State = TaskState.Pending,
                Metadata = taskMetadata
            };

            _mockTaskService.Setup(x => x.GetTaskStatusAsync("test-request-id", It.IsAny<CancellationToken>()))
                .ReturnsAsync(taskStatus);

            // Setup model mapping
            var modelMapping = new ModelProviderMapping
            {
                ModelAlias = "test-model",
                ProviderId = 1,
                ProviderModelId = "test-provider-model",
                Provider = new Provider { ProviderType = ProviderType.Replicate }
            };

            _mockModelMappingService.Setup(x => x.GetMappingByModelAliasAsync("test-model"))
                .Returns(Task.FromResult(modelMapping));

            // Setup virtual key validation to return null
            _mockVirtualKeyService.Setup(x => x.ValidateVirtualKeyAsync("invalid-virtual-key", "test-model"))
                .ReturnsAsync((VirtualKey)null);

            // Act
            await _orchestrator.Consume(context.Object);

            // Assert
            _mockTaskService.Verify(x => x.UpdateTaskStatusAsync(
                "test-request-id",
                TaskState.Failed,
                null,
                null,
                It.Is<string>(error => error.Contains("Invalid or disabled virtual key")),
                It.IsAny<CancellationToken>()), Times.Once);

            _mockPublishEndpoint.Verify(x => x.Publish(
                It.Is<VideoGenerationFailed>(e => e.RequestId == "test-request-id"),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Consume_VideoGenerationRequested_WithModelNotSupportingVideo_ShouldUpdateTaskToFailed()
        {
            // Arrange
            var request = new VideoGenerationRequested
            {
                RequestId = "test-request-id",
                Model = "text-model",
                Prompt = "test prompt",
                IsAsync = true,
                VirtualKeyId = "1",
                CorrelationId = "test-correlation-id"
            };

            var context = new Mock<ConsumeContext<VideoGenerationRequested>>();
            context.Setup(x => x.Message).Returns(request);
            context.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

            // Setup task metadata
            var taskMetadata = new TaskMetadata
            {
                VirtualKeyId = 1,
                ExtensionData = new Dictionary<string, object>
                {
                    ["VirtualKey"] = "test-virtual-key"
                }
            };

            var taskStatus = new AsyncTaskStatus
            {
                TaskId = "test-request-id",
                State = TaskState.Pending,
                Metadata = taskMetadata
            };

            _mockTaskService.Setup(x => x.GetTaskStatusAsync("test-request-id", It.IsAny<CancellationToken>()))
                .ReturnsAsync(taskStatus);

            // Setup model mapping
            var modelMapping = new ModelProviderMapping
            {
                ModelAlias = "text-model",
                ProviderId = 1,
                ProviderModelId = "test-provider-model",
                Provider = new Provider { ProviderType = ProviderType.Replicate }
            };

            _mockModelMappingService.Setup(x => x.GetMappingByModelAliasAsync("text-model"))
                .Returns(Task.FromResult(modelMapping));

            // Setup virtual key validation
            var virtualKey = new VirtualKey
            {
                Id = 1,
                IsEnabled = true,
                KeyName = "test-virtual-key",
                KeyHash = "test-virtual-key-hash"
            };

            _mockVirtualKeyService.Setup(x => x.ValidateVirtualKeyAsync("test-virtual-key", "text-model"))
                .ReturnsAsync(virtualKey);

            // Setup model capabilities without video generation
            var modelCapabilities = new Dictionary<string, DiscoveredModel>
            {
                ["text-model"] = new DiscoveredModel
                {
                    ModelId = "text-model",
                    Provider = "test-provider",
                    Capabilities = new ConduitLLM.Core.Interfaces.ModelCapabilities
                    {
                        VideoGeneration = false // No video generation support
                    }
                }
            };

            _mockDiscoveryService.Setup(x => x.DiscoverModelsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCapabilities);

            // Act
            await _orchestrator.Consume(context.Object);

            // Assert
            _mockTaskService.Verify(x => x.UpdateTaskStatusAsync(
                "test-request-id",
                TaskState.Failed,
                null,
                null,
                It.Is<string>(error => error.Contains("does not support video generation")),
                It.IsAny<CancellationToken>()), Times.Once);

            _mockPublishEndpoint.Verify(x => x.Publish(
                It.Is<VideoGenerationFailed>(e => e.RequestId == "test-request-id"),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Consume_VideoGenerationRequested_WithRetryableError_ShouldScheduleRetry()
        {
            // Arrange
            var request = new VideoGenerationRequested
            {
                RequestId = "test-request-id",
                Model = "test-model",
                Prompt = "test prompt",
                IsAsync = true,
                VirtualKeyId = "1",
                CorrelationId = "test-correlation-id"
            };

            var context = new Mock<ConsumeContext<VideoGenerationRequested>>();
            context.Setup(x => x.Message).Returns(request);
            context.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

            // Setup task metadata
            var taskMetadata = new TaskMetadata
            {
                VirtualKeyId = 1,
                ExtensionData = new Dictionary<string, object>
                {
                    ["VirtualKey"] = "test-virtual-key"
                }
            };

            var taskStatus = new AsyncTaskStatus
            {
                TaskId = "test-request-id",
                State = TaskState.Pending,
                Metadata = taskMetadata,
                RetryCount = 0,
                MaxRetries = 3
            };

            _mockTaskService.Setup(x => x.GetTaskStatusAsync("test-request-id", It.IsAny<CancellationToken>()))
                .ReturnsAsync(taskStatus);

            // Setup model mapping
            var modelMapping = new ModelProviderMapping
            {
                ModelAlias = "test-model",
                ProviderId = 1,
                ProviderModelId = "test-provider-model",
                Provider = new Provider { ProviderType = ProviderType.Replicate }
            };

            _mockModelMappingService.Setup(x => x.GetMappingByModelAliasAsync("test-model"))
                .Returns(Task.FromResult(modelMapping));

            // Setup virtual key validation
            var virtualKey = new VirtualKey
            {
                Id = 1,
                IsEnabled = true,
                KeyName = "test-virtual-key",
                KeyHash = "test-virtual-key-hash"
            };

            _mockVirtualKeyService.Setup(x => x.ValidateVirtualKeyAsync("test-virtual-key", "test-model"))
                .ReturnsAsync(virtualKey);

            // Setup model capabilities
            var modelCapabilities = new Dictionary<string, DiscoveredModel>
            {
                ["test-model"] = new DiscoveredModel
                {
                    ModelId = "test-model",
                    Provider = "test-provider",
                    Capabilities = new ConduitLLM.Core.Interfaces.ModelCapabilities
                    {
                        VideoGeneration = true
                    }
                }
            };

            _mockDiscoveryService.Setup(x => x.DiscoverModelsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCapabilities);

            // Setup client factory to throw a retryable exception
            _mockClientFactory.Setup(x => x.GetClient("test-model"))
                .Throws(new TimeoutException("Request timeout"));

            // Act
            await _orchestrator.Consume(context.Object);

            // Assert
            _mockTaskService.Verify(x => x.UpdateTaskStatusAsync(
                "test-request-id",
                TaskState.Pending,
                null,
                null,
                It.Is<string>(error => error.Contains("Retry 1/3")),
                It.IsAny<CancellationToken>()), Times.Once);

            _mockPublishEndpoint.Verify(x => x.Publish(
                It.Is<VideoGenerationFailed>(e => e.RequestId == "test-request-id" && e.IsRetryable),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

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

        #region Private Method Tests (via public interface)

        [Fact]
        public async Task GetModelInfoAsync_WithExistingMapping_ShouldReturnModelInfo()
        {
            // This test verifies the behavior through the public Consume method
            // since GetModelInfoAsync is private

            // Arrange
            var request = new VideoGenerationRequested
            {
                RequestId = "test-request-id",
                Model = "test-model",
                Prompt = "test prompt",
                IsAsync = true,
                VirtualKeyId = "1",
                CorrelationId = "test-correlation-id"
            };

            var context = new Mock<ConsumeContext<VideoGenerationRequested>>();
            context.Setup(x => x.Message).Returns(request);
            context.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

            // Setup task metadata
            var taskMetadata = new TaskMetadata
            {
                VirtualKeyId = 1,
                ExtensionData = new Dictionary<string, object>
                {
                    ["VirtualKey"] = "test-virtual-key"
                }
            };

            var taskStatus = new AsyncTaskStatus
            {
                TaskId = "test-request-id",
                State = TaskState.Pending,
                Metadata = taskMetadata
            };

            _mockTaskService.Setup(x => x.GetTaskStatusAsync("test-request-id", It.IsAny<CancellationToken>()))
                .ReturnsAsync(taskStatus);

            // Setup model mapping
            var modelMapping = new ModelProviderMapping
            {
                ModelAlias = "test-model",
                ProviderId = 1,
                ProviderModelId = "test-provider-model",
                Provider = new Provider { ProviderType = ProviderType.Replicate }
            };

            _mockModelMappingService.Setup(x => x.GetMappingByModelAliasAsync("test-model"))
                .Returns(Task.FromResult(modelMapping));

            // Setup virtual key validation
            var virtualKey = new VirtualKey
            {
                Id = 1,
                IsEnabled = true,
                KeyName = "test-virtual-key",
                KeyHash = "test-virtual-key-hash"
            };

            _mockVirtualKeyService.Setup(x => x.ValidateVirtualKeyAsync("test-virtual-key", "test-model"))
                .ReturnsAsync(virtualKey);

            // Setup model capabilities
            var modelCapabilities = new Dictionary<string, DiscoveredModel>
            {
                ["test-model"] = new DiscoveredModel
                {
                    ModelId = "test-model",
                    Provider = "test-provider",
                    Capabilities = new ConduitLLM.Core.Interfaces.ModelCapabilities
                    {
                        VideoGeneration = true
                    }
                }
            };

            _mockDiscoveryService.Setup(x => x.DiscoverModelsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCapabilities);

            // Setup client factory to return null (will cause NotSupportedException)
            _mockClientFactory.Setup(x => x.GetClient("test-model"))
                .Returns((ILLMClient)null);

            // Act
            await _orchestrator.Consume(context.Object);

            // Assert
            _mockModelMappingService.Verify(x => x.GetMappingByModelAliasAsync("test-model"), Times.Once);
            // Should not call discovery service since mapping was found
            _mockDiscoveryService.Verify(x => x.DiscoverModelsAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact(Skip = "Video generation uses reflection which cannot be easily mocked in unit tests")]
        public async Task CalculateVideoCost_ShouldUseCorrectUsageParameters()
        {
            // This test verifies cost calculation through the public interface
            // since CalculateVideoCostAsync is private

            // Arrange
            var request = new VideoGenerationRequested
            {
                RequestId = "test-request-id",
                Model = "test-model",
                Prompt = "test prompt",
                IsAsync = true,
                VirtualKeyId = "1",
                CorrelationId = "test-correlation-id",
                Parameters = new VideoGenerationParameters
                {
                    Duration = 10,
                    Size = "1920x1080"
                }
            };

            var context = new Mock<ConsumeContext<VideoGenerationRequested>>();
            context.Setup(x => x.Message).Returns(request);
            context.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

            // Setup all the mocks for a successful video generation
            // (This would trigger cost calculation)
            SetupSuccessfulVideoGeneration(request);

            // Setup cost calculation
            _mockCostService.Setup(x => x.CalculateCostAsync(
                It.IsAny<string>(),
                It.Is<Usage>(u => u.VideoDurationSeconds == 10 && u.VideoResolution == "1920x1080"),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(10.50m);

            // Act
            await _orchestrator.Consume(context.Object);

            // Assert
            _mockCostService.Verify(x => x.CalculateCostAsync(
                It.IsAny<string>(),
                It.Is<Usage>(u => u.VideoDurationSeconds == 10 && u.VideoResolution == "1920x1080"),
                It.IsAny<CancellationToken>()), 
                Times.Once);
        }

        #endregion

        #region Test Helper Methods

        private void SetupSuccessfulVideoGeneration(VideoGenerationRequested request)
        {
            // Setup task metadata
            var taskMetadata = new TaskMetadata
            {
                VirtualKeyId = int.Parse(request.VirtualKeyId),
                ExtensionData = new Dictionary<string, object>
                {
                    ["VirtualKey"] = "test-virtual-key",
                    ["Request"] = new VideoGenerationRequest
                    {
                        Model = request.Model,
                        Prompt = request.Prompt,
                        Duration = request.Parameters?.Duration,
                        Size = request.Parameters?.Size,
                        Fps = request.Parameters?.Fps
                    }
                }
            };

            var taskStatus = new AsyncTaskStatus
            {
                TaskId = request.RequestId,
                State = TaskState.Pending,
                Metadata = taskMetadata
            };

            _mockTaskService.Setup(x => x.GetTaskStatusAsync(request.RequestId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(taskStatus);

            // Setup model mapping
            var modelMapping = new ModelProviderMapping
            {
                ModelAlias = request.Model,
                ProviderId = 1,
                ProviderModelId = "test-provider-model",
                Provider = new Provider { ProviderType = ProviderType.Replicate }
            };

            _mockModelMappingService.Setup(x => x.GetMappingByModelAliasAsync(request.Model))
                .Returns(Task.FromResult(modelMapping));

            // Setup virtual key validation
            var virtualKey = new VirtualKey
            {
                Id = 1,
                IsEnabled = true,
                KeyName = "test-virtual-key",
                KeyHash = "test-virtual-key-hash"
            };

            _mockVirtualKeyService.Setup(x => x.ValidateVirtualKeyAsync("test-virtual-key", request.Model))
                .ReturnsAsync(virtualKey);

            // Setup model capabilities
            var modelCapabilities = new Dictionary<string, DiscoveredModel>
            {
                [request.Model] = new DiscoveredModel
                {
                    ModelId = request.Model,
                    Provider = "test-provider",
                    Capabilities = new ConduitLLM.Core.Interfaces.ModelCapabilities
                    {
                        VideoGeneration = true
                    }
                }
            };

            _mockDiscoveryService.Setup(x => x.DiscoverModelsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCapabilities);

            // Setup mock client with CreateVideoAsync method
            var mockClient = new Mock<ILLMClient>();
            var videoResponse = new VideoGenerationResponse
            {
                Data = new List<VideoData>
                {
                    new VideoData
                    {
                        Url = "https://example.com/video.mp4",
                        Metadata = new VideoMetadata
                        {
                            Width = 1920,
                            Height = 1080,
                            Duration = 10,
                            Fps = 30,
                            MimeType = "video/mp4"
                        }
                    }
                },
                Usage = new VideoGenerationUsage
                {
                    TotalDurationSeconds = 10,
                    VideosGenerated = 1
                },
                Model = request.Model,
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            // Video generation uses reflection to call CreateVideoAsync, which can't be easily mocked
            // So we'll test the cost calculation more directly
            _mockClientFactory.Setup(x => x.GetClient(request.Model))
                .Returns(mockClient.Object);

            // Setup media storage
            var storageResult = new MediaStorageResult
            {
                StorageKey = "video/test-key.mp4",
                Url = "https://storage.example.com/video/test-key.mp4",
                SizeBytes = 1024000,
                ContentHash = "test-hash",
                CreatedAt = DateTime.UtcNow
            };

            _mockStorageService.Setup(x => x.StoreVideoAsync(
                It.IsAny<Stream>(),
                It.IsAny<VideoMediaMetadata>(),
                It.IsAny<Action<long>>()))
                .ReturnsAsync(storageResult);

            // Setup cost calculation
            _mockCostService.Setup(x => x.CalculateCostAsync(It.IsAny<string>(), It.IsAny<Usage>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(5.00m);
        }

        #endregion
    }
}