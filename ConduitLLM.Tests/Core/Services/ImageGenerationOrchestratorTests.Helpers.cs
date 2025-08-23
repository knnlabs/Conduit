using System.Net;
using System.Text;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using Moq;
using Moq.Protected;

namespace ConduitLLM.Tests.Core.Services
{
    public partial class ImageGenerationOrchestratorTests
    {
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

            // Setup model with image generation capabilities
            var modelEntity = new Model
            {
                Id = 1,
                Name = model,
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
                Provider = new Provider { ProviderType = provider switch
                {
                    "openai" => ProviderType.OpenAI,
                    "minimax" => ProviderType.MiniMax,
                    "replicate" => ProviderType.Replicate,
                    _ => ProviderType.Replicate
                } },
                ProviderModelId = model
            };

            _mockModelMappingService.Setup(x => x.GetMappingByModelAliasAsync(request.Request.Model))
                .Returns(Task.FromResult(modelMapping));

            // Setup provider service to return the provider
            var providerEntity = new Provider 
            { 
                Id = 1,
                ProviderType = provider switch
                {
                    "openai" => ProviderType.OpenAI,
                    "minimax" => ProviderType.MiniMax,
                    "replicate" => ProviderType.Replicate,
                    _ => ProviderType.Replicate
                }
            };

            _mockProviderService.Setup(x => x.GetProviderByIdAsync(1))
                .ReturnsAsync(providerEntity);

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
            
            // Setup cost calculation service
            // The test expects specific total costs based on the test parameters
            decimal expectedTotalCost = 0.020m * request.Request.N; // Default
            
            // Set expected costs based on the specific test case parameters
            if (provider == "openai" && model == "dall-e-3" && request.Request.N == 1)
                expectedTotalCost = 0.040m;
            else if (provider == "openai" && model == "dall-e-2" && request.Request.N == 2)
                expectedTotalCost = 0.040m;
            else if (provider == "minimax" && model == "minimax-image" && request.Request.N == 3)
                expectedTotalCost = 0.030m;
            else if (provider == "replicate" && model == "sdxl" && request.Request.N == 1)
                expectedTotalCost = 0.025m;
            else if (provider == "unknown" && model == "unknown-model" && request.Request.N == 1)
                expectedTotalCost = 0.025m;
            
            _mockCostCalculationService.Setup(x => x.CalculateCostAsync(
                It.Is<string>(m => m == model),
                It.Is<Usage>(u => u.ImageCount == request.Request.N),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedTotalCost);
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

            // Setup model with image generation capabilities
            var modelEntity = new Model
            {
                Id = 1,
                Name = model,
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
                Provider = new Provider { ProviderType = provider switch
                {
                    "openai" => ProviderType.OpenAI,
                    "minimax" => ProviderType.MiniMax,
                    "replicate" => ProviderType.Replicate,
                    _ => ProviderType.Replicate
                } },
                ProviderModelId = model
            };

            _mockModelMappingService.Setup(x => x.GetMappingByModelAliasAsync(request.Request.Model))
                .Returns(Task.FromResult(modelMapping));

            // Setup provider service to return the provider
            var providerEntity = new Provider 
            { 
                Id = 1,
                ProviderType = provider switch
                {
                    "openai" => ProviderType.OpenAI,
                    "minimax" => ProviderType.MiniMax,
                    "replicate" => ProviderType.Replicate,
                    _ => ProviderType.Replicate
                }
            };

            _mockProviderService.Setup(x => x.GetProviderByIdAsync(1))
                .ReturnsAsync(providerEntity);

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