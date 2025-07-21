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
        private readonly Mock<IModelProviderMappingService> _mockMappingService;
        private readonly Mock<ILogger<ImageGenerationController>> _mockLogger;
        private readonly ImageGenerationController _controller;

        public ImageGenerationControllerTests(ITestOutputHelper output) : base(output)
        {
            _mockMetricsService = new Mock<IImageGenerationMetricsService>();
            _mockMappingService = new Mock<IModelProviderMappingService>();
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
            var mappings = new List<ModelProviderMapping>
            {
                new ModelProviderMapping 
                { 
                    ModelAlias = "dalle-3",
                    ProviderName = "openai",
                    ProviderModelId = "dall-e-3",
                    SupportsImageGeneration = true
                },
                new ModelProviderMapping 
                { 
                    ModelAlias = "sd-xl",
                    ProviderName = "replicate",
                    ProviderModelId = "sdxl",
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

            var mappings = new List<ModelProviderMapping>
            {
                new ModelProviderMapping { 
                    ModelAlias = "test-model",
                    ProviderName = "test", 
                    ProviderModelId = "model",
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
        public async Task GetOptimalProvider_WithNoImageProviders_ShouldReturnNotFound()
        {
            // Arrange
            var mappings = new List<ModelProviderMapping>
            {
                new ModelProviderMapping { 
                    ModelAlias = "non-image-model",
                    ProviderName = "provider",
                    ProviderModelId = "model",
                    SupportsImageGeneration = false 
                },
                new ModelProviderMapping { 
                    ModelAlias = "non-image-model",
                    ProviderName = "provider",
                    ProviderModelId = "model",
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
            Assert.Equal("No image generation providers available", error.error.ToString());
        }

        [Fact]
        public async Task GetOptimalProvider_WithNoProviderMeetingCriteria_ShouldReturnNotFound()
        {
            // Arrange
            var mappings = new List<ModelProviderMapping>
            {
                new ModelProviderMapping { 
                    ModelAlias = "test-model",
                    ProviderName = "test", 
                    ProviderModelId = "model",
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
            Assert.Equal("No provider meets the specified criteria", error.error.ToString());
        }

        [Fact]
        public async Task GetOptimalProvider_WithNullStats_ShouldUseDefaultValues()
        {
            // Arrange
            var mappings = new List<ModelProviderMapping>
            {
                new ModelProviderMapping { 
                    ModelAlias = "test-model",
                    ProviderName = "test", 
                    ProviderModelId = "model",
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
                .ReturnsAsync(("test", "model"));

            _mockMetricsService.Setup(x => x.GetProviderStatsAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((ImageGenerationProviderStats)null);

            // Act
            var result = await _controller.GetOptimalProvider();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<OptimalProviderResponse>(okResult.Value);
            Assert.Equal(0, response.EstimatedWaitTimeSeconds);
            Assert.Equal(0, response.AverageGenerationTimeMs);
            Assert.Equal(1.0, response.SuccessRate);
            Assert.Equal(1.0, response.HealthScore);
            Assert.Equal(0, response.CurrentQueueDepth);
        }

        [Fact]
        public async Task GetOptimalProvider_WithException_ShouldReturn500()
        {
            // Arrange
            _mockMappingService.Setup(x => x.GetAllMappingsAsync())
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _controller.GetOptimalProvider();

            // Assert
            var statusCodeResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, statusCodeResult.StatusCode);
            dynamic error = statusCodeResult.Value;
            Assert.Equal("Internal server error", error.error.ToString());
        }

        #endregion

        #region GetProviderStats (All Providers) Tests

        [Fact]
        public async Task GetProviderStats_All_ShouldReturnAllStats()
        {
            // Arrange
            var stats = new List<ImageGenerationProviderStats>
            {
                new ImageGenerationProviderStats
                {
                    Provider = "openai",
                    Model = "dall-e-3",
                    AvgGenerationTimeMs = 3500,
                    P95GenerationTimeMs = 5000,
                    SuccessRate = 0.98,
                    RequestCount = 150
                },
                new ImageGenerationProviderStats
                {
                    Provider = "replicate",
                    Model = "sdxl",
                    AvgGenerationTimeMs = 2800,
                    P95GenerationTimeMs = 4200,
                    SuccessRate = 0.95,
                    RequestCount = 200
                }
            };

            _mockMetricsService.Setup(x => x.GetAllProviderStatsAsync(60, It.IsAny<CancellationToken>()))
                .ReturnsAsync(stats);

            // Act
            var result = await _controller.GetProviderStats();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsAssignableFrom<IEnumerable<ImageGenerationProviderStats>>(okResult.Value);
            Assert.Equal(2, response.Count());
        }

        [Fact]
        public async Task GetProviderStats_All_WithCustomWindow_ShouldPassCorrectParameter()
        {
            // Arrange
            var windowMinutes = 120;
            _mockMetricsService.Setup(x => x.GetAllProviderStatsAsync(
                    It.Is<int>(w => w == windowMinutes), 
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ImageGenerationProviderStats>());

            // Act
            var result = await _controller.GetProviderStats(windowMinutes);

            // Assert
            Assert.IsType<OkObjectResult>(result);
            _mockMetricsService.Verify(x => x.GetAllProviderStatsAsync(windowMinutes, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetProviderStats_All_WithException_ShouldReturn500()
        {
            // Arrange
            _mockMetricsService.Setup(x => x.GetAllProviderStatsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Metrics service error"));

            // Act
            var result = await _controller.GetProviderStats();

            // Assert
            var statusCodeResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, statusCodeResult.StatusCode);
            dynamic error = statusCodeResult.Value;
            Assert.Equal("Internal server error", error.error.ToString());
        }

        #endregion

        #region GetProviderStats (Specific Provider) Tests

        [Fact]
        public async Task GetProviderStats_Specific_WithValidProvider_ShouldReturnStats()
        {
            // Arrange
            var provider = "openai";
            var model = "dall-e-3";
            var stats = new ImageGenerationProviderStats
            {
                Provider = provider,
                Model = model,
                AvgGenerationTimeMs = 3500,
                P95GenerationTimeMs = 5000,
                SuccessRate = 0.98,
                RequestCount = 150,
                IsHealthy = true,
                WindowMinutes = 60
            };

            _mockMetricsService.Setup(x => x.GetProviderStatsAsync(provider, model, 60, It.IsAny<CancellationToken>()))
                .ReturnsAsync(stats);

            // Act
            var result = await _controller.GetProviderStats(provider, model);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ImageGenerationProviderStats>(okResult.Value);
            Assert.Equal(provider, response.Provider);
            Assert.Equal(model, response.Model);
            Assert.Equal(3500, response.AvgGenerationTimeMs);
            Assert.Equal(0.98, response.SuccessRate);
        }

        [Fact]
        public async Task GetProviderStats_Specific_WithCustomWindow_ShouldPassCorrectParameter()
        {
            // Arrange
            var provider = "test";
            var model = "model";
            var windowMinutes = 180;
            
            _mockMetricsService.Setup(x => x.GetProviderStatsAsync(
                    provider, 
                    model, 
                    It.Is<int>(w => w == windowMinutes), 
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ImageGenerationProviderStats());

            // Act
            var result = await _controller.GetProviderStats(provider, model, windowMinutes);

            // Assert
            Assert.IsType<OkObjectResult>(result);
            _mockMetricsService.Verify(x => x.GetProviderStatsAsync(
                provider, model, windowMinutes, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetProviderStats_Specific_WithNoStats_ShouldReturnNotFound()
        {
            // Arrange
            var provider = "unknown";
            var model = "model";

            _mockMetricsService.Setup(x => x.GetProviderStatsAsync(provider, model, It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ImageGenerationProviderStats)null);

            // Act
            var result = await _controller.GetProviderStats(provider, model);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            dynamic error = notFoundResult.Value;
            Assert.Equal($"No statistics found for {provider}/{model}", error.error.ToString());
        }

        [Fact]
        public async Task GetProviderStats_Specific_WithException_ShouldReturn500()
        {
            // Arrange
            var provider = "test";
            var model = "model";

            _mockMetricsService.Setup(x => x.GetProviderStatsAsync(
                    It.IsAny<string>(), 
                    It.IsAny<string>(), 
                    It.IsAny<int>(), 
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _controller.GetProviderStats(provider, model);

            // Assert
            var statusCodeResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, statusCodeResult.StatusCode);
            dynamic error = statusCodeResult.Value;
            Assert.Equal("Internal server error", error.error.ToString());
        }

        #endregion

        #region Edge Cases

        [Theory]
        [InlineData("provider/with/slash", "model")]
        [InlineData("provider", "model/with/slash")]
        [InlineData("provider-with-dash", "model_with_underscore")]
        public async Task GetProviderStats_Specific_WithSpecialCharacters_ShouldHandleCorrectly(string provider, string model)
        {
            // Arrange
            _mockMetricsService.Setup(x => x.GetProviderStatsAsync(
                    provider, 
                    model, 
                    It.IsAny<int>(), 
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ImageGenerationProviderStats { Provider = provider, Model = model });

            // Act
            var result = await _controller.GetProviderStats(provider, model);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ImageGenerationProviderStats>(okResult.Value);
            Assert.Equal(provider, response.Provider);
            Assert.Equal(model, response.Model);
        }

        [Fact]
        public async Task GetOptimalProvider_WithManyProviders_ShouldSelectFromAll()
        {
            // Arrange
            var mappings = Enumerable.Range(1, 10).Select(i => new ModelProviderMapping
            {
                ModelAlias = $"model-{i}",
                ProviderName = $"provider-{i}",
                ProviderModelId = $"model-{i}",
                SupportsImageGeneration = true
            }).ToList();

            _mockMappingService.Setup(x => x.GetAllMappingsAsync())
                .ReturnsAsync(mappings);

            var selectedProvider = "provider-5";
            var selectedModel = "model-5";

            _mockMetricsService.Setup(x => x.SelectOptimalProviderAsync(
                    It.Is<IEnumerable<(string, string)>>(providers => providers.Count() == 10),
                    It.IsAny<int>(),
                    It.IsAny<double?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((selectedProvider, selectedModel));

            _mockMetricsService.Setup(x => x.GetProviderStatsAsync(
                    selectedProvider,
                    selectedModel,
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ImageGenerationProviderStats());

            // Act
            var result = await _controller.GetOptimalProvider();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<OptimalProviderResponse>(okResult.Value);
            Assert.Equal(selectedProvider, response.Provider);
            Assert.Equal(selectedModel, response.Model);
        }

        #endregion

        #region Authorization Tests

        [Fact]
        public void Controller_ShouldRequireAuthorization()
        {
            // Arrange & Act
            var controllerType = typeof(ImageGenerationController);
            var authorizeAttribute = Attribute.GetCustomAttribute(controllerType, typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute));

            // Assert
            Assert.NotNull(authorizeAttribute);
        }

        #endregion
    }
}