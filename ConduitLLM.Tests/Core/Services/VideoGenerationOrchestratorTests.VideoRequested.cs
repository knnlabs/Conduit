using System;
using System.Collections.Generic;
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
    public partial class VideoGenerationOrchestratorTests
    {
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

            // Setup capability service to indicate model supports video generation
            _mockCapabilityService.Setup(x => x.SupportsVideoGenerationAsync("test-model"))
                .ReturnsAsync(true);

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
                It.Is<string>(error => error.Contains("Retry 1/3 scheduled: Request timeout")),
                It.IsAny<CancellationToken>()), Times.Once);

            _mockPublishEndpoint.Verify(x => x.Publish(
                It.Is<VideoGenerationFailed>(e => e.RequestId == "test-request-id" && e.IsRetryable),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion
    }
}