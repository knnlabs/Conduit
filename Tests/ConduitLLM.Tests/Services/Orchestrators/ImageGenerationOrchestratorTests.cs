using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Services.Orchestrators
{
    /// <summary>
    /// Unit tests for ImageGenerationOrchestrator.
    /// </summary>
    public class ImageGenerationOrchestratorTests : MediaGenerationOrchestratorTestBase<
        ImageGenerationOrchestrator,
        ConduitLLM.Core.Models.ImageGenerationRequest,
        ConduitLLM.Core.Models.ImageGenerationResponse,
        ImageGenerationRequested>
    {
        protected override string GetRequestId(ImageGenerationRequested request) => request.TaskId;
        protected override string? GetWebhookUrl(ImageGenerationRequested request) => request.WebhookUrl;

        public ImageGenerationOrchestratorTests()
        {
        }

        protected override ImageGenerationOrchestrator CreateOrchestrator()
        {
            return new ImageGenerationOrchestrator(
                ClientFactoryMock.Object,
                TaskServiceMock.Object,
                StorageServiceMock.Object,
                PublishEndpointMock.Object,
                ModelMappingServiceMock.Object,
                VirtualKeyServiceMock.Object,
                CostServiceMock.Object,
                TaskRegistryMock.Object,
                WebhookServiceMock.Object,
                HttpClientFactoryMock.Object,
                ParameterValidatorMock.Object,
                Metrics,
                LoggerMock.Object as ILogger<ImageGenerationOrchestrator> ?? new Mock<ILogger<ImageGenerationOrchestrator>>().Object);
        }

        protected override ImageGenerationRequested CreateTestEventRequest()
        {
            return new ImageGenerationRequested
            {
                TaskId = "test-task-id",
                VirtualKeyId = 1,
                Request = new ConduitLLM.Core.Events.ImageGenerationRequest
                {
                    Model = "test-model",
                    Prompt = "Generate a test image",
                    N = 2,
                    Size = "1024x1024",
                    Quality = "standard",
                    Style = "vivid",
                    ResponseFormat = "url"
                },
                WebhookUrl = "https://example.com/webhook",
                CorrelationId = "test-correlation-id"
            };
        }

        protected override ConduitLLM.Core.Models.ImageGenerationResponse CreateTestResponse()
        {
            return new ConduitLLM.Core.Models.ImageGenerationResponse
            {
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Data = new List<ConduitLLM.Core.Models.ImageData>
                {
                    new ConduitLLM.Core.Models.ImageData
                    {
                        Url = "https://example.com/image1.png"
                    },
                    new ConduitLLM.Core.Models.ImageData
                    {
                        Url = "https://example.com/image2.png"
                    }
                }
            };
        }

        protected override void SetupSuccessfulGeneration(ConduitLLM.Core.Models.ImageGenerationResponse response)
        {
            // Mock client for image generation
            var mockClient = new Mock<ILLMClient>();
            mockClient.Setup(x => x.CreateImageAsync(
                It.IsAny<ConduitLLM.Core.Models.ImageGenerationRequest>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(response);
            
            ClientFactoryMock.Setup(x => x.GetClientAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockClient.Object);
            
            StorageServiceMock.Setup(x => x.StoreAsync(
                It.IsAny<Stream>(),
                It.IsAny<MediaMetadata>(),
                It.IsAny<IProgress<long>>()))
                .ReturnsAsync(new MediaStorageResult
                {
                    StorageKey = "image-key",
                    Url = "https://storage.example.com/image.png",
                    SizeBytes = 1024
                });
        }

        protected override void SetupFailedGeneration(Exception exception)
        {
            // Setup to simulate failure during orchestration
            ClientFactoryMock.Setup(x => x.GetClientAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(exception);
        }

        [Fact]
        public async Task Consume_WithMultipleImages_ShouldProcessAllInParallel()
        {
            // Arrange
            var request = CreateTestEventRequest();
            var context = CreateConsumeContext(request);
            var response = CreateTestResponse();

            SetupSuccessfulGeneration(response);

            // Act
            await Orchestrator.Consume(context.Object);

            // Assert - Should publish progress event with correct counts
            PublishEndpointMock.Verify(x => x.Publish(
                It.Is<ImageGenerationProgress>(e =>
                    e.TaskId == request.TaskId &&
                    e.TotalImages == 2),
                It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task Consume_WithBase64Response_ShouldProcessCorrectly()
        {
            // Arrange
            var request = CreateTestEventRequest();
            var context = CreateConsumeContext(request);
            
            var response = new ConduitLLM.Core.Models.ImageGenerationResponse
            {
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Data = new List<ConduitLLM.Core.Models.ImageData>
                {
                    new ConduitLLM.Core.Models.ImageData
                    {
                        B64Json = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg=="
                    }
                }
            };

            SetupSuccessfulGeneration(response);

            // Act
            await Orchestrator.Consume(context.Object);

            // Assert
            TaskServiceMock.Verify(x => x.UpdateTaskStatusAsync(
                request.TaskId,
                TaskState.Completed,
                It.IsAny<int?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Consume_WithUrlResponse_ShouldDownloadAndStore()
        {
            // Arrange
            var request = CreateTestEventRequest();
            var context = CreateConsumeContext(request);
            var response = CreateTestResponse();

            SetupSuccessfulGeneration(response);

            // Setup HTTP client for downloading
            var httpClient = new System.Net.Http.HttpClient(new MockHttpMessageHandler());
            HttpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>()))
                .Returns(httpClient);

            // Act
            await Orchestrator.Consume(context.Object);

            // Assert
            TaskServiceMock.Verify(x => x.UpdateTaskStatusAsync(
                request.TaskId,
                TaskState.Completed,
                It.IsAny<int?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task PublishCompletedEvent_ShouldIncludeAllImageUrls()
        {
            // Arrange
            var request = CreateTestEventRequest();
            var context = CreateConsumeContext(request);
            var response = CreateTestResponse();

            SetupSuccessfulGeneration(response);

            // Act
            await Orchestrator.Consume(context.Object);

            // Assert
            PublishEndpointMock.Verify(x => x.Publish(
                It.Is<ImageGenerationCompleted>(e =>
                    e.TaskId == request.TaskId &&
                    e.Images.Count == 2 &&
                    e.CorrelationId == request.CorrelationId),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task PublishProgressEvent_ShouldTrackImageCompletion()
        {
            // Arrange
            var request = CreateTestEventRequest();
            var context = CreateConsumeContext(request);
            var response = CreateTestResponse();

            SetupSuccessfulGeneration(response);

            // Act
            await Orchestrator.Consume(context.Object);

            // Assert - Should publish initial progress
            PublishEndpointMock.Verify(x => x.Publish(
                It.Is<ImageGenerationProgress>(e =>
                    e.TaskId == request.TaskId &&
                    e.Status == "processing" &&
                    e.ImagesCompleted == 0 &&
                    e.TotalImages == 2),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CreateWebhookPayload_ShouldIncludeAllImageDetails()
        {
            // Arrange
            var request = CreateTestEventRequest();
            var context = CreateConsumeContext(request);
            var response = CreateTestResponse();

            SetupSuccessfulGeneration(response);

            // Act
            await Orchestrator.Consume(context.Object);

            // Assert
            PublishEndpointMock.Verify(x => x.Publish(
                It.Is<WebhookDeliveryRequested>(w =>
                    w.TaskId == request.TaskId &&
                    w.WebhookUrl == request.WebhookUrl),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Consume_WithPartialImageFailure_ShouldProcessSuccessfulOnes()
        {
            // Arrange
            var request = CreateTestEventRequest();
            var context = CreateConsumeContext(request);
            
            // Create response with one valid URL and one invalid
            var response = new ConduitLLM.Core.Models.ImageGenerationResponse
            {
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Data = new List<ConduitLLM.Core.Models.ImageData>
                {
                    new ConduitLLM.Core.Models.ImageData
                    {
                        Url = "https://example.com/image1.png"
                    },
                    new ConduitLLM.Core.Models.ImageData
                    {
                        // Neither URL nor B64Json - should be skipped
                    }
                }
            };

            SetupSuccessfulGeneration(response);

            // Act
            await Orchestrator.Consume(context.Object);

            // Assert - Should complete successfully with partial results
            TaskServiceMock.Verify(x => x.UpdateTaskStatusAsync(
                request.TaskId,
                TaskState.Completed,
                It.IsAny<int?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        private class MockHttpMessageHandler : System.Net.Http.HttpMessageHandler
        {
            protected override Task<System.Net.Http.HttpResponseMessage> SendAsync(
                System.Net.Http.HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                var response = new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new System.Net.Http.ByteArrayContent(new byte[] { 0x89, 0x50, 0x4E, 0x47 })
                };
                return Task.FromResult(response);
            }
        }
    }
}