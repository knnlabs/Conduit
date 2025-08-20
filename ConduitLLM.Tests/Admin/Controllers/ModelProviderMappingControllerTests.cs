// TODO: Update tests for new Model architecture where capabilities come from Model entity
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ConduitLLM.Admin.Controllers;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Tests.Admin.TestHelpers;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Extensions;
using ConduitLLM.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;
using ConduitLLM.Configuration.Interfaces;

namespace ConduitLLM.Tests.Admin.Controllers
{
    /// <summary>
    /// Unit tests for the ModelProviderMappingController class.
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Component", "AdminController")]
    public class ModelProviderMappingControllerTests
    {
        private readonly Mock<IAdminModelProviderMappingService> _mockService;
        private readonly Mock<IProviderService> _mockCredentialService;
        private readonly Mock<ILogger<ModelProviderMappingController>> _mockLogger;
        private readonly ModelProviderMappingController _controller;
        private readonly ITestOutputHelper _output;

        public ModelProviderMappingControllerTests(ITestOutputHelper output)
        {
            _output = output;
            _mockService = new Mock<IAdminModelProviderMappingService>();
            _mockCredentialService = new Mock<IProviderService>();
            _mockLogger = new Mock<ILogger<ModelProviderMappingController>>();
            _controller = new ModelProviderMappingController(_mockService.Object, _mockCredentialService.Object, _mockLogger.Object);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullService_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new ModelProviderMappingController(null!, _mockCredentialService.Object, _mockLogger.Object));
        }


        [Fact]
        public void Constructor_WithNullCredentialService_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new ModelProviderMappingController(_mockService.Object, null!, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new ModelProviderMappingController(_mockService.Object, _mockCredentialService.Object, null!));
        }

        #endregion

        #region GetAllMappings Tests

        [Fact]
        public async Task GetAllMappings_WithMappings_ShouldReturnOkWithList()
        {
            // Arrange
            var mappings = new List<ModelProviderMapping>
            {
                new() 
                { 
                    Id = 1,
                    ModelAlias = "gpt-4",
                    ModelId = 1,
                    ProviderId = 1,
                    ProviderModelId = "gpt-4-turbo",
                    // SupportsVision = true,
                    IsEnabled = true
                },
                new() 
                { 
                    Id = 2,
                    ModelAlias = "claude-3",
                    ProviderId = 2,
                    ProviderModelId = "claude-3-opus",
                    // SupportsVision = true,
                    IsEnabled = true
                }
            };

            _mockService.Setup(x => x.GetAllMappingsAsync())
                .ReturnsAsync(mappings);

            // Act
            var result = await _controller.GetAllMappings();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedMappings = Assert.IsAssignableFrom<IEnumerable<ModelProviderMappingDto>>(okResult.Value);
            returnedMappings.Should().HaveCount(2);
            returnedMappings.First().ModelId.Should().Be(1);
        }

        [Fact]
        public async Task GetAllMappings_WithException_ShouldReturn500()
        {
            // Arrange
            _mockService.Setup(x => x.GetAllMappingsAsync())
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _controller.GetAllMappings();

            // Assert
            var statusCodeResult = Assert.IsType<ObjectResult>(result);
            statusCodeResult.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
            
            _mockLogger.VerifyLogWithAnyException(LogLevel.Error, "Error getting all model provider mappings");
        }

        #endregion

        #region GetMappingById Tests

        [Fact]
        public async Task GetMappingById_WithExistingId_ShouldReturnOkWithMapping()
        {
            // Arrange
            var mapping = new ModelProviderMapping
            {
                Id = 1,
                ModelAlias = "gpt-4",
                    ModelId = 1,
                ProviderId = 1,
                ProviderModelId = "gpt-4-turbo"
            };

            _mockService.Setup(x => x.GetMappingByIdAsync(1))
                .ReturnsAsync(mapping);

            // Act
            var result = await _controller.GetMappingById(1);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedMapping = Assert.IsType<ModelProviderMappingDto>(okResult.Value);
            returnedMapping.ModelId.Should().Be(1);
        }

        [Fact]
        public async Task GetMappingById_WithNonExistingId_ShouldReturnNotFound()
        {
            // Arrange
            _mockService.Setup(x => x.GetMappingByIdAsync(999))
                .ReturnsAsync((ModelProviderMapping?)null);

            // Act
            var result = await _controller.GetMappingById(999);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            var errorResponse = Assert.IsType<ErrorResponseDto>(notFoundResult.Value);
            errorResponse.error.ToString().Should().Be("Model provider mapping not found");
        }

        #endregion

        #region GetMappingByModelId Tests - REMOVED: Method doesn't exist in controller

        #endregion

        #region AddMapping Tests

        [Fact]
        public async Task AddMapping_WithValidMapping_ShouldReturnCreated()
        {
            // Arrange
            var mapping = new ModelProviderMapping
            {
                ModelAlias = "new-model",
                    ModelId = 1,
                ProviderId = 1,
                ProviderModelId = "gpt-4-new",
                // SupportsStreaming = true
            };

            var createdMapping = new ModelProviderMapping
            {
                Id = 123,
                ModelAlias = "new-model",
                    ModelId = 1,
                ProviderId = 1,
                ProviderModelId = "gpt-4-new",
                // SupportsStreaming = true
            };

            // First call should return null (no existing mapping)
            _mockService.Setup(x => x.GetMappingByModelIdAsync(1))
                .ReturnsAsync((ModelProviderMapping?)null);
            
            _mockService.Setup(x => x.AddMappingAsync(It.IsAny<ModelProviderMapping>()))
                .ReturnsAsync(true);

            // Second call should return the created mapping
            _mockService.SetupSequence(x => x.GetMappingByModelIdAsync(1))
                .ReturnsAsync((ModelProviderMapping?)null)  // First call (check existence)
                .ReturnsAsync(createdMapping);              // Second call (get created)

            // Act
            var actionResult = await _controller.CreateMapping(mapping.ToDto());

            // Assert
            var createdResult = Assert.IsType<CreatedAtActionResult>(actionResult);
            createdResult.ActionName.Should().Be(nameof(ModelProviderMappingController.GetMappingById));
            createdResult.RouteValues!["id"].Should().Be(123);
        }

        [Fact]
        public async Task AddMapping_WithDuplicateModelId_ShouldReturnConflict()
        {
            // Arrange
            var mapping = new ModelProviderMapping
            {
                ModelAlias = "existing-model",
                    ModelId = 1,
                ProviderId = 1,
                ProviderModelId = "gpt-4"
            };

            // Mock that a mapping already exists for this model ID
            _mockService.Setup(x => x.GetMappingByModelIdAsync(1))
                .ReturnsAsync(new ModelProviderMapping { Id = 999, ModelAlias = "existing-model", ModelId = 1 });

            // Act
            var actionResult = await _controller.CreateMapping(mapping.ToDto());

            // Assert
            var conflictResult = Assert.IsType<ConflictObjectResult>(actionResult);
            var errorResponse = Assert.IsType<ErrorResponseDto>(conflictResult.Value);
            errorResponse.error.ToString().Should().Contain("already exists");
        }

        [Fact]
        public async Task AddMapping_WithInvalidProviderId_ShouldReturnBadRequest()
        {
            // Arrange
            var mapping = new ModelProviderMapping
            {
                ModelAlias = "new-model",
                    ModelId = 1,
                ProviderId = 999, // Invalid provider
                ProviderModelId = "gpt-4"
            };

            // No existing mapping
            _mockService.Setup(x => x.GetMappingByModelIdAsync(1))
                .ReturnsAsync((ModelProviderMapping?)null);

            // Add fails (e.g., invalid provider ID)
            _mockService.Setup(x => x.AddMappingAsync(It.IsAny<ModelProviderMapping>()))
                .ReturnsAsync(false);

            // Act
            var actionResult = await _controller.CreateMapping(mapping.ToDto());

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(actionResult);
            var errorResponse = Assert.IsType<ErrorResponseDto>(badRequestResult.Value);
            errorResponse.error.ToString().Should().Contain("Failed to create");
        }

        #endregion

        #region UpdateMapping Tests

        [Fact]
        public async Task UpdateMapping_WithValidMapping_ShouldReturnNoContent()
        {
            // Arrange
            var mapping = new ModelProviderMapping
            {
                Id = 1,
                ModelAlias = "gpt-4",
                    ModelId = 1,
                ProviderId = 1,
                ProviderModelId = "gpt-4-turbo-updated",
                // SupportsVision = true
            };

            // Mock that the mapping exists
            _mockService.Setup(x => x.GetMappingByIdAsync(1))
                .ReturnsAsync(mapping);

            _mockService.Setup(x => x.UpdateMappingAsync(It.IsAny<ModelProviderMapping>()))
                .ReturnsAsync(true);

            // Act
            var actionResult = await _controller.UpdateMapping(1, mapping.ToDto());

            // Assert
            Assert.IsType<NoContentResult>(actionResult);
        }

        [Fact]
        public async Task UpdateMapping_WithNonExistingId_ShouldReturnNotFound()
        {
            // Arrange
            var mapping = new ModelProviderMapping
            {
                Id = 999,
                ModelAlias = "gpt-4",
                    ModelId = 1,
                ProviderId = 1,
                ProviderModelId = "gpt-4"
            };

            // Mock that the mapping doesn't exist
            _mockService.Setup(x => x.GetMappingByIdAsync(999))
                .ReturnsAsync((ModelProviderMapping?)null);

            // Act
            var actionResult = await _controller.UpdateMapping(999, mapping.ToDto());

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(actionResult);
            var errorResponse = Assert.IsType<ErrorResponseDto>(notFoundResult.Value);
            errorResponse.error.ToString().Should().Be("Model provider mapping not found");
        }

        #endregion

        #region DeleteMapping Tests

        [Fact]
        public async Task DeleteMapping_WithExistingId_ShouldReturnNoContent()
        {
            // Arrange
            var existingMapping = new ModelProviderMapping { Id = 1, ModelAlias = "test-model", ModelId = 1 };
            _mockService.Setup(x => x.GetMappingByIdAsync(1))
                .ReturnsAsync(existingMapping);
            _mockService.Setup(x => x.DeleteMappingAsync(1))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.DeleteMapping(1);

            // Assert
            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task DeleteMapping_WithNonExistingId_ShouldReturnNotFound()
        {
            // Arrange
            _mockService.Setup(x => x.GetMappingByIdAsync(999))
                .ReturnsAsync((ModelProviderMapping?)null);

            // Act
            var result = await _controller.DeleteMapping(999);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            var errorResponse = Assert.IsType<ErrorResponseDto>(notFoundResult.Value);
            errorResponse.error.ToString().Should().Be("Model provider mapping not found");
        }

        #endregion

        #region GetProvidersAsync Tests

        [Fact]
        public async Task GetProviders_ShouldReturnProviderList()
        {
            // Arrange
            var providers = new List<Provider>
            {
                new() { Id = 1, ProviderType = ProviderType.OpenAI },
                new() { Id = 2, ProviderType = ProviderType.Groq },
                new() { Id = 3, ProviderType = ProviderType.MiniMax }
            };

            _mockService.Setup(x => x.GetProvidersAsync())
                .ReturnsAsync(providers);

            // Act
            var result = await _controller.GetProviders();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedProviders = Assert.IsAssignableFrom<IEnumerable<Provider>>(okResult.Value);
            returnedProviders.Should().HaveCount(3);
        }

        #endregion

        #region BulkCreateMappings Tests

        [Fact]
        public async Task BulkCreateMappings_WithValidMappings_ShouldReturnSuccess()
        {
            // Arrange
            var mappings = new List<ModelProviderMapping>
            {
                new() 
                { 
                    ModelAlias = "model1", 
                    ModelId = 1,
                    ProviderId = 1, 
                    ProviderModelId = "gpt-4"
                },
                new() 
                { 
                    ModelAlias = "model2", 
                    ModelId = 2,
                    ProviderId = 2, 
                    ProviderModelId = "claude-3"
                    // SupportsVision = true 
                }
            };

            var created = new List<ModelProviderMapping>
            {
                new() { Id = 1, ModelAlias = "model1", ProviderId = 1, ProviderModelId = "gpt-4", ModelId = 1 },
                new() { Id = 2, ModelAlias = "model2", ProviderId = 2, ProviderModelId = "claude-3", ModelId = 2 }
            };
            var errors = new List<string>();

            _mockService.Setup(x => x.CreateBulkMappingsAsync(It.IsAny<IEnumerable<ModelProviderMapping>>()))
                .ReturnsAsync((created, errors));

            // Act
            var result = await _controller.CreateBulkMappings(mappings.Select(m => m.ToDto()).ToList());

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedResponse = Assert.IsType<BulkMappingResult>(okResult.Value);
            returnedResponse.TotalProcessed.Should().Be(2);
            returnedResponse.Created.Should().HaveCount(2);
            returnedResponse.SuccessCount.Should().Be(2);
        }

        [Fact]
        public async Task BulkCreateMappings_WithSomeFailures_ShouldReturnPartialSuccess()
        {
            // Arrange
            var mappings = new List<ModelProviderMapping>
            {
                new() { ModelAlias = "model1", ProviderId = 1, ProviderModelId = "gpt-4", ModelId = 1 },
                new() { ModelAlias = "duplicate", ProviderId = 1, ProviderModelId = "gpt-4", ModelId = 1 },
                new() { ModelAlias = "model3", ProviderId = 1, ProviderModelId = "model", ModelId = 1 }
            };

            var created = new List<ModelProviderMapping>
            {
                new() { Id = 1, ModelAlias = "model1", ProviderId = 1, ModelId = 1 }
            };
            var errors = new List<string>
            {
                "Model ID already exists",
                "Provider not found"
            };

            _mockService.Setup(x => x.CreateBulkMappingsAsync(It.IsAny<IEnumerable<ModelProviderMapping>>()))
                .ReturnsAsync((created, errors));

            // Act
            var result = await _controller.CreateBulkMappings(mappings.Select(m => m.ToDto()).ToList());

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedResponse = Assert.IsType<BulkMappingResult>(okResult.Value);
            returnedResponse.SuccessCount.Should().Be(1);
            returnedResponse.FailureCount.Should().Be(2);
        }

        [Fact]
        public async Task BulkCreateMappings_WithEmptyRequest_ShouldReturnBadRequest()
        {
            // Arrange
            var mappings = new List<ModelProviderMapping>();

            // Act
            var result = await _controller.CreateBulkMappings(mappings.Select(m => m.ToDto()).ToList());

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var errorResponse = Assert.IsType<ErrorResponseDto>(badRequestResult.Value);
            errorResponse.error.ToString().Should().Be("No mappings provided");
        }

        [Fact]
        public async Task BulkCreateMappings_WithExistingModels_ShouldReturnErrors()
        {
            // Arrange
            var mappings = new List<ModelProviderMapping>
            {
                new() { ModelAlias = "existing", ProviderId = 1, ProviderModelId = "gpt-4-updated", ModelId = 1 }
            };

            var created = new List<ModelProviderMapping>();
            var errors = new List<string> { "Model 'existing' already exists" };

            _mockService.Setup(x => x.CreateBulkMappingsAsync(It.IsAny<IEnumerable<ModelProviderMapping>>()))
                .ReturnsAsync((created, errors));

            // Act
            var result = await _controller.CreateBulkMappings(mappings.Select(m => m.ToDto()).ToList());

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedResponse = Assert.IsType<BulkMappingResult>(okResult.Value);
            returnedResponse.Errors.Should().HaveCount(1);
            returnedResponse.Created.Should().BeEmpty();
        }

        #endregion
    }
}