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
    }
}