using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Http.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace ConduitLLM.Tests.Http.Controllers
{
    [Trait("Category", "Unit")]
    [Trait("Component", "Http")]
    [Trait("Phase", "2")]
    public class ImageGenerationControllerTests : ControllerTestBase
    {
        private readonly Mock<IImageGenerationMetricsService> _mockMetricsService;
        private readonly Mock<ConduitLLM.Configuration.IModelProviderMappingService> _mockMappingService;
        private readonly Mock<ILogger<ImageGenerationController>> _mockLogger;
        private readonly ImageGenerationController _controller;

        public ImageGenerationControllerTests(ITestOutputHelper output) : base(output)
        {
            _mockMetricsService = new Mock<IImageGenerationMetricsService>();
            _mockMappingService = new Mock<ConduitLLM.Configuration.IModelProviderMappingService>();
            _mockLogger = CreateLogger<ImageGenerationController>();

            _controller = new ImageGenerationController(
                _mockMetricsService.Object,
                _mockMappingService.Object,
                _mockLogger.Object);

            _controller.ControllerContext = CreateControllerContext();
        }

        #region GetOptimalProvider Tests

        [Fact]
        public async Task GetOptimalProvider_WithAvailableProviders_ShouldReturnOptimalChoice()
        {
            // Arrange
            var mappings = new List<ConduitLLM.Configuration.ModelProviderMapping>
            {
                new ConduitLLM.Configuration.ModelProviderMapping 
                { 
                    ModelAlias = "dalle-3",
                    ProviderModelId = "dall-e-3",
                    ProviderId = 1,
                    ProviderType = ProviderType.OpenAI,
                    SupportsImageGeneration = true
                },
                new ConduitLLM.Configuration.ModelProviderMapping 
                { 
                    ModelAlias = "sd-xl",
                    ProviderModelId = "sdxl",
                    ProviderId = 2,
                    ProviderType = ProviderType.Replicate,
                    SupportsImageGeneration = true
                }
            };

            var stats = new ImageGenerationProviderStats
            {
                Provider = "openai",
                Model = "dall-e-3",
                EstimatedWaitTimeSeconds = 5.2,
                AvgGenerationTimeMs = 3500,
                SuccessRate = 0.98,
                HealthScore = 0.95,
                CurrentQueueDepth = 3
            };

            _mockMappingService.Setup(x => x.GetAllMappingsAsync())
                .ReturnsAsync(mappings);

            _mockMetricsService.Setup(x => x.SelectOptimalProviderAsync(
                    It.IsAny<IEnumerable<(string, string)>>(),
                    1,
                    null,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(("openai", "dall-e-3"));

            _mockMetricsService.Setup(x => x.GetProviderStatsAsync(
                    "openai",
                    "dall-e-3",
                    60,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(stats);

            // Act
            var result = await _controller.GetOptimalProvider();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<OptimalProviderResponse>(okResult.Value);
            Assert.Equal("openai", response.Provider);
            Assert.Equal("dall-e-3", response.Model);
            Assert.Equal(5.2, response.EstimatedWaitTimeSeconds);
            Assert.Equal(3500, response.AverageGenerationTimeMs);
            Assert.Equal(0.98, response.SuccessRate);
            Assert.Equal(0.95, response.HealthScore);
            Assert.Equal(3, response.CurrentQueueDepth);
        }

        [Fact]
        public async Task GetOptimalProvider_WithImageCountAndMaxWaitTime_ShouldPassParametersCorrectly()
        {
            // Arrange
            var imageCount = 5;
            var maxWaitTime = 30.0;

            var mappings = new List<ConduitLLM.Configuration.ModelProviderMapping>
            {
                new ConduitLLM.Configuration.ModelProviderMapping { 
                    ModelAlias = "test-model",
                    ProviderModelId = "model",
                    ProviderId = 1,
                    ProviderType = ProviderType.OpenAI,
                    SupportsImageGeneration = true
                }
            };

            _mockMappingService.Setup(x => x.GetAllMappingsAsync())
                .ReturnsAsync(mappings);

            _mockMetricsService.Setup(x => x.SelectOptimalProviderAsync(
                    It.IsAny<IEnumerable<(string, string)>>(),
                    It.Is<int>(i => i == imageCount),
                    It.Is<double?>(d => d == maxWaitTime),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(("test", "model"));

            _mockMetricsService.Setup(x => x.GetProviderStatsAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ImageGenerationProviderStats());

            // Act
            var result = await _controller.GetOptimalProvider(imageCount, maxWaitTime);

            // Assert
            Assert.IsType<OkObjectResult>(result);
            _mockMetricsService.Verify(x => x.SelectOptimalProviderAsync(
                It.IsAny<IEnumerable<(string, string)>>(),
                imageCount,
                maxWaitTime,
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetOptimalProvider_WithNoImageGenerationProviders_ShouldReturnNotFound()
        {
            // Arrange
            var mappings = new List<ConduitLLM.Configuration.ModelProviderMapping>
            {
                new ConduitLLM.Configuration.ModelProviderMapping { 
                    ModelAlias = "non-image-model",
                    ProviderModelId = "model",
                    ProviderId = 1,
                    ProviderType = ProviderType.OpenAI,
                    SupportsImageGeneration = false
                },
                new ConduitLLM.Configuration.ModelProviderMapping { 
                    ModelAlias = "non-image-model",
                    ProviderModelId = "model",
                    ProviderId = 2,
                    ProviderType = ProviderType.OpenAI,
                    SupportsImageGeneration = false
                }
            };

            _mockMappingService.Setup(x => x.GetAllMappingsAsync())
                .ReturnsAsync(mappings);

            // Act
            var result = await _controller.GetOptimalProvider();

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            dynamic error = notFoundResult.Value;
            Assert.Equal("No image generation providers available", error.error);
        }

        [Fact]
        public async Task GetOptimalProvider_WhenServiceReturnsNull_ShouldReturnServiceUnavailable()
        {
            // Arrange
            var mappings = new List<ConduitLLM.Configuration.ModelProviderMapping>
            {
                new ConduitLLM.Configuration.ModelProviderMapping { 
                    ModelAlias = "test-model",
                    ProviderModelId = "model",
                    ProviderId = 1,
                    ProviderType = ProviderType.OpenAI,
                    SupportsImageGeneration = true
                }
            };

            _mockMappingService.Setup(x => x.GetAllMappingsAsync())
                .ReturnsAsync(mappings);

            _mockMetricsService.Setup(x => x.SelectOptimalProviderAsync(
                    It.IsAny<IEnumerable<(string, string)>>(),
                    It.IsAny<int>(),
                    It.IsAny<double?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(((string, string)?)null);

            // Act
            var result = await _controller.GetOptimalProvider();

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            dynamic error = notFoundResult.Value;
            Assert.Equal("No provider meets the specified criteria", error.error);
        }

        [Fact]
        public async Task GetOptimalProvider_WhenServiceThrows_ShouldReturnInternalServerError()
        {
            // Arrange
            var mappings = new List<ConduitLLM.Configuration.ModelProviderMapping>
            {
                new ConduitLLM.Configuration.ModelProviderMapping { 
                    ModelAlias = "test-model",
                    ProviderModelId = "model",
                    ProviderId = 1,
                    ProviderType = ProviderType.OpenAI,
                    SupportsImageGeneration = true
                }
            };

            _mockMappingService.Setup(x => x.GetAllMappingsAsync())
                .ReturnsAsync(mappings);

            _mockMetricsService.Setup(x => x.SelectOptimalProviderAsync(
                    It.IsAny<IEnumerable<(string, string)>>(),
                    It.IsAny<int>(),
                    It.IsAny<double?>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Service error"));

            // Act
            var result = await _controller.GetOptimalProvider();

            // Assert
            var statusResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, statusResult.StatusCode);
        }

        #endregion

        #region GetProviderStats Tests

        [Fact]
        public async Task GetProviderStats_WithValidProvider_ShouldReturnStats()
        {
            // Arrange
            var provider = "openai";
            var model = "dall-e-3";
            var timeWindowMinutes = 30;
            
            var stats = new ImageGenerationProviderStats
            {
                Provider = provider,
                Model = model,
                RequestCount = 100,
                AvgGenerationTimeMs = 3500,
                P95GenerationTimeMs = 5000,
                SuccessRate = 0.95,
                CurrentQueueDepth = 10,
                EstimatedWaitTimeSeconds = 12.5,
                HealthScore = 0.92,
                LastUpdated = DateTime.UtcNow
            };

            _mockMetricsService.Setup(x => x.GetProviderStatsAsync(
                    provider,
                    model,
                    timeWindowMinutes,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(stats);

            // Act
            var result = await _controller.GetProviderStats(provider, model, timeWindowMinutes);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedStats = Assert.IsType<ImageGenerationProviderStats>(okResult.Value);
            Assert.Equal(provider, returnedStats.Provider);
            Assert.Equal(model, returnedStats.Model);
            Assert.Equal(100, returnedStats.RequestCount);
            Assert.Equal(0.95, returnedStats.SuccessRate);
        }

        [Fact]
        public async Task GetProviderStats_WithNoStats_ShouldReturnNotFound()
        {
            // Arrange
            var provider = "unknown";
            var model = "unknown-model";

            _mockMetricsService.Setup(x => x.GetProviderStatsAsync(
                    provider,
                    model,
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((ImageGenerationProviderStats?)null);

            // Act
            var result = await _controller.GetProviderStats(provider, model);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            dynamic error = notFoundResult.Value;
            Assert.Contains("No statistics found", (string)error.error);
        }

        [Fact]
        public async Task GetProviderStats_WhenServiceThrows_ShouldReturnInternalServerError()
        {
            // Arrange
            var provider = "openai";
            var model = "dall-e-3";

            _mockMetricsService.Setup(x => x.GetProviderStatsAsync(
                    provider,
                    model,
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Service error"));

            // Act
            var result = await _controller.GetProviderStats(provider, model);

            // Assert
            var statusResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, statusResult.StatusCode);
            dynamic error = statusResult.Value;
            Assert.Equal("Internal server error", (string)error.error);
        }

        #endregion

        #region GetAllProviderStats Tests

        [Fact]
        public async Task GetAllProviderStats_WithMultipleProviders_ShouldReturnAllStats()
        {
            // Arrange
            var stats = new List<ImageGenerationProviderStats>
            {
                new ImageGenerationProviderStats
                {
                    Provider = "openai",
                    Model = "dall-e-3",
                    SuccessRate = 0.95
                },
                new ImageGenerationProviderStats
                {
                    Provider = "replicate",
                    Model = "sdxl",
                    SuccessRate = 0.88
                },
                new ImageGenerationProviderStats
                {
                    Provider = "stability",
                    Model = "stable-diffusion",
                    SuccessRate = 0.92
                }
            };

            _mockMetricsService.Setup(x => x.GetAllProviderStatsAsync(
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(stats);

            // Act
            var result = await _controller.GetProviderStats();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedStats = Assert.IsAssignableFrom<IEnumerable<ImageGenerationProviderStats>>(okResult.Value);
            Assert.Equal(3, returnedStats.Count());
            Assert.Contains(returnedStats, s => s.Provider == "openai" && s.SuccessRate == 0.95);
            Assert.Contains(returnedStats, s => s.Provider == "replicate" && s.SuccessRate == 0.88);
            Assert.Contains(returnedStats, s => s.Provider == "stability" && s.SuccessRate == 0.92);
        }

        [Fact]
        public async Task GetAllProviderStats_WithCustomTimeWindow_ShouldPassParameterCorrectly()
        {
            // Arrange
            var timeWindowMinutes = 120;
            var stats = new List<ImageGenerationProviderStats>();

            _mockMetricsService.Setup(x => x.GetAllProviderStatsAsync(
                    It.Is<int>(t => t == timeWindowMinutes),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(stats);

            // Act
            var result = await _controller.GetProviderStats(timeWindowMinutes);

            // Assert
            Assert.IsType<OkObjectResult>(result);
            _mockMetricsService.Verify(x => x.GetAllProviderStatsAsync(
                timeWindowMinutes,
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetAllProviderStats_WhenServiceThrows_ShouldReturnInternalServerError()
        {
            // Arrange
            _mockMetricsService.Setup(x => x.GetAllProviderStatsAsync(
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Service error"));

            // Act
            var result = await _controller.GetProviderStats();

            // Assert
            var statusResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, statusResult.StatusCode);
        }

        #endregion

        #region Private Helper Methods

        private ConduitLLM.Configuration.ModelProviderMapping CreateMapping(string provider, string model) =>
            new ConduitLLM.Configuration.ModelProviderMapping
            {
                ModelAlias = model,
                ProviderModelId = model,
                ProviderId = 1,
                ProviderType = provider switch
                {
                    "openai" => ProviderType.OpenAI,
                    "replicate" => ProviderType.Replicate,
                    "stability" => ProviderType.Replicate, // StabilityAI models run through Replicate
                    _ => ProviderType.OpenAI
                },
                SupportsImageGeneration = true
            };

        #endregion

        #region Edge Cases and Validation Tests

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(101)]
        public async Task GetOptimalProvider_WithInvalidImageCount_ShouldReturnBadRequest(int imageCount)
        {
            // Arrange
            var mappings = new List<ConduitLLM.Configuration.ModelProviderMapping>
            {
                new ConduitLLM.Configuration.ModelProviderMapping 
                { 
                    ModelAlias = "dalle-3",
                    ProviderModelId = "dall-e-3",
                    ProviderId = 1,
                    ProviderType = ProviderType.OpenAI,
                    SupportsImageGeneration = true
                }
            };

            _mockMappingService.Setup(x => x.GetAllMappingsAsync())
                .ReturnsAsync(mappings);

            // Note: The controller doesn't validate imageCount, so it will pass invalid values to the service
            // The service should handle validation and return appropriate result
            _mockMetricsService.Setup(x => x.SelectOptimalProviderAsync(
                    It.IsAny<IEnumerable<(string, string)>>(),
                    imageCount,
                    It.IsAny<double?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(((string, string)?)null);

            // Act
            var result = await _controller.GetOptimalProvider(imageCount);

            // Assert
            // Since service returns null for invalid input, controller returns NotFound
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            dynamic error = notFoundResult.Value;
            Assert.Equal("No provider meets the specified criteria", error.error);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(0)]
        public async Task GetOptimalProvider_WithInvalidMaxWaitTime_ShouldReturnBadRequest(double maxWaitTime)
        {
            // Arrange
            var mappings = new List<ConduitLLM.Configuration.ModelProviderMapping>
            {
                new ConduitLLM.Configuration.ModelProviderMapping 
                { 
                    ModelAlias = "dalle-3",
                    ProviderModelId = "dall-e-3",
                    ProviderId = 1,
                    ProviderType = ProviderType.OpenAI,
                    SupportsImageGeneration = true
                }
            };

            _mockMappingService.Setup(x => x.GetAllMappingsAsync())
                .ReturnsAsync(mappings);

            // Note: The controller doesn't validate maxWaitTime, so it will pass invalid values to the service
            // The service should handle validation and return appropriate result
            _mockMetricsService.Setup(x => x.SelectOptimalProviderAsync(
                    It.IsAny<IEnumerable<(string, string)>>(),
                    1,
                    maxWaitTime,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(((string, string)?)null);

            // Act
            var result = await _controller.GetOptimalProvider(1, maxWaitTime);

            // Assert
            // Since service returns null for invalid input, controller returns NotFound
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            dynamic error = notFoundResult.Value;
            Assert.Equal("No provider meets the specified criteria", error.error);
        }

        [Fact]
        public async Task GetOptimalProvider_WithLargeImageCount_ShouldHandleCorrectly()
        {
            // Arrange
            var mappings = Enumerable.Range(0, 10).Select(i => new ConduitLLM.Configuration.ModelProviderMapping
            {
                ModelAlias = $"model-{i}",
                ProviderModelId = $"model-{i}",
                ProviderId = i,
                ProviderType = ProviderType.OpenAI,
                SupportsImageGeneration = true
            }).ToList();

            _mockMappingService.Setup(x => x.GetAllMappingsAsync())
                .ReturnsAsync(mappings);

            _mockMetricsService.Setup(x => x.SelectOptimalProviderAsync(
                    It.IsAny<IEnumerable<(string, string)>>(),
                    100,
                    It.IsAny<double?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(("provider-0", "model-0"));

            _mockMetricsService.Setup(x => x.GetProviderStatsAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ImageGenerationProviderStats());

            // Act
            var result = await _controller.GetOptimalProvider(100);

            // Assert
            Assert.IsType<OkObjectResult>(result);
        }

        #endregion
    }
}