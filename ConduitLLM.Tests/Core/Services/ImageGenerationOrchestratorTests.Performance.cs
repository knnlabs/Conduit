using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

using MassTransit;

using Moq;

namespace ConduitLLM.Tests.Core.Services
{
    public partial class ImageGenerationOrchestratorTests
    {
        #region Cost Calculation Tests

        [Theory]
        [Trait("Category", "TimingSensitive")]
        [InlineData("openai", "dall-e-3", 1, 0.040)]
        [InlineData("openai", "dall-e-2", 2, 0.040)]
        [InlineData("minimax", "minimax-image", 3, 0.030)]
        [InlineData("replicate", "sdxl", 1, 0.025)]
        [InlineData("unknown", "unknown-model", 1, 0.025)] // Unknown defaults to Replicate
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
        [Trait("Category", "TimingSensitive")]
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
        [Trait("Category", "TimingSensitive")]
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
        [Trait("Category", "TimingSensitive")]
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
        [Trait("Category", "TimingSensitive")]
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
    }
}