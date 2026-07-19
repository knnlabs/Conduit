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
            var createdResult = actionResult.Should().BeOfType<CreatedAtActionResult>().Subject;
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
                ProviderId = 1, // same provider as the new mapping — this is the conflict
                ProviderModelId = "gpt-4-old"
            };

            // Mock GetAllMappingsAsync to return existing mapping with same alias AND provider
            _mockService.Setup(x => x.GetAllMappingsAsync())
                .ReturnsAsync(new List<ModelProviderMapping> { existingMapping });

            // Act
            var actionResult = await _controller.CreateMapping(mapping.ToDto());

            // Assert
            var conflictResult = actionResult.Should().BeOfType<ConflictObjectResult>().Subject;
            var errorResponse = conflictResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
            errorResponse.error.ToString().Should().Contain("A mapping for model alias 'existing-model' and this provider already exists");
        }

        [Fact]
        public async Task AddMapping_SameAliasDifferentProvider_IsAllowed_ForFailoverChains()
        {
            // Arrange: alias already mapped to provider 2; the new mapping targets provider 1.
            // Multiple providers per alias form a failover chain and must NOT conflict.
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

            _mockService.Setup(x => x.GetAllMappingsAsync())
                .ReturnsAsync(new List<ModelProviderMapping> { existingMapping });
            _mockService.Setup(x => x.AddMappingAsync(It.IsAny<ModelProviderMapping>()))
                .ReturnsAsync(true);
            _mockService.Setup(x => x.GetMappingByIdAsync(It.IsAny<int>()))
                .ReturnsAsync(mapping);

            // Act
            var actionResult = await _controller.CreateMapping(mapping.ToDto());

            // Assert
            actionResult.Should().NotBeOfType<ConflictObjectResult>();
            _mockService.Verify(x => x.AddMappingAsync(It.IsAny<ModelProviderMapping>()), Times.Once);
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
            var badRequestResult = actionResult.Should().BeOfType<BadRequestObjectResult>().Subject;
            var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
            errorResponse.error.ToString().Should().Contain("Failed to create");
        }

        #endregion
    }
}