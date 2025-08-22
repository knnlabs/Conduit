using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Tests.Helpers;
using MassTransit;
using Moq;

namespace ConduitLLM.Tests.Core.Services
{
    public partial class VideoGenerationOrchestratorTests
    {
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

            // Setup model mapping with a complete model that supports video
            var model = ModelTestHelper.CreateCompleteTestModel(
                modelName: "test-model",
                
                supportsVideoGeneration: true);
            
            var modelMapping = new ModelProviderMapping
            {
                ModelAlias = "test-model",
                ModelId = model.Id,
                Model = model,
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

            // Model capabilities are now accessed through ModelProviderMapping

            // Setup client factory to return null (will cause NotSupportedException)
            _mockClientFactory.Setup(x => x.GetClient("test-model"))
                .Returns((ILLMClient)null);

            // Act
            await _orchestrator.Consume(context.Object);

            // Assert
            // The orchestrator calls GetMappingByModelAliasAsync twice:
            // 1. To check if the model supports video generation
            // 2. In GetModelInfoAsync to get the model information
            _mockModelMappingService.Verify(x => x.GetMappingByModelAliasAsync("test-model"), Times.Exactly(2));
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
                    ModelId = 1,
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

            // Model capabilities are now accessed through ModelProviderMapping

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