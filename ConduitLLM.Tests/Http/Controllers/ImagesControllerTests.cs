using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Constants;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Http.Controllers;
using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace ConduitLLM.Tests.Http.Controllers
{
    [Trait("Category", "Unit")]
    [Trait("Component", "Http")]
    [Trait("Phase", "2")]
    public class ImagesControllerTests : ControllerTestBase
    {
        private readonly Mock<ILLMClientFactory> _mockClientFactory;
        private readonly Mock<IMediaStorageService> _mockStorageService;
        private readonly Mock<ILogger<ImagesController>> _mockLogger;
        private readonly Mock<ConduitLLM.Configuration.IModelProviderMappingService> _mockModelMappingService;
        private readonly Mock<IAsyncTaskService> _mockTaskService;
        private readonly Mock<IPublishEndpoint> _mockPublishEndpoint;
        private readonly Mock<IVirtualKeyService> _mockVirtualKeyService;
        private readonly Mock<IMediaLifecycleService> _mockMediaLifecycleService;
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
        private readonly Mock<ILLMClient> _mockLLMClient;
        private readonly Mock<IUrlHelper> _mockUrlHelper;
        private readonly ImagesController _controller;

        public ImagesControllerTests(ITestOutputHelper output) : base(output)
        {
            _mockClientFactory = new Mock<ILLMClientFactory>();
            _mockStorageService = new Mock<IMediaStorageService>();
            _mockLogger = CreateLogger<ImagesController>();
            _mockModelMappingService = new Mock<ConduitLLM.Configuration.IModelProviderMappingService>();
            _mockTaskService = new Mock<IAsyncTaskService>();
            _mockPublishEndpoint = new Mock<IPublishEndpoint>();
            _mockVirtualKeyService = new Mock<IVirtualKeyService>();
            _mockMediaLifecycleService = new Mock<IMediaLifecycleService>();
            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _mockLLMClient = new Mock<ILLMClient>();
            _mockUrlHelper = new Mock<IUrlHelper>();

            _controller = new ImagesController(
                _mockClientFactory.Object,
                _mockStorageService.Object,
                _mockLogger.Object,
                _mockModelMappingService.Object,
                _mockTaskService.Object,
                _mockPublishEndpoint.Object,
                _mockVirtualKeyService.Object,
                _mockMediaLifecycleService.Object,
                _mockHttpClientFactory.Object);

            // Setup default controller context
            _controller.ControllerContext = CreateControllerContext();
            _controller.Url = _mockUrlHelper.Object;
        }

        #region CreateImage Tests

        [Fact]
        public async Task CreateImage_WithEmptyPrompt_ShouldReturnBadRequest()
        {
            // Arrange
            var request = new ConduitLLM.Core.Models.ImageGenerationRequest
            {
                Prompt = "",
                Model = "dall-e-3"
            };

            // Act
            var result = await _controller.CreateImage(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var errorResponse = badRequestResult.Value as dynamic;
            Assert.NotNull(errorResponse);
            Assert.Equal("Prompt is required", errorResponse.error.message.ToString());
            Assert.Equal("invalid_request_error", errorResponse.error.type.ToString());
        }

        [Fact]
        public async Task CreateImage_WithUnsupportedModel_ShouldReturnBadRequest()
        {
            // Arrange
            var request = new ConduitLLM.Core.Models.ImageGenerationRequest
            {
                Prompt = "A beautiful sunset",
                Model = "gpt-4"
            };

            // Create a simple mapping with no image generation support
            var mapping = new ModelProviderMapping
            {
                ModelAlias = "gpt-4",
                ProviderModelId = "gpt-4",
                SupportsImageGeneration = false,
                Provider = new Provider { ProviderType = ProviderType.OpenAI }
            };

            _mockModelMappingService.Setup(x => x.GetMappingByModelAliasAsync("gpt-4"))
                .ReturnsAsync(mapping);

            // Act
            var result = await _controller.CreateImage(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var errorResponse = badRequestResult.Value as dynamic;
            Assert.NotNull(errorResponse);
            Assert.Equal("Model gpt-4 does not support image generation", errorResponse.error.message.ToString());
            Assert.Equal("invalid_request_error", errorResponse.error.type.ToString());
        }

        [Fact]
        public async Task CreateImage_WithServiceException_ShouldReturn500()
        {
            // Arrange
            var request = new ConduitLLM.Core.Models.ImageGenerationRequest
            {
                Prompt = "A beautiful sunset",
                Model = "dall-e-3"
            };

            _mockModelMappingService.Setup(x => x.GetMappingByModelAliasAsync(It.IsAny<string>()))
                .ThrowsAsync(new Exception("Service error"));

            // Act
            var result = await _controller.CreateImage(request);

            // Assert
            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, objectResult.StatusCode);
            var errorResponse = objectResult.Value as dynamic;
            Assert.NotNull(errorResponse);
            Assert.Equal("An error occurred while generating images", errorResponse.error.message.ToString());
            Assert.Equal("server_error", errorResponse.error.type.ToString());
        }

        #endregion

        #region CreateImageAsync Tests

        [Fact]
        public async Task CreateImageAsync_WithEmptyPrompt_ShouldReturnBadRequest()
        {
            // Arrange
            var request = new ConduitLLM.Core.Models.ImageGenerationRequest
            {
                Prompt = "",
                Model = "dall-e-3"
            };

            // Act
            var result = await _controller.CreateImageAsync(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var errorResponse = badRequestResult.Value as dynamic;
            Assert.NotNull(errorResponse);
            Assert.Equal("Prompt is required", errorResponse.error.message.ToString());
        }

        [Fact]
        public async Task CreateImageAsync_WithModelValidationFailure_ShouldReturnBadRequestBeforeAuth()
        {
            // Arrange
            var request = new ConduitLLM.Core.Models.ImageGenerationRequest
            {
                Prompt = "A beautiful sunset",
                Model = "gpt-4"
            };

            // Create a mapping that doesn't support image generation
            var mapping = new ModelProviderMapping
            {
                ModelAlias = "gpt-4",
                ProviderModelId = "gpt-4",
                SupportsImageGeneration = false,
                Provider = new Provider { ProviderType = ProviderType.OpenAI }
            };

            _mockModelMappingService.Setup(x => x.GetMappingByModelAliasAsync("gpt-4"))
                .ReturnsAsync(mapping);

            _controller.ControllerContext = CreateControllerContext();

            // Act
            var result = await _controller.CreateImageAsync(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var errorResponse = badRequestResult.Value as dynamic;
            Assert.NotNull(errorResponse);
            Assert.Equal("Model gpt-4 does not support image generation", errorResponse.error.message.ToString());
            Assert.Equal("invalid_request_error", errorResponse.error.type.ToString());
        }

        #endregion

        #region GetGenerationStatus Tests

        [Fact]
        public async Task GetGenerationStatus_WithNonExistentTask_ShouldReturnNotFound()
        {
            // Arrange
            var taskId = "non-existent-task";

            _mockTaskService.Setup(x => x.GetTaskStatusAsync(taskId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((AsyncTaskStatus?)null);

            // Act
            var result = await _controller.GetGenerationStatus(taskId);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            var errorResponse = notFoundResult.Value as dynamic;
            Assert.NotNull(errorResponse);
            Assert.Equal("Task not found", errorResponse.error.message.ToString());
            Assert.Equal("not_found_error", errorResponse.error.type.ToString());
        }

        [Fact]
        public async Task GetGenerationStatus_WithServiceException_ShouldReturn500()
        {
            // Arrange
            var taskId = "task-123";

            _mockTaskService.Setup(x => x.GetTaskStatusAsync(taskId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Service error"));

            // Act
            var result = await _controller.GetGenerationStatus(taskId);

            // Assert
            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, objectResult.StatusCode);
            var errorResponse = objectResult.Value as dynamic;
            Assert.NotNull(errorResponse);
            Assert.Equal("An error occurred while getting task status", errorResponse.error.message.ToString());
        }

        #endregion

        #region CancelGeneration Tests

        [Fact]
        public async Task CancelGeneration_WithNonExistentTask_ShouldReturnNotFound()
        {
            // Arrange
            var taskId = "non-existent-task";

            _mockTaskService.Setup(x => x.GetTaskStatusAsync(taskId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((AsyncTaskStatus?)null);

            // Act
            var result = await _controller.CancelGeneration(taskId);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            var errorResponse = notFoundResult.Value as dynamic;
            Assert.NotNull(errorResponse);
            Assert.Equal("Task not found", errorResponse.error.message.ToString());
        }

        [Fact]
        public async Task CancelGeneration_WithCompletedTask_ShouldReturnBadRequest()
        {
            // Arrange
            var taskId = "task-123";
            var virtualKeyId = 123;
            var virtualKeyValue = "condt_test_key_123456"; // Use a more realistic key format
            
            var taskStatus = new AsyncTaskStatus
            {
                TaskId = taskId,
                State = TaskState.Completed,
                Metadata = new ConduitLLM.Core.Models.TaskMetadata(virtualKeyId)
            };

            // Compute the correct hash
            string expectedHash;
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                var keyBytes = System.Text.Encoding.UTF8.GetBytes(virtualKeyValue);
                var hashBytes = sha256.ComputeHash(keyBytes);
                expectedHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }
            
            var virtualKey = new VirtualKey
            {
                Id = virtualKeyId,
                KeyHash = expectedHash
            };

            _mockTaskService.Setup(x => x.GetTaskStatusAsync(taskId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(taskStatus);

            _mockVirtualKeyService.Setup(x => x.GetVirtualKeyInfoForValidationAsync(virtualKeyId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(virtualKey);

            _controller.ControllerContext = CreateControllerContext();
            _controller.ControllerContext.HttpContext.User = new System.Security.Claims.ClaimsPrincipal(
                new System.Security.Claims.ClaimsIdentity(new[]
                {
                    new System.Security.Claims.Claim("VirtualKey", virtualKeyValue)
                }, "Test"));

            // Act
            var result = await _controller.CancelGeneration(taskId);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var errorResponse = badRequestResult.Value as dynamic;
            Assert.NotNull(errorResponse);
            Assert.Equal("Task has already completed", errorResponse.error.message.ToString());
            Assert.Equal("invalid_request_error", errorResponse.error.type.ToString());
        }

        #endregion

        #region Authorization Tests

        [Fact]
        public void Controller_ShouldRequireAuthorization()
        {
            // Arrange & Act
            var controllerType = typeof(ImagesController);
            var authorizeAttribute = Attribute.GetCustomAttribute(controllerType, typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute));

            // Assert
            Assert.NotNull(authorizeAttribute);
        }

        #endregion
    }
}