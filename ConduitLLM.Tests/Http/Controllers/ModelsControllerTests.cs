using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Http.Controllers;
using ConduitLLM.Http.Models;
using ConduitLLM.Http.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace ConduitLLM.Tests.Http.Controllers
{
    public class ModelsControllerTests : ControllerTestBase
    {
        private readonly Mock<ILLMRouter> _mockRouter;
        private readonly Mock<ILogger<ModelsController>> _mockLogger;
        private readonly Mock<IModelMetadataService> _mockMetadataService;
        private readonly ModelsController _controller;

        public ModelsControllerTests(ITestOutputHelper output) : base(output)
        {
            _mockRouter = new Mock<ILLMRouter>();
            _mockLogger = CreateLogger<ModelsController>();
            _mockMetadataService = new Mock<IModelMetadataService>();

            _controller = new ModelsController(
                _mockRouter.Object,
                _mockLogger.Object,
                _mockMetadataService.Object);

            _controller.ControllerContext = CreateControllerContext();
        }

        #region ListModels Tests

        [Fact]
        public void ListModels_ReturnsAvailableModels()
        {
            // Arrange
            var models = new List<string> { "gpt-4", "gpt-3.5-turbo", "dall-e-3" };
            _mockRouter.Setup(x => x.GetAvailableModels()).Returns(models);

            // Act
            var result = _controller.ListModels();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            dynamic response = okResult.Value!;
            
            Assert.Equal("list", response.@object);
            Assert.NotNull(response.data);
            
            // Count the items in the data array
            int count = 0;
            foreach (var item in response.data)
            {
                count++;
                Assert.NotNull(item.id);
                Assert.Equal("model", item.@object);
            }
            Assert.Equal(3, count);
        }

        [Fact]
        public void ListModels_WhenExceptionThrown_Returns500()
        {
            // Arrange
            _mockRouter.Setup(x => x.GetAvailableModels())
                .Throws(new Exception("Test exception"));

            // Act
            var result = _controller.ListModels();

            // Assert
            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, objectResult.StatusCode);
            
            var errorResponse = Assert.IsType<OpenAIErrorResponse>(objectResult.Value);
            Assert.Equal("Test exception", errorResponse.Error.Message);
            Assert.Equal("server_error", errorResponse.Error.Type);
            Assert.Equal("internal_error", errorResponse.Error.Code);
        }

        #endregion

        #region GetModelMetadata Tests

        [Fact]
        public async Task GetModelMetadata_WhenMetadataExists_ReturnsMetadata()
        {
            // Arrange
            var modelId = "dall-e-3";
            var metadata = new
            {
                image = new
                {
                    sizes = new[] { "1024x1024", "1792x1024" },
                    maxImages = 1,
                    qualityOptions = new[] { "standard", "hd" }
                }
            };
            
            _mockMetadataService.Setup(x => x.GetModelMetadataAsync(modelId))
                .ReturnsAsync(metadata);

            // Act
            var result = await _controller.GetModelMetadata(modelId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            dynamic response = okResult.Value!;
            
            Assert.Equal(modelId, response.modelId);
            Assert.NotNull(response.metadata);
        }

        [Fact]
        public async Task GetModelMetadata_WhenMetadataNotFound_Returns404()
        {
            // Arrange
            var modelId = "nonexistent-model";
            _mockMetadataService.Setup(x => x.GetModelMetadataAsync(modelId))
                .ReturnsAsync((object?)null);

            // Act
            var result = await _controller.GetModelMetadata(modelId);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            var errorResponse = Assert.IsType<OpenAIErrorResponse>(notFoundResult.Value);
            
            Assert.Equal($"No metadata found for model '{modelId}'", errorResponse.Error.Message);
            Assert.Equal("invalid_request_error", errorResponse.Error.Type);
            Assert.Equal("model_not_found", errorResponse.Error.Code);
        }

        [Fact]
        public async Task GetModelMetadata_WhenExceptionThrown_Returns500()
        {
            // Arrange
            var modelId = "dall-e-3";
            _mockMetadataService.Setup(x => x.GetModelMetadataAsync(modelId))
                .ThrowsAsync(new Exception("Test exception"));

            // Act
            var result = await _controller.GetModelMetadata(modelId);

            // Assert
            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, objectResult.StatusCode);
            
            var errorResponse = Assert.IsType<OpenAIErrorResponse>(objectResult.Value);
            Assert.Equal("Test exception", errorResponse.Error.Message);
            Assert.Equal("server_error", errorResponse.Error.Type);
            Assert.Equal("internal_error", errorResponse.Error.Code);
        }

        [Fact]
        public async Task GetModelMetadata_LogsInformation()
        {
            // Arrange
            var modelId = "dall-e-3";
            var metadata = new { test = "data" };
            
            _mockMetadataService.Setup(x => x.GetModelMetadataAsync(modelId))
                .ReturnsAsync(metadata);

            // Act
            await _controller.GetModelMetadata(modelId);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Getting metadata for model {modelId}")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GetModelMetadata_WhenError_LogsError()
        {
            // Arrange
            var modelId = "dall-e-3";
            var exception = new Exception("Test error");
            
            _mockMetadataService.Setup(x => x.GetModelMetadataAsync(modelId))
                .ThrowsAsync(exception);

            // Act
            await _controller.GetModelMetadata(modelId);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Error retrieving metadata for model {modelId}")),
                    It.Is<Exception>(e => e == exception),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullRouter_ThrowsArgumentNullException()
        {
            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new ModelsController(
                null!,
                _mockLogger.Object,
                _mockMetadataService.Object));
            
            Assert.Equal("router", ex.ParamName);
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new ModelsController(
                _mockRouter.Object,
                null!,
                _mockMetadataService.Object));
            
            Assert.Equal("logger", ex.ParamName);
        }

        [Fact]
        public void Constructor_WithNullMetadataService_ThrowsArgumentNullException()
        {
            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new ModelsController(
                _mockRouter.Object,
                _mockLogger.Object,
                null!));
            
            Assert.Equal("metadataService", ex.ParamName);
        }

        #endregion

        #region Attribute Tests

        [Fact]
        public void Controller_HasCorrectAttributes()
        {
            // Arrange
            var controllerType = typeof(ModelsController);

            // Assert - Controller attributes
            Assert.NotNull(controllerType.GetCustomAttributes(typeof(ApiControllerAttribute), false));
            Assert.NotNull(controllerType.GetCustomAttributes(typeof(RouteAttribute), false));
            
            var routeAttr = (RouteAttribute)controllerType.GetCustomAttributes(typeof(RouteAttribute), false)[0];
            Assert.Equal("v1", routeAttr.Template);
        }

        [Fact]
        public void GetModelMetadata_HasCorrectRoute()
        {
            // Arrange
            var methodInfo = typeof(ModelsController).GetMethod(nameof(ModelsController.GetModelMetadata));

            // Assert
            Assert.NotNull(methodInfo);
            var routeAttr = methodInfo.GetCustomAttributes(typeof(HttpGetAttribute), false)[0] as HttpGetAttribute;
            Assert.NotNull(routeAttr);
            Assert.Equal("models/{modelId}/metadata", routeAttr.Template);
        }

        #endregion
    }
}