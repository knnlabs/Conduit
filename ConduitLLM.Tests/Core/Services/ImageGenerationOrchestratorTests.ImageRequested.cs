using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using Moq;

namespace ConduitLLM.Tests.Core.Services
{
    public partial class ImageGenerationOrchestratorTests
    {
        #region ImageGenerationRequested Event Tests - Helper Methods

        private void SetupSuccessfulImageGeneration(ImageGenerationRequested request)
        {
            // Setup virtual key
            var virtualKey = new VirtualKey
            {
                Id = request.VirtualKeyId,
                IsEnabled = true,
                KeyName = "test-virtual-key",
                KeyHash = request.VirtualKeyHash
            };

            _mockVirtualKeyService.Setup(x => x.ValidateVirtualKeyAsync(request.VirtualKeyHash, request.Request.Model))
                .ReturnsAsync(virtualKey);

            // Setup model with image generation capabilities
            var modelEntity = new Model
            {
                Id = 1,
                Name = request.Request.Model,
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
                ModelAlias = request.Request.Model,
                ModelId = 1,
                Model = modelEntity,
                ProviderId = 1,
                ProviderModelId = request.Request.Model,
                Provider = new Provider { ProviderType = ProviderType.OpenAI }
            };

            _mockModelMappingService.Setup(x => x.GetMappingByModelAliasAsync(request.Request.Model))
                .Returns(Task.FromResult(modelMapping));

            // Setup provider service to return the provider
            var providerEntity = new Provider 
            { 
                Id = 1,
                ProviderType = ProviderType.OpenAI
            };

            _mockProviderService.Setup(x => x.GetProviderByIdAsync(1))
                .ReturnsAsync(providerEntity);

            // Setup client response with URLs
            var mockClient = new Mock<ILLMClient>();
            var imageResponse = new ConduitLLM.Core.Models.ImageGenerationResponse
            {
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Data = new List<ConduitLLM.Core.Models.ImageData>()
            };

            for (int i = 0; i < (request.Request.N > 0 ? request.Request.N : 1); i++)
            {
                imageResponse.Data.Add(new ConduitLLM.Core.Models.ImageData
                {
                    Url = $"https://example.com/image{i + 1}.png",
                    B64Json = null
                });
            }

            mockClient.Setup(x => x.CreateImageAsync(
                It.IsAny<ConduitLLM.Core.Models.ImageGenerationRequest>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(imageResponse);

            _mockClientFactory.Setup(x => x.GetClient(request.Request.Model))
                .Returns(mockClient.Object);

            // Setup storage service for each image
            var storageResults = new List<MediaStorageResult>();
            for (int i = 0; i < (request.Request.N > 0 ? request.Request.N : 1); i++)
            {
                storageResults.Add(new MediaStorageResult
                {
                    StorageKey = $"image/test-key-{i + 1}.png",
                    Url = $"https://storage.example.com/image/test-key-{i + 1}.png",
                    SizeBytes = 1024,
                    ContentHash = $"test-hash-{i + 1}",
                    CreatedAt = DateTime.UtcNow
                });
            }

            var storageCallCount = 0;
            _mockStorageService.Setup(x => x.StoreAsync(
                It.IsAny<Stream>(),
                It.IsAny<MediaMetadata>(),
                It.IsAny<IProgress<long>>()))
                .ReturnsAsync(() => storageResults[storageCallCount++]);
        }

        #endregion
    }
}