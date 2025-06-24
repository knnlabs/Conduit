using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;
using ConduitLLM.Configuration;
using MassTransit;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace ConduitLLM.Tests
{
    /// <summary>
    /// Unit tests for ImageGenerationOrchestrator.
    /// Tests cover all scenarios from issue #162: successful flow, multiple images, 
    /// provider routing, error handling, spend tracking, media storage, and metadata handling.
    /// </summary>
    public class ImageGenerationOrchestratorTests
    {
        private readonly Mock<ILLMClientFactory> _mockClientFactory;
        private readonly Mock<IAsyncTaskService> _mockTaskService;
        private readonly Mock<IMediaStorageService> _mockStorageService;
        private readonly Mock<IPublishEndpoint> _mockPublishEndpoint;
        private readonly Mock<IModelProviderMappingService> _mockMappingService;
        private readonly Mock<IProviderDiscoveryService> _mockDiscoveryService;
        private readonly Mock<IVirtualKeyService> _mockVirtualKeyService;
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
        private readonly Mock<ICancellableTaskRegistry> _mockTaskRegistry;
        private readonly Mock<ILogger<ImageGenerationOrchestrator>> _mockLogger;
        private readonly Mock<ILLMClient> _mockLLMClient;
        private readonly Mock<ConsumeContext<ImageGenerationRequested>> _mockContext;
        private readonly ITestOutputHelper _output;
        
        private readonly ImageGenerationOrchestrator _orchestrator;

        public ImageGenerationOrchestratorTests(ITestOutputHelper output)
        {
            _output = output;
            _mockClientFactory = new Mock<ILLMClientFactory>();
            _mockTaskService = new Mock<IAsyncTaskService>();
            _mockStorageService = new Mock<IMediaStorageService>();
            _mockPublishEndpoint = new Mock<IPublishEndpoint>();
            _mockMappingService = new Mock<IModelProviderMappingService>();
            _mockDiscoveryService = new Mock<IProviderDiscoveryService>();
            _mockVirtualKeyService = new Mock<IVirtualKeyService>();
            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _mockTaskRegistry = new Mock<ICancellableTaskRegistry>();
            _mockLogger = new Mock<ILogger<ImageGenerationOrchestrator>>();
            _mockLLMClient = new Mock<ILLMClient>();
            _mockContext = new Mock<ConsumeContext<ImageGenerationRequested>>();

            _orchestrator = new ImageGenerationOrchestrator(
                _mockClientFactory.Object,
                _mockTaskService.Object,
                _mockStorageService.Object,
                _mockPublishEndpoint.Object,
                _mockMappingService.Object,
                _mockDiscoveryService.Object,
                _mockVirtualKeyService.Object,
                _mockHttpClientFactory.Object,
                _mockTaskRegistry.Object,
                _mockLogger.Object);
        }

        #region Happy Path Tests

        [Fact]
        public async Task Consume_SuccessfulImageGeneration_ShouldCompleteWithResult()
        {
            // Arrange
            var taskId = "test-task-123";
            var correlationId = "test-correlation-456";
            var virtualKeyId = 1;
            var virtualKeyHash = "test-key-hash";
            
            var request = new ImageGenerationRequested
            {
                TaskId = taskId,
                VirtualKeyId = virtualKeyId,
                VirtualKeyHash = virtualKeyHash,
                CorrelationId = correlationId,
                Request = new ConduitLLM.Core.Events.ImageGenerationRequest
                {
                    Prompt = "A beautiful sunset",
                    Model = "dall-e-3",
                    N = 1,
                    Size = "1024x1024",
                    Quality = "standard",
                    ResponseFormat = "url"
                }
            };

            var virtualKey = new ConduitLLM.Configuration.Entities.VirtualKey
            {
                Id = virtualKeyId,
                KeyHash = virtualKeyHash,
                IsEnabled = true
            };

            var modelMapping = new ModelProviderMapping
            {
                ModelAlias = "dall-e-3",
                ProviderName = "OpenAI",
                ProviderModelId = "dall-e-3",
                SupportsImageGeneration = true
            };

            var imageResponse = new ConduitLLM.Core.Models.ImageGenerationResponse
            {
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Data = new List<ConduitLLM.Core.Models.ImageData>
                {
                    new ConduitLLM.Core.Models.ImageData
                    {
                        Url = "https://example.com/image1.png"
                    }
                }
            };

            // Setup mocks
            _mockContext.Setup(x => x.Message).Returns(request);
            _mockContext.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

            _mockVirtualKeyService.Setup(x => x.ValidateVirtualKeyAsync(virtualKeyHash, "dall-e-3"))
                .ReturnsAsync(virtualKey);

            _mockMappingService.Setup(x => x.GetMappingByModelAliasAsync("dall-e-3"))
                .ReturnsAsync(modelMapping);

            _mockClientFactory.Setup(x => x.GetClient("dall-e-3"))
                .Returns(_mockLLMClient.Object);

            _mockLLMClient.Setup(x => x.CreateImageAsync(It.IsAny<ConduitLLM.Core.Models.ImageGenerationRequest>(), null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(imageResponse);

            // Act
            await _orchestrator.Consume(_mockContext.Object);

            // Assert
            _mockTaskService.Verify(x => x.UpdateTaskStatusAsync(
                taskId, 
                TaskState.Processing, 
                null, 
                null, 
                null, 
                It.IsAny<CancellationToken>()), Times.Once);

            _mockTaskService.Verify(x => x.UpdateTaskStatusAsync(
                taskId, 
                TaskState.Completed, 
                100, 
                It.IsAny<object>(), 
                null, 
                It.IsAny<CancellationToken>()), Times.Once);

            _mockPublishEndpoint.Verify(x => x.Publish(
                It.IsAny<ImageGenerationProgress>(), 
                It.IsAny<CancellationToken>()), Times.AtLeastOnce);

            _mockPublishEndpoint.Verify(x => x.Publish(
                It.IsAny<ImageGenerationCompleted>(), 
                It.IsAny<CancellationToken>()), Times.Once);

            _output.WriteLine("Successful image generation flow completed");
        }

        [Fact]
        public async Task Consume_MultipleImageGeneration_ShouldHandleNParameter()
        {
            // Arrange
            var taskId = "test-task-multi";
            var request = CreateTestImageGenerationRequest(taskId, n: 3);

            var imageResponse = new ConduitLLM.Core.Models.ImageGenerationResponse
            {
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Data = new List<ConduitLLM.Core.Models.ImageData>
                {
                    new ConduitLLM.Core.Models.ImageData { Url = "https://example.com/image1.png" },
                    new ConduitLLM.Core.Models.ImageData { Url = "https://example.com/image2.png" },
                    new ConduitLLM.Core.Models.ImageData { Url = "https://example.com/image3.png" }
                }
            };

            SetupBasicMocks(request, imageResponse);

            // Act
            await _orchestrator.Consume(_mockContext.Object);

            // Assert
            _mockPublishEndpoint.Verify(x => x.Publish(
                It.Is<ImageGenerationProgress>(p => p.TotalImages == 3),
                It.IsAny<CancellationToken>()), Times.AtLeastOnce);

            _mockPublishEndpoint.Verify(x => x.Publish(
                It.Is<ImageGenerationCompleted>(c => c.Images.Count == 3),
                It.IsAny<CancellationToken>()), Times.Once);

            _output.WriteLine("Multiple image generation with N=3 completed successfully");
        }

        [Theory]
        [InlineData("1024x1024", "standard")]
        [InlineData("1792x1024", "hd")]
        [InlineData("1024x1792", "standard")]
        public async Task Consume_DifferentImageSizesAndQuality_ShouldHandleAllFormats(string size, string quality)
        {
            // Arrange
            var taskId = $"test-task-{size}-{quality}";
            var request = CreateTestImageGenerationRequest(taskId, size: size, quality: quality);

            var imageResponse = new ConduitLLM.Core.Models.ImageGenerationResponse
            {
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Data = new List<ConduitLLM.Core.Models.ImageData>
                {
                    new ConduitLLM.Core.Models.ImageData { Url = "https://example.com/image.png" }
                }
            };

            SetupBasicMocks(request, imageResponse);

            // Act
            await _orchestrator.Consume(_mockContext.Object);

            // Assert
            _mockLLMClient.Verify(x => x.CreateImageAsync(
                It.Is<ConduitLLM.Core.Models.ImageGenerationRequest>(r => 
                    r.Size == size && r.Quality == quality),
                null,
                It.IsAny<CancellationToken>()), Times.Once);

            _mockTaskService.Verify(x => x.UpdateTaskStatusAsync(
                taskId, TaskState.Completed, 100, It.IsAny<object>(), null, It.IsAny<CancellationToken>()), Times.Once);

            _output.WriteLine($"Image generation with size {size} and quality {quality} completed");
        }

        #endregion

        #region Provider Routing Tests

        [Theory]
        [InlineData("dall-e-3", "OpenAI")]
        [InlineData("minimax-image", "MiniMax")]
        [InlineData("replicate-flux", "Replicate")]
        public async Task Consume_DifferentProviders_ShouldRouteCorrectly(string model, string expectedProvider)
        {
            // Arrange
            var taskId = $"test-task-{model}";
            var request = CreateTestImageGenerationRequest(taskId, model: model);

            var modelMapping = new ModelProviderMapping
            {
                ModelAlias = model,
                ProviderName = expectedProvider,
                ProviderModelId = model,
                SupportsImageGeneration = true
            };

            var imageResponse = new ConduitLLM.Core.Models.ImageGenerationResponse
            {
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Data = new List<ConduitLLM.Core.Models.ImageData>
                {
                    new ConduitLLM.Core.Models.ImageData { Url = "https://example.com/image.png" }
                }
            };

            SetupBasicMocks(request, imageResponse, modelMapping);

            // Act
            await _orchestrator.Consume(_mockContext.Object);

            // Assert
            _mockMappingService.Verify(x => x.GetMappingByModelAliasAsync(model), Times.Once);
            _mockClientFactory.Verify(x => x.GetClient(model), Times.Once);

            _mockPublishEndpoint.Verify(x => x.Publish(
                It.Is<ImageGenerationCompleted>(c => c.Provider == expectedProvider && c.Model == model),
                It.IsAny<CancellationToken>()), Times.Once);

            _output.WriteLine($"Provider routing for {model} -> {expectedProvider} verified");
        }

        [Fact]
        public async Task Consume_ModelNotFound_ShouldFailWithAppropriateError()
        {
            // Arrange
            var taskId = "test-task-invalid-model";
            var request = CreateTestImageGenerationRequest(taskId, model: "invalid-model");

            _mockContext.Setup(x => x.Message).Returns(request);
            _mockContext.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

            _mockVirtualKeyService.Setup(x => x.ValidateVirtualKeyAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new ConduitLLM.Configuration.Entities.VirtualKey { Id = 1, IsEnabled = true });

            _mockMappingService.Setup(x => x.GetMappingByModelAliasAsync("invalid-model"))
                .ReturnsAsync((ModelProviderMapping?)null);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _orchestrator.Consume(_mockContext.Object));

            Assert.Contains("invalid-model", exception.Message);
            Assert.Contains("not found", exception.Message);

            _mockTaskService.Verify(x => x.UpdateTaskStatusAsync(
                taskId, TaskState.Failed, null, null, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);

            _output.WriteLine("Model not found error handling verified");
        }

        [Fact]
        public async Task Consume_ModelDoesNotSupportImageGeneration_ShouldFailWithError()
        {
            // Arrange
            var taskId = "test-task-unsupported";
            var request = CreateTestImageGenerationRequest(taskId, model: "gpt-4");

            var modelMapping = new ModelProviderMapping
            {
                ModelAlias = "gpt-4",
                ProviderName = "OpenAI",
                ProviderModelId = "gpt-4",
                SupportsImageGeneration = false // This model doesn't support image generation
            };

            _mockContext.Setup(x => x.Message).Returns(request);
            _mockContext.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

            _mockVirtualKeyService.Setup(x => x.ValidateVirtualKeyAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new ConduitLLM.Configuration.Entities.VirtualKey { Id = 1, IsEnabled = true });

            _mockMappingService.Setup(x => x.GetMappingByModelAliasAsync("gpt-4"))
                .ReturnsAsync(modelMapping);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _orchestrator.Consume(_mockContext.Object));

            Assert.Contains("gpt-4", exception.Message);
            Assert.Contains("not found", exception.Message);

            _output.WriteLine("Unsupported model error handling verified");
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public async Task Consume_ProviderFailure_ShouldHandleGracefully()
        {
            // Arrange
            var taskId = "test-task-provider-failure";
            var request = CreateTestImageGenerationRequest(taskId);

            SetupBasicMocksForFailure(request);

            _mockLLMClient.Setup(x => x.CreateImageAsync(It.IsAny<ConduitLLM.Core.Models.ImageGenerationRequest>(), null, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new HttpRequestException("Provider API error"));

            // Act & Assert
            var exception = await Assert.ThrowsAsync<HttpRequestException>(
                () => _orchestrator.Consume(_mockContext.Object));

            Assert.Equal("Provider API error", exception.Message);

            _mockTaskService.Verify(x => x.UpdateTaskStatusAsync(
                taskId, TaskState.Failed, null, null, "Provider API error", It.IsAny<CancellationToken>()), Times.Once);

            _mockPublishEndpoint.Verify(x => x.Publish(
                It.Is<ImageGenerationFailed>(f => f.Error == "Provider API error" && f.IsRetryable == false),
                It.IsAny<CancellationToken>()), Times.Once);

            _output.WriteLine("Provider failure error handling verified");
        }


        [Theory]
        [InlineData(typeof(TaskCanceledException), true)]
        [InlineData(typeof(TimeoutException), true)]
        [InlineData(typeof(ArgumentException), false)]
        [InlineData(typeof(UnauthorizedAccessException), false)]
        public async Task IsRetryableError_ShouldCorrectlyIdentifyRetryableErrors(Type exceptionType, bool expectedRetryable)
        {
            // Arrange
            var taskId = "test-task-retry-logic";
            var request = CreateTestImageGenerationRequest(taskId);

            SetupBasicMocksForFailure(request);

            var exception = (Exception)Activator.CreateInstance(exceptionType, "Test error")!;
            _mockLLMClient.Setup(x => x.CreateImageAsync(It.IsAny<ConduitLLM.Core.Models.ImageGenerationRequest>(), null, It.IsAny<CancellationToken>()))
                .ThrowsAsync(exception);

            // Act & Assert
            await Assert.ThrowsAsync(exceptionType, () => _orchestrator.Consume(_mockContext.Object));

            _mockPublishEndpoint.Verify(x => x.Publish(
                It.Is<ImageGenerationFailed>(f => f.IsRetryable == expectedRetryable),
                It.IsAny<CancellationToken>()), Times.Once);

            _output.WriteLine($"Retry logic for {exceptionType.Name} verified (retryable: {expectedRetryable})");
        }

        #endregion

        #region Spend Tracking and Event Publishing Tests

        [Fact]
        public async Task Consume_SuccessfulGeneration_ShouldTrackSpendCorrectly()
        {
            // Arrange
            var taskId = "test-task-spend";
            var request = CreateTestImageGenerationRequest(taskId, model: "dall-e-3", n: 2);

            var imageResponse = new ConduitLLM.Core.Models.ImageGenerationResponse
            {
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Data = new List<ConduitLLM.Core.Models.ImageData>
                {
                    new ConduitLLM.Core.Models.ImageData { Url = "https://example.com/image1.png" },
                    new ConduitLLM.Core.Models.ImageData { Url = "https://example.com/image2.png" }
                }
            };

            SetupBasicMocks(request, imageResponse);

            // Act
            await _orchestrator.Consume(_mockContext.Object);

            // Assert - DALL-E 3 should cost $0.040 per image, so 2 images = $0.080
            _mockPublishEndpoint.Verify(x => x.Publish(
                It.Is<SpendUpdateRequested>(s => s.Amount == 0.080m && s.KeyId == request.VirtualKeyId),
                It.IsAny<CancellationToken>()), Times.Once);

            _mockPublishEndpoint.Verify(x => x.Publish(
                It.Is<ImageGenerationCompleted>(c => c.Cost == 0.080m),
                It.IsAny<CancellationToken>()), Times.Once);

            _output.WriteLine("Spend tracking for DALL-E 3 (2 images = $0.080) verified");
        }

        [Theory]
        [InlineData("dall-e-2", "OpenAI", 1, 0.020)]
        [InlineData("minimax-image", "MiniMax", 3, 0.030)]
        [InlineData("replicate-flux", "Replicate", 1, 0.025)]
        public async Task Consume_DifferentProviders_ShouldCalculateCostCorrectly(string model, string provider, int imageCount, decimal expectedCost)
        {
            // Arrange
            var taskId = $"test-task-cost-{model}";
            var request = CreateTestImageGenerationRequest(taskId, model: model, n: imageCount);

            var imageData = Enumerable.Range(0, imageCount)
                .Select(i => new ConduitLLM.Core.Models.ImageData { Url = $"https://example.com/image{i}.png" })
                .ToList();

            var imageResponse = new ConduitLLM.Core.Models.ImageGenerationResponse 
            { 
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Data = imageData 
            };

            // Create model mapping with correct provider
            var modelMapping = new ModelProviderMapping
            {
                ModelAlias = model,
                ProviderName = provider,
                ProviderModelId = model,
                SupportsImageGeneration = true
            };

            SetupBasicMocks(request, imageResponse, modelMapping);

            // Act
            await _orchestrator.Consume(_mockContext.Object);

            // Assert
            _mockPublishEndpoint.Verify(x => x.Publish(
                It.Is<SpendUpdateRequested>(s => s.Amount == expectedCost),
                It.IsAny<CancellationToken>()), Times.Once);

            _output.WriteLine($"Cost calculation for {model} ({imageCount} images = ${expectedCost:F3}) verified");
        }

        [Fact]
        public async Task Consume_SuccessfulGeneration_ShouldPublishProgressEvents()
        {
            // Arrange
            var taskId = "test-task-progress";
            var request = CreateTestImageGenerationRequest(taskId, n: 2);

            var imageResponse = new ConduitLLM.Core.Models.ImageGenerationResponse
            {
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Data = new List<ConduitLLM.Core.Models.ImageData>
                {
                    new ConduitLLM.Core.Models.ImageData { Url = "https://example.com/image1.png" },
                    new ConduitLLM.Core.Models.ImageData { Url = "https://example.com/image2.png" }
                }
            };

            SetupBasicMocks(request, imageResponse);

            // Act
            await _orchestrator.Consume(_mockContext.Object);

            // Assert
            // Should publish initial progress (processing)
            _mockPublishEndpoint.Verify(x => x.Publish(
                It.Is<ImageGenerationProgress>(p => 
                    p.Status == "processing" && 
                    p.ImagesCompleted == 0 && 
                    p.TotalImages == 2),
                It.IsAny<CancellationToken>()), Times.Once);

            // Should publish progress for each image being stored
            _mockPublishEndpoint.Verify(x => x.Publish(
                It.Is<ImageGenerationProgress>(p => p.Status == "storing"),
                It.IsAny<CancellationToken>()), Times.AtLeast(2));

            _output.WriteLine("Progress event publishing verified");
        }

        #endregion

        #region Media Storage Tests

        [Fact]
        public async Task Consume_Base64Response_ShouldStoreImageCorrectly()
        {
            // Arrange
            var taskId = "test-task-b64";
            var request = CreateTestImageGenerationRequest(taskId, responseFormat: "b64_json");

            var base64Data = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("fake-image-data"));
            var imageResponse = new ConduitLLM.Core.Models.ImageGenerationResponse
            {
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Data = new List<ConduitLLM.Core.Models.ImageData>
                {
                    new ConduitLLM.Core.Models.ImageData { B64Json = base64Data }
                }
            };

            var storageResult = new MediaStorageResult
            {
                Url = "https://cdn.example.com/stored-image.png",
                StorageKey = "images/stored-image.png"
            };

            SetupBasicMocks(request, imageResponse);
            _mockStorageService.Setup(x => x.StoreAsync(It.IsAny<System.IO.Stream>(), It.IsAny<MediaMetadata>()))
                .ReturnsAsync(storageResult);

            // Act
            await _orchestrator.Consume(_mockContext.Object);

            // Assert
            _mockStorageService.Verify(x => x.StoreAsync(
                It.IsAny<System.IO.Stream>(),
                It.Is<MediaMetadata>(m => 
                    m.ContentType == "image/png" && 
                    m.MediaType == MediaType.Image)),
                Times.Once);

            _mockPublishEndpoint.Verify(x => x.Publish(
                It.Is<MediaGenerationCompleted>(m => 
                    m.MediaUrl == storageResult.Url && 
                    m.StorageKey == storageResult.StorageKey),
                It.IsAny<CancellationToken>()), Times.Once);

            _output.WriteLine("Base64 image storage verified");
        }

        #endregion

        #region Helper Methods

        private ImageGenerationRequested CreateTestImageGenerationRequest(
            string taskId, 
            string prompt = "Test prompt", 
            string model = "dall-e-3", 
            int n = 1, 
            string size = "1024x1024", 
            string quality = "standard", 
            string responseFormat = "url")
        {
            return new ImageGenerationRequested
            {
                TaskId = taskId,
                VirtualKeyId = 1,
                VirtualKeyHash = "test-key-hash",
                CorrelationId = Guid.NewGuid().ToString(),
                Request = new ConduitLLM.Core.Events.ImageGenerationRequest
                {
                    Prompt = prompt,
                    Model = model,
                    N = n,
                    Size = size,
                    Quality = quality,
                    ResponseFormat = responseFormat
                }
            };
        }

        private void SetupBasicMocks(
            ImageGenerationRequested request, 
            ConduitLLM.Core.Models.ImageGenerationResponse imageResponse,
            ModelProviderMapping? modelMapping = null)
        {
            _mockContext.Setup(x => x.Message).Returns(request);
            _mockContext.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

            var virtualKey = new ConduitLLM.Configuration.Entities.VirtualKey
            {
                Id = request.VirtualKeyId,
                KeyHash = request.VirtualKeyHash,
                IsEnabled = true
            };

            _mockVirtualKeyService.Setup(x => x.ValidateVirtualKeyAsync(request.VirtualKeyHash, request.Request.Model))
                .ReturnsAsync(virtualKey);

            var mapping = modelMapping ?? new ModelProviderMapping
            {
                ModelAlias = request.Request.Model,
                ProviderName = "OpenAI",
                ProviderModelId = request.Request.Model,
                SupportsImageGeneration = true
            };

            _mockMappingService.Setup(x => x.GetMappingByModelAliasAsync(request.Request.Model))
                .ReturnsAsync(mapping);

            _mockClientFactory.Setup(x => x.GetClient(request.Request.Model))
                .Returns(_mockLLMClient.Object);

            _mockLLMClient.Setup(x => x.CreateImageAsync(It.IsAny<ConduitLLM.Core.Models.ImageGenerationRequest>(), null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(imageResponse);
        }

        private void SetupBasicMocksForFailure(ImageGenerationRequested request)
        {
            _mockContext.Setup(x => x.Message).Returns(request);
            _mockContext.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

            var virtualKey = new ConduitLLM.Configuration.Entities.VirtualKey
            {
                Id = request.VirtualKeyId,
                KeyHash = request.VirtualKeyHash,
                IsEnabled = true
            };

            _mockVirtualKeyService.Setup(x => x.ValidateVirtualKeyAsync(request.VirtualKeyHash, request.Request.Model))
                .ReturnsAsync(virtualKey);

            var mapping = new ModelProviderMapping
            {
                ModelAlias = request.Request.Model,
                ProviderName = "OpenAI", 
                ProviderModelId = request.Request.Model,
                SupportsImageGeneration = true
            };

            _mockMappingService.Setup(x => x.GetMappingByModelAliasAsync(request.Request.Model))
                .ReturnsAsync(mapping);

            _mockClientFactory.Setup(x => x.GetClient(request.Request.Model))
                .Returns(_mockLLMClient.Object);
        }

        #endregion
    }
}