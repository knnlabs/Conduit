using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;
using MassTransit;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Services
{
    /// <summary>
    /// Unit tests for VideoGenerationService to ensure comprehensive coverage of video generation functionality.
    /// </summary>
    public class VideoGenerationServiceTests
    {
        private readonly Mock<ILLMClientFactory> _mockClientFactory;
        private readonly Mock<IModelCapabilityService> _mockCapabilityService;
        private readonly Mock<ICostCalculationService> _mockCostService;
        private readonly Mock<IVirtualKeyService> _mockVirtualKeyService;
        private readonly Mock<IMediaStorageService> _mockMediaStorage;
        private readonly Mock<IAsyncTaskService> _mockTaskService;
        private readonly Mock<IPublishEndpoint> _mockPublishEndpoint;
        private readonly Mock<ILogger<VideoGenerationService>> _mockLogger;
        private readonly VideoGenerationService _service;

        public VideoGenerationServiceTests()
        {
            _mockClientFactory = new Mock<ILLMClientFactory>();
            _mockCapabilityService = new Mock<IModelCapabilityService>();
            _mockCostService = new Mock<ICostCalculationService>();
            _mockVirtualKeyService = new Mock<IVirtualKeyService>();
            _mockMediaStorage = new Mock<IMediaStorageService>();
            _mockTaskService = new Mock<IAsyncTaskService>();
            _mockPublishEndpoint = new Mock<IPublishEndpoint>();
            _mockLogger = new Mock<ILogger<VideoGenerationService>>();

            _service = new VideoGenerationService(
                _mockClientFactory.Object,
                _mockCapabilityService.Object,
                _mockCostService.Object,
                _mockVirtualKeyService.Object,
                _mockMediaStorage.Object,
                _mockTaskService.Object,
                _mockLogger.Object,
                _mockPublishEndpoint.Object);
        }

        [Fact]
        public void Constructor_WithAllDependencies_InitializesSuccessfully()
        {
            // Act & Assert - service should be created without throwing
            Assert.NotNull(_service);
        }

        [Fact]
        public void Constructor_WithNullClientFactory_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new VideoGenerationService(
                null, _mockCapabilityService.Object, _mockCostService.Object,
                _mockVirtualKeyService.Object, _mockMediaStorage.Object, _mockTaskService.Object,
                _mockLogger.Object, _mockPublishEndpoint.Object));
        }

        [Fact]
        public void Constructor_WithNullCapabilityService_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new VideoGenerationService(
                _mockClientFactory.Object, null, _mockCostService.Object,
                _mockVirtualKeyService.Object, _mockMediaStorage.Object, _mockTaskService.Object,
                _mockLogger.Object, _mockPublishEndpoint.Object));
        }

        [Fact]
        public void Constructor_WithNullCostService_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new VideoGenerationService(
                _mockClientFactory.Object, _mockCapabilityService.Object, null,
                _mockVirtualKeyService.Object, _mockMediaStorage.Object, _mockTaskService.Object,
                _mockLogger.Object, _mockPublishEndpoint.Object));
        }

        [Fact]
        public void Constructor_WithNullVirtualKeyService_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new VideoGenerationService(
                _mockClientFactory.Object, _mockCapabilityService.Object, _mockCostService.Object,
                null, _mockMediaStorage.Object, _mockTaskService.Object,
                _mockLogger.Object, _mockPublishEndpoint.Object));
        }

        [Fact]
        public void Constructor_WithNullMediaStorage_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new VideoGenerationService(
                _mockClientFactory.Object, _mockCapabilityService.Object, _mockCostService.Object,
                _mockVirtualKeyService.Object, null, _mockTaskService.Object,
                _mockLogger.Object, _mockPublishEndpoint.Object));
        }

        [Fact]
        public void Constructor_WithNullTaskService_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new VideoGenerationService(
                _mockClientFactory.Object, _mockCapabilityService.Object, _mockCostService.Object,
                _mockVirtualKeyService.Object, _mockMediaStorage.Object, null,
                _mockLogger.Object, _mockPublishEndpoint.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new VideoGenerationService(
                _mockClientFactory.Object, _mockCapabilityService.Object, _mockCostService.Object,
                _mockVirtualKeyService.Object, _mockMediaStorage.Object, _mockTaskService.Object,
                null, _mockPublishEndpoint.Object));
        }

        [Fact]
        public async Task ValidateRequestAsync_WithValidRequest_ReturnsTrue()
        {
            // Arrange
            var request = new VideoGenerationRequest
            {
                Model = "test-model",
                Prompt = "Generate a video of a sunset",
                Duration = 10,
                Fps = 30
            };

            _mockCapabilityService.Setup(x => x.SupportsVideoGenerationAsync("test-model"))
                .ReturnsAsync(true);

            // Act
            var result = await _service.ValidateRequestAsync(request);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task ValidateRequestAsync_WithEmptyPrompt_ReturnsFalse()
        {
            // Arrange
            var request = new VideoGenerationRequest
            {
                Model = "test-model",
                Prompt = "",
                Duration = 10
            };

            // Act
            var result = await _service.ValidateRequestAsync(request);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task ValidateRequestAsync_WithNullPrompt_ReturnsFalse()
        {
            // Arrange
            var request = new VideoGenerationRequest
            {
                Model = "test-model",
                Prompt = null,
                Duration = 10
            };

            // Act
            var result = await _service.ValidateRequestAsync(request);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task ValidateRequestAsync_WithEmptyModel_ReturnsFalse()
        {
            // Arrange
            var request = new VideoGenerationRequest
            {
                Model = "",
                Prompt = "Generate a video",
                Duration = 10
            };

            // Act
            var result = await _service.ValidateRequestAsync(request);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task ValidateRequestAsync_WithUnsupportedModel_ReturnsFalse()
        {
            // Arrange
            var request = new VideoGenerationRequest
            {
                Model = "text-only-model",
                Prompt = "Generate a video",
                Duration = 10
            };

            _mockCapabilityService.Setup(x => x.SupportsVideoGenerationAsync("text-only-model"))
                .ReturnsAsync(false);

            // Act
            var result = await _service.ValidateRequestAsync(request);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task ValidateRequestAsync_WithInvalidDurationTooLow_ReturnsFalse()
        {
            // Arrange
            var request = new VideoGenerationRequest
            {
                Model = "test-model",
                Prompt = "Generate a video",
                Duration = 0
            };

            _mockCapabilityService.Setup(x => x.SupportsVideoGenerationAsync("test-model"))
                .ReturnsAsync(true);

            // Act
            var result = await _service.ValidateRequestAsync(request);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task ValidateRequestAsync_WithInvalidDurationTooHigh_ReturnsFalse()
        {
            // Arrange
            var request = new VideoGenerationRequest
            {
                Model = "test-model",
                Prompt = "Generate a video",
                Duration = 61
            };

            _mockCapabilityService.Setup(x => x.SupportsVideoGenerationAsync("test-model"))
                .ReturnsAsync(true);

            // Act
            var result = await _service.ValidateRequestAsync(request);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task ValidateRequestAsync_WithInvalidFpsTooLow_ReturnsFalse()
        {
            // Arrange
            var request = new VideoGenerationRequest
            {
                Model = "test-model",
                Prompt = "Generate a video",
                Duration = 10,
                Fps = 0
            };

            _mockCapabilityService.Setup(x => x.SupportsVideoGenerationAsync("test-model"))
                .ReturnsAsync(true);

            // Act
            var result = await _service.ValidateRequestAsync(request);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task ValidateRequestAsync_WithInvalidFpsTooHigh_ReturnsFalse()
        {
            // Arrange
            var request = new VideoGenerationRequest
            {
                Model = "test-model",
                Prompt = "Generate a video",
                Duration = 10,
                Fps = 121
            };

            _mockCapabilityService.Setup(x => x.SupportsVideoGenerationAsync("test-model"))
                .ReturnsAsync(true);

            // Act
            var result = await _service.ValidateRequestAsync(request);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task EstimateCostAsync_WithValidRequest_ReturnsCost()
        {
            // Arrange
            var request = new VideoGenerationRequest
            {
                Model = "test-model",
                Prompt = "Generate a video",
                Duration = 10,
                Size = "1280x720"
            };

            var expectedCost = 0.05m;
            _mockCostService.Setup(x => x.CalculateCostAsync(
                "test-model",
                It.Is<Usage>(u => u.VideoDurationSeconds == 10 && u.VideoResolution == "1280x720"),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedCost);

            // Act
            var result = await _service.EstimateCostAsync(request);

            // Assert
            Assert.Equal(expectedCost, result);
        }

        [Fact]
        public async Task EstimateCostAsync_WithNullDuration_UsesDefault()
        {
            // Arrange
            var request = new VideoGenerationRequest
            {
                Model = "test-model",
                Prompt = "Generate a video",
                Duration = null,
                Size = "1280x720"
            };

            var expectedCost = 0.03m;
            _mockCostService.Setup(x => x.CalculateCostAsync(
                "test-model",
                It.Is<Usage>(u => u.VideoDurationSeconds == 6), // Default 6 seconds
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedCost);

            // Act
            var result = await _service.EstimateCostAsync(request);

            // Assert
            Assert.Equal(expectedCost, result);
        }

        [Fact]
        public async Task GenerateVideoAsync_WithInvalidRequest_ThrowsArgumentException()
        {
            // Arrange
            var request = new VideoGenerationRequest
            {
                Model = "test-model",
                Prompt = "", // Invalid empty prompt
                Duration = 10
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _service.GenerateVideoAsync(request, "sk-test-key"));
        }

        [Fact]
        public async Task GenerateVideoAsync_WithInvalidVirtualKey_ThrowsUnauthorizedAccessException()
        {
            // Arrange
            var request = new VideoGenerationRequest
            {
                Model = "test-model",
                Prompt = "Generate a video",
                Duration = 10
            };

            _mockCapabilityService.Setup(x => x.SupportsVideoGenerationAsync("test-model"))
                .ReturnsAsync(true);

            _mockVirtualKeyService.Setup(x => x.ValidateVirtualKeyAsync("invalid-key", "test-model"))
                .ReturnsAsync((VirtualKey)null);

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                _service.GenerateVideoAsync(request, "invalid-key"));
        }

        [Fact]
        public async Task GenerateVideoAsync_WithDisabledVirtualKey_ThrowsUnauthorizedAccessException()
        {
            // Arrange
            var request = new VideoGenerationRequest
            {
                Model = "test-model",
                Prompt = "Generate a video",
                Duration = 10
            };

            var disabledKey = new VirtualKey
            {
                Id = 1,
                IsEnabled = false
            };

            _mockCapabilityService.Setup(x => x.SupportsVideoGenerationAsync("test-model"))
                .ReturnsAsync(true);

            _mockVirtualKeyService.Setup(x => x.ValidateVirtualKeyAsync("sk-disabled", "test-model"))
                .ReturnsAsync(disabledKey);

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                _service.GenerateVideoAsync(request, "sk-disabled"));
        }

        [Fact]
        public async Task GenerateVideoAsync_WithNoProviderAvailable_ThrowsNotSupportedException()
        {
            // Arrange
            var request = new VideoGenerationRequest
            {
                Model = "unsupported-model",
                Prompt = "Generate a video",
                Duration = 10
            };

            var validKey = new VirtualKey
            {
                Id = 1,
                IsEnabled = true
            };

            _mockCapabilityService.Setup(x => x.SupportsVideoGenerationAsync("unsupported-model"))
                .ReturnsAsync(true);

            _mockVirtualKeyService.Setup(x => x.ValidateVirtualKeyAsync("sk-test", "unsupported-model"))
                .ReturnsAsync(validKey);

            _mockClientFactory.Setup(x => x.GetClient("unsupported-model"))
                .Returns((ILLMClient)null);

            // Act & Assert
            await Assert.ThrowsAsync<NotSupportedException>(() =>
                _service.GenerateVideoAsync(request, "sk-test"));
        }

        [Fact]
        public async Task GenerateVideoAsync_WithValidRequestAndClient_GeneratesVideoSuccessfully()
        {
            // Arrange
            var request = new VideoGenerationRequest
            {
                Model = "test-model",
                Prompt = "Generate a video",
                Duration = 10,
                Size = "1280x720"
            };

            var validKey = new VirtualKey
            {
                Id = 1,
                IsEnabled = true
            };

            var videoData = new VideoData
            {
                B64Json = Convert.ToBase64String(new byte[] { 1, 2, 3, 4, 5 }),
                Metadata = new VideoMetadata
                {
                    Width = 1280,
                    Height = 720,
                    Duration = 10,
                    Fps = 30,
                    MimeType = "video/mp4"
                }
            };

            var expectedResponse = new VideoGenerationResponse
            {
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Data = new List<VideoData> { videoData },
                Model = "test-model"
            };

            var mockClient = new Mock<ILLMClient>();
            var storageResult = new MediaStorageResult
            {
                Url = "https://cdn.example.com/video123.mp4",
                StorageKey = "video123"
            };

            _mockCapabilityService.Setup(x => x.SupportsVideoGenerationAsync("test-model"))
                .ReturnsAsync(true);

            _mockVirtualKeyService.Setup(x => x.ValidateVirtualKeyAsync("sk-test", "test-model"))
                .ReturnsAsync(validKey);

            _mockClientFactory.Setup(x => x.GetClient("test-model"))
                .Returns(mockClient.Object);

            _mockCostService.Setup(x => x.CalculateCostAsync(It.IsAny<string>(), It.IsAny<Usage>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(0.05m);

            _mockMediaStorage.Setup(x => x.StoreVideoAsync(It.IsAny<Stream>(), It.IsAny<VideoMediaMetadata>(), It.IsAny<Action<long>?>()))
                .ReturnsAsync(storageResult);

            // Since we can't easily mock reflection calls, let's create a test client that implements the method
            var testClient = new TestVideoClient(expectedResponse);
            _mockClientFactory.Setup(x => x.GetClient("test-model"))
                .Returns(testClient);

            // Act
            var result = await _service.GenerateVideoAsync(request, "sk-test");

            // Assert
            Assert.NotNull(result);
            Assert.Equal("test-model", result.Model);
            Assert.NotNull(result.Data);
            Assert.Single(result.Data);
            Assert.Equal("https://cdn.example.com/video123.mp4", result.Data.First().Url);
            Assert.Null(result.Data.First().B64Json); // Should be cleared after storage

            // Verify spend was updated
            _mockVirtualKeyService.Verify(x => x.UpdateSpendAsync(1, 0.05m), Times.Once);

            // Verify events were published
            _mockPublishEndpoint.Verify(x => x.Publish(
                It.IsAny<VideoGenerationRequested>(), It.IsAny<CancellationToken>()), Times.Once);
            _mockPublishEndpoint.Verify(x => x.Publish(
                It.IsAny<VideoGenerationCompleted>(), It.IsAny<CancellationToken>()), Times.Once);
            _mockPublishEndpoint.Verify(x => x.Publish(
                It.IsAny<MediaGenerationCompleted>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GenerateVideoAsync_WhenExceptionThrown_PublishesFailedEventAndRethrows()
        {
            // Arrange
            var request = new VideoGenerationRequest
            {
                Model = "test-model",
                Prompt = "Generate a video",
                Duration = 10
            };

            var validKey = new VirtualKey
            {
                Id = 1,
                IsEnabled = true
            };

            _mockCapabilityService.Setup(x => x.SupportsVideoGenerationAsync("test-model"))
                .ReturnsAsync(true);

            _mockVirtualKeyService.Setup(x => x.ValidateVirtualKeyAsync("sk-test", "test-model"))
                .ReturnsAsync(validKey);

            var mockClient = new TestVideoClient(null);
            mockClient.ShouldThrow = true;
            _mockClientFactory.Setup(x => x.GetClient("test-model"))
                .Returns(mockClient);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<TargetInvocationException>(() =>
                _service.GenerateVideoAsync(request, "sk-test"));

            Assert.IsType<InvalidOperationException>(exception.InnerException);
            Assert.Equal("Test exception", exception.InnerException?.Message);

            // Verify failed event was published
            _mockPublishEndpoint.Verify(x => x.Publish(
                It.IsAny<VideoGenerationFailed>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GenerateVideoWithTaskAsync_WithValidRequest_CreatesTaskAndReturnsResponse()
        {
            // Arrange
            var request = new VideoGenerationRequest
            {
                Model = "test-model",
                Prompt = "Generate a video",
                Duration = 10,
                WebhookUrl = "https://example.com/webhook"
            };

            var validKey = new VirtualKey
            {
                Id = 1,
                IsEnabled = true
            };

            var taskId = "task-123";

            _mockCapabilityService.Setup(x => x.SupportsVideoGenerationAsync("test-model"))
                .ReturnsAsync(true);

            _mockVirtualKeyService.Setup(x => x.ValidateVirtualKeyAsync("sk-test", "test-model"))
                .ReturnsAsync(validKey);

            _mockTaskService.Setup(x => x.CreateTaskAsync("video_generation", 1, It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(taskId);

            // Act
            var result = await _service.GenerateVideoWithTaskAsync(request, "sk-test");

            // Assert
            Assert.NotNull(result);
            Assert.Equal("test-model", result.Model);
            Assert.NotNull(result.Data);
            Assert.Single(result.Data);
            Assert.Equal($"pending:{taskId}", result.Data.First().Url);

            // Verify task was created
            _mockTaskService.Verify(x => x.CreateTaskAsync("video_generation", 1, It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);

            // Verify async event was published
            _mockPublishEndpoint.Verify(x => x.Publish(
                It.Is<VideoGenerationRequested>(e => e.IsAsync == true && e.RequestId == taskId), 
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetVideoGenerationStatusAsync_WithNonExistentTask_ThrowsInvalidOperationException()
        {
            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.GetVideoGenerationStatusAsync("task-123", "sk-test"));
            
            Assert.Contains("Task task-123 not found", exception.Message);
        }

        [Fact]
        public async Task CancelVideoGenerationAsync_PublishesCancellationEventAndReturnsTrue()
        {
            // Act
            var result = await _service.CancelVideoGenerationAsync("task-123", "sk-test");

            // Assert
            Assert.True(result);

            // Verify cancellation event was published
            _mockPublishEndpoint.Verify(x => x.Publish(
                It.Is<VideoGenerationCancelled>(e => e.RequestId == "task-123"), 
                It.IsAny<CancellationToken>()), Times.Once);
        }

        // Test helper class that implements video generation method
        private class TestVideoClient : ILLMClient
        {
            private readonly VideoGenerationResponse _response;
            public bool ShouldThrow { get; set; }

            public TestVideoClient(VideoGenerationResponse response)
            {
                _response = response;
            }

            public Task<VideoGenerationResponse> CreateVideoAsync(VideoGenerationRequest request, string apiKey, CancellationToken cancellationToken = default)
            {
                if (ShouldThrow)
                {
                    throw new InvalidOperationException("Test exception");
                }
                return Task.FromResult(_response);
            }

            // Required ILLMClient methods - not implemented for this test
            public Task<ChatCompletionResponse> CreateChatCompletionAsync(ChatCompletionRequest request, string apiKey = null, CancellationToken cancellationToken = default)
                => throw new NotImplementedException();

            public IAsyncEnumerable<ChatCompletionChunk> StreamChatCompletionAsync(ChatCompletionRequest request, string apiKey = null, CancellationToken cancellationToken = default)
                => throw new NotImplementedException();

            public Task<List<string>> ListModelsAsync(string apiKey = null, CancellationToken cancellationToken = default)
                => throw new NotImplementedException();

            public Task<EmbeddingResponse> CreateEmbeddingAsync(EmbeddingRequest request, string apiKey = null, CancellationToken cancellationToken = default)
                => throw new NotImplementedException();

            public Task<ImageGenerationResponse> CreateImageAsync(ConduitLLM.Core.Models.ImageGenerationRequest request, string apiKey = null, CancellationToken cancellationToken = default)
                => throw new NotImplementedException();

            public string ProviderName => "Test";
        }
    }
}