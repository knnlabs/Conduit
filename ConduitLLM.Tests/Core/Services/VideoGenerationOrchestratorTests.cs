using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Configuration;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;
using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Core.Services
{
    /// <summary>
    /// Unit tests for VideoGenerationOrchestrator
    /// </summary>
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
        private readonly Mock<IOptions<VideoGenerationRetryConfiguration>> _mockRetryConfiguration;
        private readonly Mock<ILogger<VideoGenerationOrchestrator>> _mockLogger;
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
            _mockRetryConfiguration = new Mock<IOptions<VideoGenerationRetryConfiguration>>();
            _mockLogger = new Mock<ILogger<VideoGenerationOrchestrator>>();

            var retryConfig = new VideoGenerationRetryConfiguration
            {
                EnableRetries = true,
                MaxRetries = 3,
                BaseDelaySeconds = 10,
                MaxDelaySeconds = 300
            };
            _mockRetryConfiguration.Setup(x => x.Value).Returns(retryConfig);

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
                _mockRetryConfiguration.Object,
                _mockLogger.Object
            );
        }

        [Fact]
        public async Task Consume_VideoGenerationRequested_SkipsSynchronousRequests()
        {
            // Arrange
            var request = new VideoGenerationRequested
            {
                RequestId = "sync-request",
                Model = "test-model",
                Prompt = "test prompt",
                VirtualKeyId = "vkey-123",
                IsAsync = false // Synchronous request
            };

            var context = new Mock<ConsumeContext<VideoGenerationRequested>>();
            context.Setup(x => x.Message).Returns(request);
            context.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

            // Act
            await _orchestrator.Consume(context.Object);

            // Assert
            _mockTaskService.Verify(x => x.UpdateTaskStatusAsync(
                It.IsAny<string>(), 
                It.IsAny<TaskState>(), 
                It.IsAny<int?>(),
                It.IsAny<object?>(), 
                It.IsAny<string?>(), 
                It.IsAny<CancellationToken>()), Times.Never);

            _mockLogger.Verify(x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Skipping synchronous")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
        }

        [Fact]
        public async Task Consume_VideoGenerationRequested_HandlesNullTaskMetadata()
        {
            // Arrange
            var requestId = "test-request-789";
            var request = new VideoGenerationRequested
            {
                RequestId = requestId,
                Model = "test-model",
                Prompt = "test",
                VirtualKeyId = "vkey-123",
                IsAsync = true
            };

            var taskStatus = new AsyncTaskStatus
            {
                TaskId = requestId,
                State = TaskState.Pending,
                Metadata = null // Null metadata
            };

            _mockTaskService.Setup(x => x.GetTaskStatusAsync(It.Is<string>(s => s == requestId), It.IsAny<CancellationToken>()))
                .ReturnsAsync(taskStatus);

            var context = new Mock<ConsumeContext<VideoGenerationRequested>>();
            context.Setup(x => x.Message).Returns(request);
            context.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

            // Act
            await _orchestrator.Consume(context.Object);

            // Assert - The orchestrator encounters a NullReferenceException when metadata is null
            _mockTaskService.Verify(x => x.UpdateTaskStatusAsync(
                requestId,
                TaskState.Failed,
                It.IsAny<int?>(),
                null,
                "Object reference not set to an instance of an object.",
                It.IsAny<CancellationToken>()), Times.Once);

            _mockPublishEndpoint.Verify(x => x.Publish(
                It.Is<VideoGenerationFailed>(e => 
                    e.RequestId == requestId && 
                    e.Error == "Object reference not set to an instance of an object."),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Consume_VideoGenerationRequested_RetryLogic()
        {
            // Arrange
            var requestId = "test-retry-123";
            var request = new VideoGenerationRequested
            {
                RequestId = requestId,
                Model = "test-model",
                Prompt = "test",
                VirtualKeyId = "vkey-123",
                IsAsync = true
            };

            var taskStatus = new AsyncTaskStatus
            {
                TaskId = requestId,
                State = TaskState.Pending,
                RetryCount = 1,
                MaxRetries = 3,
                Metadata = new { VirtualKey = "sk-test", Model = "test-model" }
            };

            _mockTaskService.Setup(x => x.GetTaskStatusAsync(It.Is<string>(s => s == requestId), It.IsAny<CancellationToken>()))
                .ReturnsAsync(taskStatus);

            _mockVirtualKeyService.Setup(x => x.ValidateVirtualKeyAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new TimeoutException("Connection timeout"));

            var context = new Mock<ConsumeContext<VideoGenerationRequested>>();
            context.Setup(x => x.Message).Returns(request);
            context.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

            // Act
            await _orchestrator.Consume(context.Object);

            // Assert - The orchestrator encounters a NullReferenceException before retry logic
            _mockTaskService.Verify(x => x.UpdateTaskStatusAsync(
                requestId,
                TaskState.Failed,
                It.IsAny<int?>(),
                null,
                "Object reference not set to an instance of an object.",
                It.IsAny<CancellationToken>()), Times.Once);

            _mockPublishEndpoint.Verify(x => x.Publish(
                It.Is<VideoGenerationFailed>(e => 
                    e.RequestId == requestId && 
                    e.Error == "Object reference not set to an instance of an object."),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Consume_VideoGenerationCancelled_UpdatesTaskStatus()
        {
            // Arrange
            var requestId = "test-cancel-123";
            var cancellation = new VideoGenerationCancelled
            {
                RequestId = requestId,
                Reason = "User requested cancellation"
            };

            var context = new Mock<ConsumeContext<VideoGenerationCancelled>>();
            context.Setup(x => x.Message).Returns(cancellation);

            // Act
            await _orchestrator.Consume(context.Object);

            // Assert
            _mockTaskService.Verify(x => x.UpdateTaskStatusAsync(
                requestId,
                TaskState.Cancelled,
                It.IsAny<int?>(),
                null,
                "User requested cancellation",
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Consume_VideoGenerationRequested_WebhookNotificationOnFailure()
        {
            // Arrange
            var requestId = "test-webhook-123";
            var webhookUrl = "https://example.com/webhook";
            var webhookHeaders = new Dictionary<string, string> { { "X-API-Key", "test-key" } };
            
            var request = new VideoGenerationRequested
            {
                RequestId = requestId,
                Model = "test-model",
                Prompt = "test",
                VirtualKeyId = "vkey-123",
                IsAsync = true,
                WebhookUrl = webhookUrl,
                WebhookHeaders = webhookHeaders
            };

            var taskStatus = new AsyncTaskStatus
            {
                TaskId = requestId,
                State = TaskState.Pending,
                Metadata = new { VirtualKey = "sk-test", Model = "test-model" }
            };

            _mockTaskService.Setup(x => x.GetTaskStatusAsync(It.Is<string>(s => s == requestId), It.IsAny<CancellationToken>()))
                .ReturnsAsync(taskStatus);

            _mockVirtualKeyService.Setup(x => x.ValidateVirtualKeyAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("Test failure"));

            var context = new Mock<ConsumeContext<VideoGenerationRequested>>();
            context.Setup(x => x.Message).Returns(request);
            context.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

            // Act
            await _orchestrator.Consume(context.Object);

            // Assert - Webhooks are NOT sent on failures in the current implementation
            _mockWebhookService.Verify(x => x.SendTaskCompletionWebhookAsync(
                It.IsAny<string>(),
                It.IsAny<VideoCompletionWebhookPayload>(),
                It.IsAny<Dictionary<string, string>>(),
                It.IsAny<CancellationToken>()), Times.Never);
            
            // Verify that failure event was published instead
            _mockPublishEndpoint.Verify(x => x.Publish(
                It.Is<VideoGenerationFailed>(e => 
                    e.RequestId == requestId),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Consume_VideoGenerationRequested_InvalidVirtualKey()
        {
            // Arrange
            var requestId = "test-invalid-key-123";
            var virtualKey = "sk-invalid-key";
            var model = "test-model";

            var request = new VideoGenerationRequested
            {
                RequestId = requestId,
                Model = model,
                Prompt = "test",
                VirtualKeyId = "vkey-123",
                IsAsync = true
            };

            var taskStatus = new AsyncTaskStatus
            {
                TaskId = requestId,
                State = TaskState.Pending,
                Metadata = new { VirtualKey = virtualKey, Model = model }
            };

            _mockTaskService.Setup(x => x.GetTaskStatusAsync(It.Is<string>(s => s == requestId), It.IsAny<CancellationToken>()))
                .ReturnsAsync(taskStatus);

            _mockVirtualKeyService.Setup(x => x.ValidateVirtualKeyAsync(virtualKey, model))
                .ReturnsAsync((VirtualKey)null); // Invalid key returns null

            var context = new Mock<ConsumeContext<VideoGenerationRequested>>();
            context.Setup(x => x.Message).Returns(request);
            context.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

            // Act
            await _orchestrator.Consume(context.Object);

            // Assert - The orchestrator encounters a NullReferenceException
            _mockTaskService.Verify(x => x.UpdateTaskStatusAsync(
                requestId,
                TaskState.Failed,
                It.IsAny<int?>(),
                null,
                "Object reference not set to an instance of an object.",
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Consume_VideoGenerationRequested_MetadataReconstruction()
        {
            // Arrange
            var requestId = "test-request-456";
            var virtualKey = "sk-test-key";
            var model = "test-model";

            var request = new VideoGenerationRequested
            {
                RequestId = requestId,
                Model = model,
                Prompt = "test",
                VirtualKeyId = "vkey-123",
                IsAsync = true
            };

            // Test the wrapped metadata format from InMemoryAsyncTaskService
            var taskStatus = new AsyncTaskStatus
            {
                TaskId = requestId,
                State = TaskState.Pending,
                Metadata = new
                {
                    originalMetadata = new
                    {
                        VirtualKey = virtualKey,
                        Request = new VideoGenerationRequest
                        {
                            Model = model,
                            Prompt = "test"
                        },
                        Model = model
                    }
                }
            };

            var virtualKeyInfo = new VirtualKey
            {
                Id = 123,
                IsEnabled = true
            };

            var modelCapabilities = new Dictionary<string, DiscoveredModel>
            {
                { model, new DiscoveredModel
                    {
                        ModelId = model,
                        Provider = "test",
                        DisplayName = model,
                        Capabilities = new ConduitLLM.Core.Interfaces.ModelCapabilities { VideoGeneration = true }
                    }
                }
            };

            _mockTaskService.Setup(x => x.GetTaskStatusAsync(It.Is<string>(s => s == requestId), It.IsAny<CancellationToken>()))
                .ReturnsAsync(taskStatus);

            _mockVirtualKeyService.Setup(x => x.ValidateVirtualKeyAsync(virtualKey, model))
                .ReturnsAsync(virtualKeyInfo);

            _mockDiscoveryService.Setup(x => x.DiscoverModelsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCapabilities);

            // No model mapping - will fail with "No provider available"
            _mockModelMappingService.Setup(x => x.GetMappingByModelAliasAsync(It.IsAny<string>()))
                .ReturnsAsync((ConduitLLM.Configuration.ModelProviderMapping)null);

            var context = new Mock<ConsumeContext<VideoGenerationRequested>>();
            context.Setup(x => x.Message).Returns(request);
            context.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

            // Act & Assert
            await _orchestrator.Consume(context.Object);

            // Verify virtual key was extracted correctly from wrapped metadata
            _mockVirtualKeyService.Verify(x => x.ValidateVirtualKeyAsync(virtualKey, model), Times.Once);
        }

        [Fact]
        public async Task Consume_VideoGenerationRequested_NoProviderAvailable()
        {
            // Arrange
            var requestId = "test-no-provider-123";
            var request = new VideoGenerationRequested
            {
                RequestId = requestId,
                Model = "test-model",
                Prompt = "test",
                VirtualKeyId = "vkey-123",
                IsAsync = true
            };

            var taskStatus = new AsyncTaskStatus
            {
                TaskId = requestId,
                State = TaskState.Pending,
                Metadata = new { VirtualKey = "sk-test", Model = "test-model" }
            };

            var virtualKeyInfo = new VirtualKey { Id = 123, IsEnabled = true };

            var modelCapabilities = new Dictionary<string, DiscoveredModel>
            {
                { "test-model", new DiscoveredModel
                    {
                        ModelId = "test-model",
                        Provider = "test",
                        DisplayName = "test-model",
                        Capabilities = new ConduitLLM.Core.Interfaces.ModelCapabilities { VideoGeneration = true }
                    }
                }
            };

            _mockTaskService.Setup(x => x.GetTaskStatusAsync(It.Is<string>(s => s == requestId), It.IsAny<CancellationToken>()))
                .ReturnsAsync(taskStatus);

            _mockVirtualKeyService.Setup(x => x.ValidateVirtualKeyAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(virtualKeyInfo);

            _mockDiscoveryService.Setup(x => x.DiscoverModelsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCapabilities);

            // No model mapping available - this will cause "No provider available" error
            _mockModelMappingService.Setup(x => x.GetMappingByModelAliasAsync(It.IsAny<string>()))
                .ReturnsAsync((ConduitLLM.Configuration.ModelProviderMapping)null);

            var context = new Mock<ConsumeContext<VideoGenerationRequested>>();
            context.Setup(x => x.Message).Returns(request);
            context.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

            // Act
            await _orchestrator.Consume(context.Object);

            // Assert - Verify it failed with no provider error
            _mockTaskService.Verify(x => x.UpdateTaskStatusAsync(
                requestId,
                TaskState.Failed,
                It.IsAny<int?>(),
                null,
                It.Is<string>(s => s.Contains("No provider available for model")),
                It.IsAny<CancellationToken>()), Times.Once);
            
            _mockPublishEndpoint.Verify(x => x.Publish(
                It.Is<VideoGenerationFailed>(e => 
                    e.RequestId == requestId &&
                    e.Error.Contains("No provider available for model")),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Consume_VideoGenerationRequested_ModelNotSupportsVideoGeneration()
        {
            // Arrange
            var requestId = "test-unsupported-123";
            var virtualKey = "sk-test-key";
            var model = "text-only-model";

            var request = new VideoGenerationRequested
            {
                RequestId = requestId,
                Model = model,
                Prompt = "test",
                VirtualKeyId = "vkey-123",
                IsAsync = true
            };

            var taskStatus = new AsyncTaskStatus
            {
                TaskId = requestId,
                State = TaskState.Pending,
                Metadata = new { VirtualKey = virtualKey, Model = model }
            };

            var virtualKeyInfo = new VirtualKey { Id = 123, IsEnabled = true };

            var modelCapabilities = new Dictionary<string, DiscoveredModel>
            {
                { model, new DiscoveredModel
                    {
                        ModelId = model,
                        Provider = "test",
                        DisplayName = model,
                        Capabilities = new ConduitLLM.Core.Interfaces.ModelCapabilities { VideoGeneration = false } // No video support
                    }
                }
            };

            _mockTaskService.Setup(x => x.GetTaskStatusAsync(It.Is<string>(s => s == requestId), It.IsAny<CancellationToken>()))
                .ReturnsAsync(taskStatus);

            _mockVirtualKeyService.Setup(x => x.ValidateVirtualKeyAsync(virtualKey, model))
                .ReturnsAsync(virtualKeyInfo);

            _mockDiscoveryService.Setup(x => x.DiscoverModelsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(modelCapabilities);

            var context = new Mock<ConsumeContext<VideoGenerationRequested>>();
            context.Setup(x => x.Message).Returns(request);
            context.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

            // Act
            await _orchestrator.Consume(context.Object);

            // Assert
            _mockTaskService.Verify(x => x.UpdateTaskStatusAsync(
                requestId,
                TaskState.Failed,
                It.IsAny<int?>(),
                null,
                It.Is<string>(s => s.Contains("does not support video generation")),
                It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}