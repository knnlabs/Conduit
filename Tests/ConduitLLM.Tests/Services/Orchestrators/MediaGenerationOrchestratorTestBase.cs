using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using IVirtualKeyService = ConduitLLM.Core.Interfaces.IVirtualKeyService;
using IModelProviderMappingService = ConduitLLM.Configuration.Interfaces.IModelProviderMappingService;
using ConduitLLM.Core.Configuration;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Metrics;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services.Abstractions;
using ConduitLLM.Core.Validation;
using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Services.Orchestrators
{
    /// <summary>
    /// Base test class for all media generation orchestrators.
    /// Provides common test cases and helper methods.
    /// </summary>
    public abstract class MediaGenerationOrchestratorTestBase<TOrchestrator, TRequest, TResponse, TEventRequest>
        where TOrchestrator : MediaGenerationOrchestrator<TRequest, TResponse, TEventRequest>
        where TRequest : class
        where TResponse : class
        where TEventRequest : class
    {
        // Mocked dependencies
        protected readonly Mock<ILLMClientFactory> ClientFactoryMock;
        protected readonly Mock<IAsyncTaskService> TaskServiceMock;
        protected readonly Mock<IMediaStorageService> StorageServiceMock;
        protected readonly Mock<IPublishEndpoint> PublishEndpointMock;
        protected readonly Mock<IModelProviderMappingService> ModelMappingServiceMock;
        protected readonly Mock<IVirtualKeyService> VirtualKeyServiceMock;
        protected readonly Mock<ICostCalculationService> CostServiceMock;
        protected readonly Mock<ICancellableTaskRegistry> TaskRegistryMock;
        protected readonly Mock<IWebhookNotificationService> WebhookServiceMock;
        protected readonly Mock<IHttpClientFactory> HttpClientFactoryMock;
        protected readonly Mock<MinimalParameterValidator> ParameterValidatorMock;
        protected readonly MediaGenerationMetrics Metrics;
        protected readonly Mock<IProviderErrorTrackingService> ErrorTrackingServiceMock;
        protected readonly Mock<ILogger> LoggerMock;

        // System under test
        private TOrchestrator? _orchestrator;
        protected TOrchestrator Orchestrator 
        { 
            get 
            { 
                if (_orchestrator == null)
                {
                    _orchestrator = CreateOrchestrator();
                }
                return _orchestrator;
            }
        }

        // Abstract methods to get properties from event
        protected abstract string GetRequestId(TEventRequest request);
        protected abstract string? GetWebhookUrl(TEventRequest request);

        protected MediaGenerationOrchestratorTestBase()
        {
            // Initialize mocks
            ClientFactoryMock = new Mock<ILLMClientFactory>();
            TaskServiceMock = new Mock<IAsyncTaskService>();
            StorageServiceMock = new Mock<IMediaStorageService>();
            PublishEndpointMock = new Mock<IPublishEndpoint>();
            ModelMappingServiceMock = new Mock<IModelProviderMappingService>();
            VirtualKeyServiceMock = new Mock<IVirtualKeyService>();
            CostServiceMock = new Mock<ICostCalculationService>();
            TaskRegistryMock = new Mock<ICancellableTaskRegistry>();
            WebhookServiceMock = new Mock<IWebhookNotificationService>();
            HttpClientFactoryMock = new Mock<IHttpClientFactory>();
            ErrorTrackingServiceMock = new Mock<IProviderErrorTrackingService>();
            LoggerMock = new Mock<ILogger>();
            
            // MinimalParameterValidator requires a logger in its constructor
            var validatorLoggerMock = new Mock<ILogger<MinimalParameterValidator>>();
            ParameterValidatorMock = new Mock<MinimalParameterValidator>(validatorLoggerMock.Object);
            
            // Create real metrics instance for tests - MediaGenerationMetrics needs a real IMeterFactory
            var meterFactory = new TestMeterFactory();
            Metrics = new MediaGenerationMetrics(meterFactory);

            // Setup default behaviors
            SetupDefaultMocks();

            // Note: Orchestrator is created lazily on first access to allow derived class constructors to run first
        }

        /// <summary>
        /// Creates the specific orchestrator instance to test.
        /// Must be implemented by derived test classes.
        /// </summary>
        protected abstract TOrchestrator CreateOrchestrator();

        /// <summary>
        /// Creates a test event request.
        /// Must be implemented by derived test classes.
        /// </summary>
        protected abstract TEventRequest CreateTestEventRequest();

        /// <summary>
        /// Creates a test response.
        /// Must be implemented by derived test classes.
        /// </summary>
        protected abstract TResponse CreateTestResponse();

        /// <summary>
        /// Sets up default mock behaviors.
        /// </summary>
        protected virtual void SetupDefaultMocks()
        {
            // Setup task service
            var taskMetadata = new TaskMetadata
            {
                VirtualKeyId = 1,
                ExtensionData = new Dictionary<string, object>
                {
                    ["VirtualKey"] = "test-virtual-key"
                }
            };

            TaskServiceMock.Setup(x => x.GetTaskStatusAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AsyncTaskStatus
                {
                    TaskId = "test-task-id",
                    State = TaskState.Pending,
                    Metadata = taskMetadata
                });

            TaskServiceMock.Setup(x => x.UpdateTaskStatusAsync(
                It.IsAny<string>(),
                It.IsAny<TaskState>(),
                It.IsAny<int?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Setup virtual key service
            VirtualKeyServiceMock.Setup(x => x.ValidateVirtualKeyAsync(It.IsAny<string>(), It.IsAny<string?>()))
                .ReturnsAsync(new VirtualKey
                {
                    Id = 1,
                    KeyName = "test-virtual-key",
                    KeyHash = "hashed-test-virtual-key",
                    IsEnabled = true,
                    VirtualKeyGroupId = 1
                });

            // Setup model mapping service
            var testProvider = new Provider
            {
                Id = 1,
                ProviderName = "Test Provider",
                ProviderType = ProviderType.OpenAI,
                IsEnabled = true
            };

            ModelMappingServiceMock.Setup(x => x.GetMappingByModelAliasAsync(It.IsAny<string>()))
                .ReturnsAsync(new ModelProviderMapping
                {
                    Id = 1,
                    ModelAlias = "test-model",
                    ProviderModelId = "provider-model-id",
                    ProviderId = 1,
                    Provider = testProvider,
                    IsEnabled = true
                });

            // Setup cost service
            CostServiceMock.Setup(x => x.CalculateCostAsync(
                It.IsAny<string>(),
                It.IsAny<Usage>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(0.01m);

            // Setup publish endpoint
            PublishEndpointMock.Setup(x => x.Publish(
                It.IsAny<object>(),
                It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }

        // ==========================================
        // Common Test Cases
        // ==========================================

        [Fact]
        public async Task Consume_WhenRequestIsValid_ShouldProcessSuccessfully()
        {
            // Arrange
            var request = CreateTestEventRequest();
            var context = CreateConsumeContext(request);
            var response = CreateTestResponse();

            SetupSuccessfulGeneration(response);

            // Act
            await Orchestrator.Consume(context.Object);

            // Assert
            TaskServiceMock.Verify(x => x.UpdateTaskStatusAsync(
                GetRequestId(request),
                TaskState.Processing,
                It.IsAny<int?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()), Times.Once);

            TaskServiceMock.Verify(x => x.UpdateTaskStatusAsync(
                GetRequestId(request),
                TaskState.Completed,
                It.IsAny<int?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Consume_WhenGenerationFails_ShouldUpdateTaskAsFailed()
        {
            // Arrange
            var request = CreateTestEventRequest();
            var context = CreateConsumeContext(request);
            var exception = new InvalidOperationException("Generation failed");

            SetupFailedGeneration(exception);

            // Act - Should handle failure gracefully
            await Orchestrator.Consume(context.Object);

            // Assert - Should update task status to Failed with error message
            TaskServiceMock.Verify(x => x.UpdateTaskStatusAsync(
                GetRequestId(request),
                TaskState.Failed,
                It.IsAny<int?>(),
                It.IsAny<object?>(),
                exception.Message,
                It.IsAny<CancellationToken>()), Times.Once);
        }

        // Note: Cancellation testing removed from base class due to complexity
        // The orchestrator's cancellation handling requires the linked CancellationTokenSource
        // to be cancelled, which only happens when an OperationCanceledException is thrown
        // with the correct token. This is difficult to test generically across all derived
        // orchestrators which have different initialization requirements.
        // Each derived orchestrator can implement its own cancellation test if needed.

        [Fact]
        public async Task Consume_WhenVirtualKeyIsInvalid_ShouldFailGracefully()
        {
            // Arrange
            var request = CreateTestEventRequest();
            var context = CreateConsumeContext(request);

            VirtualKeyServiceMock.Setup(x => x.ValidateVirtualKeyAsync(It.IsAny<string>(), It.IsAny<string?>()))
                .ReturnsAsync((VirtualKey?)null);

            // Act
            await Orchestrator.Consume(context.Object);

            // Assert - Should update task status to Failed with appropriate error
            TaskServiceMock.Verify(x => x.UpdateTaskStatusAsync(
                GetRequestId(request),
                TaskState.Failed,
                It.IsAny<int?>(),
                It.IsAny<object?>(),
                It.Is<string>(s => s.Contains("Virtual key validation returned null") || s.Contains("Invalid") || s.Contains("unauthorized") || s.Contains("disabled")),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Consume_WhenVirtualKeyIsDisabled_ShouldFailGracefully()
        {
            // Arrange
            var request = CreateTestEventRequest();
            var context = CreateConsumeContext(request);

            VirtualKeyServiceMock.Setup(x => x.ValidateVirtualKeyAsync(It.IsAny<string>(), It.IsAny<string?>()))
                .ReturnsAsync(new VirtualKey
                {
                    Id = 1,
                    KeyName = "test-virtual-key",
                    KeyHash = "hashed-test-virtual-key",
                    IsEnabled = false,
                    VirtualKeyGroupId = 1
                });

            // Act
            await Orchestrator.Consume(context.Object);

            // Assert - Should update task status to Failed with appropriate error
            TaskServiceMock.Verify(x => x.UpdateTaskStatusAsync(
                GetRequestId(request),
                TaskState.Failed,
                It.IsAny<int?>(),
                It.IsAny<object?>(),
                It.Is<string>(s => s.Contains("Virtual key validation returned null") || s.Contains("Invalid") || s.Contains("unauthorized") || s.Contains("disabled")),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Consume_WhenModelNotFound_ShouldFailGracefully()
        {
            // Arrange
            var request = CreateTestEventRequest();
            var context = CreateConsumeContext(request);

            ModelMappingServiceMock.Setup(x => x.GetMappingByModelAliasAsync(It.IsAny<string>()))
                .ReturnsAsync((ModelProviderMapping?)null);

            // Act
            await Orchestrator.Consume(context.Object);

            // Assert - Should update task status to Failed with appropriate error
            TaskServiceMock.Verify(x => x.UpdateTaskStatusAsync(
                GetRequestId(request),
                TaskState.Failed,
                It.IsAny<int?>(),
                It.IsAny<object?>(),
                It.Is<string>(s => s.Contains("Model") && (s.Contains("not configured") || s.Contains("not found") || s.Contains("not available"))),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Consume_ShouldRegisterAndUnregisterTaskInRegistry()
        {
            // Arrange
            var request = CreateTestEventRequest();
            var context = CreateConsumeContext(request);
            var response = CreateTestResponse();

            SetupSuccessfulGeneration(response);

            // Act
            await Orchestrator.Consume(context.Object);

            // Assert
            TaskRegistryMock.Verify(x => x.RegisterTask(
                GetRequestId(request),
                It.IsAny<CancellationTokenSource>()), Times.Once);

            TaskRegistryMock.Verify(x => x.UnregisterTask(
                GetRequestId(request)), Times.Once);
        }

        [Fact]
        public async Task Consume_WhenWebhookConfigured_ShouldSendNotification()
        {
            // Arrange
            var request = CreateTestEventRequest();
            var context = CreateConsumeContext(request);
            var response = CreateTestResponse();

            SetupSuccessfulGeneration(response);

            // Act
            await Orchestrator.Consume(context.Object);

            // Assert
            var webhookUrl = GetWebhookUrl(request);
            if (!string.IsNullOrEmpty(webhookUrl))
            {
                PublishEndpointMock.Verify(x => x.Publish(
                    It.Is<WebhookDeliveryRequested>(w => 
                        w.TaskId == GetRequestId(request) &&
                        w.WebhookUrl == webhookUrl),
                    It.IsAny<CancellationToken>()), Times.Once);
            }
        }

        // ==========================================
        // Helper Methods
        // ==========================================

        protected abstract void SetupSuccessfulGeneration(TResponse response);
        protected abstract void SetupFailedGeneration(Exception exception);

        protected Mock<ConsumeContext<TEventRequest>> CreateConsumeContext(
            TEventRequest message, 
            CancellationToken cancellationToken = default)
        {
            var contextMock = new Mock<ConsumeContext<TEventRequest>>();
            contextMock.Setup(x => x.Message).Returns(message);
            contextMock.Setup(x => x.CancellationToken).Returns(cancellationToken);
            contextMock.Setup(x => x.MessageId).Returns(Guid.NewGuid());
            contextMock.Setup(x => x.CorrelationId).Returns(Guid.NewGuid());
            contextMock.Setup(x => x.ConversationId).Returns(Guid.NewGuid());
            
            // Setup publish endpoint interface
            contextMock.As<IPublishEndpoint>()
                .Setup(x => x.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            
            return contextMock;
        }
    }
    
    /// <summary>
    /// Test implementation of IMeterFactory for unit tests
    /// </summary>
    public class TestMeterFactory : System.Diagnostics.Metrics.IMeterFactory
    {
        public System.Diagnostics.Metrics.Meter Create(string name, string? version = null)
        {
            return new System.Diagnostics.Metrics.Meter(name, version);
        }

        public System.Diagnostics.Metrics.Meter Create(System.Diagnostics.Metrics.MeterOptions options)
        {
            return new System.Diagnostics.Metrics.Meter(options.Name, options.Version);
        }

        public void Dispose()
        {
            // No-op for tests
        }
    }
}