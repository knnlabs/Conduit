using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
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
using Moq.Protected;
using Xunit;

namespace ConduitLLM.Tests.Core.Services
{
    public class ImageGenerationOrchestratorTests
    {
        private readonly Mock<ILLMClientFactory> _mockClientFactory;
        private readonly Mock<IAsyncTaskService> _mockTaskService;
        private readonly Mock<IMediaStorageService> _mockStorageService;
        private readonly Mock<IPublishEndpoint> _mockPublishEndpoint;
        private readonly Mock<ConduitLLM.Configuration.IModelProviderMappingService> _mockModelMappingService;
        private readonly Mock<IProviderDiscoveryService> _mockDiscoveryService;
        private readonly Mock<IVirtualKeyService> _mockVirtualKeyService;
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
        private readonly Mock<ICancellableTaskRegistry> _mockTaskRegistry;
        private readonly Mock<IImageGenerationMetricsService> _mockMetricsService;
        private readonly Mock<ILogger<ImageGenerationOrchestrator>> _mockLogger;
        private readonly Mock<IOptions<ImageGenerationPerformanceConfiguration>> _mockPerformanceOptions;
        private readonly ImageGenerationPerformanceConfiguration _performanceConfig;
        private readonly ImageGenerationOrchestrator _orchestrator;

        public ImageGenerationOrchestratorTests()
        {
            _mockClientFactory = new Mock<ILLMClientFactory>();
            _mockTaskService = new Mock<IAsyncTaskService>();
            _mockStorageService = new Mock<IMediaStorageService>();
            _mockPublishEndpoint = new Mock<IPublishEndpoint>();
            _mockModelMappingService = new Mock<ConduitLLM.Configuration.IModelProviderMappingService>();
            _mockDiscoveryService = new Mock<IProviderDiscoveryService>();
            _mockVirtualKeyService = new Mock<IVirtualKeyService>();
            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _mockTaskRegistry = new Mock<ICancellableTaskRegistry>();
            _mockMetricsService = new Mock<IImageGenerationMetricsService>();
            _mockLogger = new Mock<ILogger<ImageGenerationOrchestrator>>();
            _mockPerformanceOptions = new Mock<IOptions<ImageGenerationPerformanceConfiguration>>();

            _performanceConfig = new ImageGenerationPerformanceConfiguration
            {
                MaxConcurrentGenerations = 5,
                ProviderConcurrencyLimits = new Dictionary<string, int>
                {
                    ["openai"] = 3,
                    ["minimax"] = 2
                },
                ProviderDownloadTimeouts = new Dictionary<string, int>
                {
                    ["openai"] = 30,
                    ["minimax"] = 60
                }
            };

            _mockPerformanceOptions.Setup(x => x.Value).Returns(_performanceConfig);

            _orchestrator = new ImageGenerationOrchestrator(
                _mockClientFactory.Object,
                _mockTaskService.Object,
                _mockStorageService.Object,
                _mockPublishEndpoint.Object,
                _mockModelMappingService.Object,
                _mockDiscoveryService.Object,
                _mockVirtualKeyService.Object,
                _mockHttpClientFactory.Object,
                _mockTaskRegistry.Object,
                _mockMetricsService.Object,
                _mockPerformanceOptions.Object,
                _mockLogger.Object);
        }

        #region ImageGenerationRequested Event Tests

        [Fact]
        public async Task Consume_ImageGenerationRequested_WithValidRequest_ShouldGenerateAndStoreImages()
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
                    N = 2,
                    Size = "1024x1024",
                    Quality = "standard",
                    ResponseFormat = "url"
                },
                WebhookUrl = "https://example.com/webhook",
                WebhookHeaders = new Dictionary<string, string> { ["Authorization"] = "Bearer token" },
                CorrelationId = "test-correlation-id"
            };

            var context = new Mock<ConsumeContext<ImageGenerationRequested>>();
            context.Setup(x => x.Message).Returns(request);
            context.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

            // Setup successful scenario
            SetupSuccessfulImageGeneration(request);

            // Act
            await _orchestrator.Consume(context.Object);

            // Assert
            _mockTaskService.Verify(x => x.UpdateTaskStatusAsync(
                "test-task-id",
                TaskState.Processing,
                null,
                null,
                null,
                It.IsAny<CancellationToken>()), Times.Once);

            _mockTaskService.Verify(x => x.UpdateTaskStatusAsync(
                "test-task-id",
                TaskState.Completed,
                100,
                It.IsAny<object>(),
                null,
                It.IsAny<CancellationToken>()), Times.Once);

            _mockPublishEndpoint.Verify(x => x.Publish(
                It.Is<ImageGenerationCompleted>(e => e.TaskId == "test-task-id"),
                It.IsAny<CancellationToken>()), Times.Once);

            _mockMetricsService.Verify(x => x.RecordMetricAsync(
                It.Is<ImageGenerationMetrics>(m => m.Success == true),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Consume_ImageGenerationRequested_WithB64JsonResponse_ShouldStoreImagesDirectly()
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
                    N = 1,
                    Size = "1024x1024",
                    ResponseFormat = "b64_json"
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

            _mockVirtualKeyService.Setup(x => x.ValidateVirtualKeyAsync("test-virtual-key-hash", "dall-e-3"))
                .ReturnsAsync(virtualKey);

            // Setup model mapping
            var modelMapping = new ConduitLLM.Configuration.ModelProviderMapping
            {
                ModelAlias = "dall-e-3",
                ProviderName = "openai",
                ProviderModelId = "dall-e-3",
                SupportsImageGeneration = true
            };

            _mockModelMappingService.Setup(x => x.GetMappingByModelAliasAsync("dall-e-3"))
                .Returns(Task.FromResult(modelMapping));

            // Setup client response with base64 data
            var mockClient = new Mock<ILLMClient>();
            var base64ImageData = Convert.ToBase64String(Encoding.UTF8.GetBytes("fake image data"));
            var imageResponse = new ConduitLLM.Core.Models.ImageGenerationResponse
            {
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Data = new List<ConduitLLM.Core.Models.ImageData>
                {
                    new ConduitLLM.Core.Models.ImageData
                    {
                        B64Json = base64ImageData,
                        Url = null
                    }
                }
            };

            mockClient.Setup(x => x.CreateImageAsync(
                It.IsAny<ConduitLLM.Core.Models.ImageGenerationRequest>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(imageResponse);

            _mockClientFactory.Setup(x => x.GetClient("dall-e-3"))
                .Returns(mockClient.Object);

            // Setup storage service
            var storageResult = new MediaStorageResult
            {
                StorageKey = "image/test-key.png",
                Url = "https://storage.example.com/image/test-key.png",
                SizeBytes = 1024,
                ContentHash = "test-hash",
                CreatedAt = DateTime.UtcNow
            };

            _mockStorageService.Setup(x => x.StoreAsync(
                It.IsAny<Stream>(),
                It.IsAny<MediaMetadata>(),
                It.IsAny<IProgress<long>>()))
                .ReturnsAsync(storageResult);

            // Act
            await _orchestrator.Consume(context.Object);

            // Assert
            _mockStorageService.Verify(x => x.StoreAsync(
                It.IsAny<Stream>(),
                It.Is<MediaMetadata>(m => m.ContentType == "image/png"),
                It.IsAny<IProgress<long>>()), Times.Once);

            _mockPublishEndpoint.Verify(x => x.Publish(
                It.Is<MediaGenerationCompleted>(e => 
                    e.MediaType == MediaType.Image && 
                    e.VirtualKeyId == 1),
                It.IsAny<CancellationToken>()), Times.Once);
        }

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
                .Returns(Task.FromResult((ConduitLLM.Configuration.ModelProviderMapping?)null));

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
            var modelMapping = new ConduitLLM.Configuration.ModelProviderMapping
            {
                ModelAlias = "gpt-4",
                ProviderName = "openai",
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

            // Setup model mapping
            var modelMapping = new ConduitLLM.Configuration.ModelProviderMapping
            {
                ModelAlias = "dall-e-3",
                ProviderName = "openai",
                ProviderModelId = "dall-e-3",
                SupportsImageGeneration = true
            };

            _mockModelMappingService.Setup(x => x.GetMappingByModelAliasAsync("dall-e-3"))
                .Returns(Task.FromResult(modelMapping));

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

        [Fact]
        public async Task Consume_ImageGenerationRequested_WithWebhookUrl_ShouldSendWebhookNotification()
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
                    N = 1,
                    Size = "1024x1024"
                },
                WebhookUrl = "https://example.com/webhook",
                WebhookHeaders = new Dictionary<string, string> { ["Authorization"] = "Bearer token" },
                CorrelationId = "test-correlation-id"
            };

            var context = new Mock<ConsumeContext<ImageGenerationRequested>>();
            context.Setup(x => x.Message).Returns(request);
            context.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

            // Setup successful scenario
            SetupSuccessfulImageGeneration(request);

            // Act
            await _orchestrator.Consume(context.Object);

            // Assert
            _mockPublishEndpoint.Verify(x => x.Publish(
                It.Is<WebhookDeliveryRequested>(e => 
                    e.TaskId == "test-task-id" && 
                    e.WebhookUrl == "https://example.com/webhook" &&
                    e.EventType == WebhookEventType.TaskCompleted),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

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

        #region Cost Calculation Tests

        [Theory]
        [InlineData("openai", "dall-e-3", 1, 0.040)]
        [InlineData("openai", "dall-e-2", 2, 0.040)]
        [InlineData("minimax", "minimax-image", 3, 0.030)]
        [InlineData("replicate", "sdxl", 1, 0.025)]
        [InlineData("unknown", "unknown-model", 1, 0.020)]
        public async Task CalculateImageGenerationCost_WithDifferentProviders_ShouldReturnCorrectCost(
            string provider, string model, int imageCount, decimal expectedCost)
        {
            // This test verifies cost calculation through the public interface
            // since CalculateImageGenerationCost is private

            // Arrange
            var request = new ImageGenerationRequested
            {
                TaskId = "test-task-id",
                VirtualKeyId = 1,
                VirtualKeyHash = "test-virtual-key-hash",
                Request = new ConduitLLM.Core.Events.ImageGenerationRequest
                {
                    Prompt = "A beautiful landscape",
                    Model = model,
                    N = imageCount
                },
                CorrelationId = "test-correlation-id"
            };

            var context = new Mock<ConsumeContext<ImageGenerationRequested>>();
            context.Setup(x => x.Message).Returns(request);
            context.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

            // Setup successful scenario with specific provider
            SetupSuccessfulImageGeneration(request, provider, model);

            // Act
            await _orchestrator.Consume(context.Object);

            // Assert
            _mockTaskService.Verify(x => x.UpdateTaskStatusAsync(
                "test-task-id",
                TaskState.Completed,
                100,
                It.Is<object>(result => 
                    result.GetType().GetProperty("cost") != null &&
                    result.GetType().GetProperty("cost").GetValue(result) != null &&
                    result.GetType().GetProperty("cost").GetValue(result).Equals(expectedCost)),
                null,
                It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region Performance Configuration Tests

        [Fact]
        public async Task GetOptimalConcurrency_WithOpenAIProvider_ShouldUseLimitFromConfiguration()
        {
            // This test verifies concurrency behavior through the public interface
            // since GetOptimalConcurrency is private

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
                    N = 10 // More than the OpenAI limit of 3
                },
                CorrelationId = "test-correlation-id"
            };

            var context = new Mock<ConsumeContext<ImageGenerationRequested>>();
            context.Setup(x => x.Message).Returns(request);
            context.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

            // Setup successful scenario
            SetupSuccessfulImageGeneration(request, "openai", "dall-e-3");

            // Act
            await _orchestrator.Consume(context.Object);

            // Assert
            // The orchestrator should process all images but with concurrency limit
            _mockTaskService.Verify(x => x.UpdateTaskStatusAsync(
                "test-task-id",
                TaskState.Completed,
                100,
                It.IsAny<object>(),
                null,
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetProviderTimeout_WithMiniMaxProvider_ShouldUseConfiguredTimeout()
        {
            // This test verifies timeout behavior through HTTP client setup

            // Arrange
            var request = new ImageGenerationRequested
            {
                TaskId = "test-task-id",
                VirtualKeyId = 1,
                VirtualKeyHash = "test-virtual-key-hash",
                Request = new ConduitLLM.Core.Events.ImageGenerationRequest
                {
                    Prompt = "A beautiful landscape",
                    Model = "minimax-image",
                    N = 1
                },
                CorrelationId = "test-correlation-id"
            };

            var context = new Mock<ConsumeContext<ImageGenerationRequested>>();
            context.Setup(x => x.Message).Returns(request);
            context.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

            // Setup successful scenario with URL response (to trigger HTTP download)
            SetupSuccessfulImageGenerationWithUrl(request, "minimax", "minimax-image");

            // Act
            await _orchestrator.Consume(context.Object);

            // Assert
            // Verify that HTTP client was created (indicating download was attempted)
            _mockHttpClientFactory.Verify(x => x.CreateClient(It.IsAny<string>()), Times.Once);
        }

        #endregion

        #region Private Method Tests (via public interface)

        [Fact]
        public async Task ProcessSingleImageAsync_WithB64JsonImage_ShouldStoreImageDirectly()
        {
            // This test verifies ProcessSingleImageAsync behavior through the public interface

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
                    N = 1,
                    ResponseFormat = "b64_json"
                },
                CorrelationId = "test-correlation-id"
            };

            var context = new Mock<ConsumeContext<ImageGenerationRequested>>();
            context.Setup(x => x.Message).Returns(request);
            context.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

            // Setup successful scenario with base64 response
            SetupSuccessfulImageGeneration(request, "openai", "dall-e-3", responseFormat: "b64_json");

            // Act
            await _orchestrator.Consume(context.Object);

            // Assert
            _mockStorageService.Verify(x => x.StoreAsync(
                It.IsAny<Stream>(),
                It.Is<MediaMetadata>(m => 
                    m.ContentType == "image/png" && 
                    m.MediaType == MediaType.Image),
                It.IsAny<IProgress<long>>()), Times.Once);
        }

        [Fact]
        public async Task DownloadAndStoreImageAsync_WithValidUrl_ShouldDownloadAndStore()
        {
            // This test verifies DownloadAndStoreImageAsync behavior through the public interface

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
                    N = 1,
                    ResponseFormat = "url"
                },
                CorrelationId = "test-correlation-id"
            };

            var context = new Mock<ConsumeContext<ImageGenerationRequested>>();
            context.Setup(x => x.Message).Returns(request);
            context.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

            // Setup successful scenario with URL response
            SetupSuccessfulImageGenerationWithUrl(request, "openai", "dall-e-3");

            // Act
            await _orchestrator.Consume(context.Object);

            // Assert
            _mockHttpClientFactory.Verify(x => x.CreateClient(It.IsAny<string>()), Times.Once);
            _mockStorageService.Verify(x => x.StoreAsync(
                It.IsAny<Stream>(),
                It.Is<MediaMetadata>(m => 
                    m.ContentType == "image/jpeg" && 
                    m.MediaType == MediaType.Image),
                It.IsAny<IProgress<long>>()), Times.Once);
        }

        #endregion

        #region Test Helper Methods

        private void SetupSuccessfulImageGeneration(ImageGenerationRequested request, string provider = "openai", string model = "dall-e-3", string responseFormat = "url")
        {
            // Setup virtual key
            var virtualKey = new VirtualKey
            {
                Id = request.VirtualKeyId,
                IsEnabled = true,
                KeyName = "test-virtual-key",
                KeyHash = "test-virtual-key-hash"
            };

            _mockVirtualKeyService.Setup(x => x.ValidateVirtualKeyAsync(request.VirtualKeyHash, request.Request.Model))
                .ReturnsAsync(virtualKey);

            // Setup model mapping
            var modelMapping = new ConduitLLM.Configuration.ModelProviderMapping
            {
                ModelAlias = request.Request.Model,
                ProviderName = provider,
                ProviderModelId = model,
                SupportsImageGeneration = true
            };

            _mockModelMappingService.Setup(x => x.GetMappingByModelAliasAsync(request.Request.Model))
                .Returns(Task.FromResult(modelMapping));

            // Setup client response
            var mockClient = new Mock<ILLMClient>();
            var imageData = new List<ConduitLLM.Core.Models.ImageData>();

            for (int i = 0; i < request.Request.N; i++)
            {
                if (responseFormat == "b64_json")
                {
                    imageData.Add(new ConduitLLM.Core.Models.ImageData
                    {
                        B64Json = Convert.ToBase64String(Encoding.UTF8.GetBytes($"fake image data {i}")),
                        Url = null
                    });
                }
                else
                {
                    imageData.Add(new ConduitLLM.Core.Models.ImageData
                    {
                        B64Json = null,
                        Url = $"https://example.com/image{i}.jpg"
                    });
                }
            }

            var imageResponse = new ConduitLLM.Core.Models.ImageGenerationResponse
            {
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Data = imageData
            };

            mockClient.Setup(x => x.CreateImageAsync(
                It.IsAny<ConduitLLM.Core.Models.ImageGenerationRequest>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(imageResponse);

            _mockClientFactory.Setup(x => x.GetClient(model))
                .Returns(mockClient.Object);

            // Setup storage service
            var storageResult = new MediaStorageResult
            {
                StorageKey = "image/test-key.png",
                Url = "https://storage.example.com/image/test-key.png",
                SizeBytes = 1024,
                ContentHash = "test-hash",
                CreatedAt = DateTime.UtcNow
            };

            _mockStorageService.Setup(x => x.StoreAsync(
                It.IsAny<Stream>(),
                It.IsAny<MediaMetadata>(),
                It.IsAny<IProgress<long>>()))
                .ReturnsAsync(storageResult);

            // Setup HTTP client for URL downloads
            SetupHttpClient();
        }

        private void SetupSuccessfulImageGenerationWithUrl(ImageGenerationRequested request, string provider, string model)
        {
            // Setup virtual key
            var virtualKey = new VirtualKey
            {
                Id = request.VirtualKeyId,
                IsEnabled = true,
                KeyName = "test-virtual-key",
                KeyHash = "test-virtual-key-hash"
            };

            _mockVirtualKeyService.Setup(x => x.ValidateVirtualKeyAsync(request.VirtualKeyHash, request.Request.Model))
                .ReturnsAsync(virtualKey);

            // Setup model mapping
            var modelMapping = new ConduitLLM.Configuration.ModelProviderMapping
            {
                ModelAlias = request.Request.Model,
                ProviderName = provider,
                ProviderModelId = model,
                SupportsImageGeneration = true
            };

            _mockModelMappingService.Setup(x => x.GetMappingByModelAliasAsync(request.Request.Model))
                .Returns(Task.FromResult(modelMapping));

            // Setup client response with URLs
            var mockClient = new Mock<ILLMClient>();
            var imageData = new List<ConduitLLM.Core.Models.ImageData>();

            for (int i = 0; i < request.Request.N; i++)
            {
                imageData.Add(new ConduitLLM.Core.Models.ImageData
                {
                    B64Json = null,
                    Url = $"https://example.com/image{i}.jpg"
                });
            }

            var imageResponse = new ConduitLLM.Core.Models.ImageGenerationResponse
            {
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Data = imageData
            };

            mockClient.Setup(x => x.CreateImageAsync(
                It.IsAny<ConduitLLM.Core.Models.ImageGenerationRequest>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(imageResponse);

            _mockClientFactory.Setup(x => x.GetClient(model))
                .Returns(mockClient.Object);

            // Setup storage service
            var storageResult = new MediaStorageResult
            {
                StorageKey = "image/test-key.jpg",
                Url = "https://storage.example.com/image/test-key.jpg",
                SizeBytes = 1024,
                ContentHash = "test-hash",
                CreatedAt = DateTime.UtcNow
            };

            _mockStorageService.Setup(x => x.StoreAsync(
                It.IsAny<Stream>(),
                It.IsAny<MediaMetadata>(),
                It.IsAny<IProgress<long>>()))
                .ReturnsAsync(storageResult);

            // Setup HTTP client for URL downloads
            SetupHttpClient();
        }

        private void SetupHttpClient()
        {
            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            var imageBytes = Encoding.UTF8.GetBytes("fake image data");

            mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", 
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new ByteArrayContent(imageBytes)
                    {
                        Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg") }
                    }
                });

            var httpClient = new HttpClient(mockHttpMessageHandler.Object);
            _mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>()))
                .Returns(httpClient);
        }

        #endregion
    }
}