using ConduitLLM.Admin.Controllers;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Extensions;

using FluentAssertions;

using Microsoft.AspNetCore.Mvc;

using Moq;

namespace ConduitLLM.Tests.Admin.Controllers
{
    /// <summary>
    /// Add mapping tests for ModelProviderMappingControllerTests
    /// </summary>
    public partial class ModelProviderMappingControllerTests
    {
        #region AddMapping Tests

        [Fact]
        public async Task AddMapping_WithValidMapping_ShouldReturnCreated()
        {
            // Arrange
            var mapping = new ModelProviderMapping
            {
                ModelAlias = "new-model",
                ModelProviderTypeAssociationId = 1,
                ProviderId = 1,
                ProviderModelId = "gpt-4-new"
            };

            var createdMapping = new ModelProviderMapping
            {
                Id = 123,
                ModelAlias = "new-model",
                ModelProviderTypeAssociationId = 1,
                ProviderId = 1,
                ProviderModelId = "gpt-4-new"
            };

            // Setup GetAllMappingsAsync to return empty list (no duplicates)
            _mockService.Setup(x => x.GetAllMappingsAsync())
                .ReturnsAsync(new List<ModelProviderMapping>());

            // Setup AddMappingAsync to set the ID and return true
            _mockService.Setup(x => x.AddMappingAsync(It.IsAny<ModelProviderMapping>()))
                .Callback<ModelProviderMapping>(m => m.Id = 123)
                .ReturnsAsync(true);
            
            _mockService.Setup(x => x.GetMappingByIdAsync(It.IsAny<int>()))
                .ReturnsAsync(createdMapping);

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
                ModelProviderTypeAssociationId = 1,
                ProviderId = 1,
                ProviderModelId = "gpt-4"
            };

            var existingMapping = new ModelProviderMapping
            {
                Id = 100,
                ModelAlias = "existing-model",
                ModelProviderTypeAssociationId = 2,
                ProviderId = 2,
                ProviderModelId = "gpt-4-old"
            };

            // Mock GetAllMappingsAsync to return existing mapping with same alias
            _mockService.Setup(x => x.GetAllMappingsAsync())
                .ReturnsAsync(new List<ModelProviderMapping> { existingMapping });

            // Act
            var actionResult = await _controller.CreateMapping(mapping.ToDto());

            // Assert
            var conflictResult = Assert.IsType<ConflictObjectResult>(actionResult);
            var errorResponse = Assert.IsType<ErrorResponseDto>(conflictResult.Value);
            errorResponse.error.ToString().Should().Contain("A mapping for model alias 'existing-model' already exists");
        }

        [Fact]
        public async Task AddMapping_WithInvalidProviderId_ShouldReturnBadRequest()
        {
            // Arrange
            var mapping = new ModelProviderMapping
            {
                ModelAlias = "new-model",
                ModelProviderTypeAssociationId = 1,
                ProviderId = 999, // Invalid provider
                ProviderModelId = "gpt-4"
            };

            // Setup GetAllMappingsAsync to return empty list (no duplicates)
            _mockService.Setup(x => x.GetAllMappingsAsync())
                .ReturnsAsync(new List<ModelProviderMapping>());

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
    }
}