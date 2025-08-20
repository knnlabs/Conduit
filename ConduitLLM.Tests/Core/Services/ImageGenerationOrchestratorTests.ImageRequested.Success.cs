using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
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
    public partial class ImageGenerationOrchestratorTests
    {
        #region ImageGenerationRequested Success Tests

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

            // Setup model with image generation capabilities
            var modelEntity = new Model
            {
                Id = 1,
                Name = "dall-e-3",
                ModelSeriesId = 1,
                ModelCapabilitiesId = 1,
                Capabilities = new ConduitLLM.Configuration.Entities.ModelCapabilities
                {
                    Id = 1,
                    SupportsImageGeneration = true,
                    MaxTokens = 4000,
                    TokenizerType = TokenizerType.Cl100KBase
                }
            };

            // Setup model mapping
            var modelMapping = new ModelProviderMapping
            {
                ModelAlias = "dall-e-3",
                ModelId = 1,
                Model = modelEntity,
                ProviderId = 1,
                ProviderModelId = "dall-e-3",
                Provider = new Provider { ProviderType = ProviderType.OpenAI }
            };

            _mockModelMappingService.Setup(x => x.GetMappingByModelAliasAsync("dall-e-3"))
                .Returns(Task.FromResult(modelMapping));

            // Setup provider service to return the provider
            var providerEntity = new Provider 
            { 
                Id = 1,
                ProviderType = ProviderType.OpenAI
            };

            _mockProviderService.Setup(x => x.GetProviderByIdAsync(1))
                .ReturnsAsync(providerEntity);

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
    }
}