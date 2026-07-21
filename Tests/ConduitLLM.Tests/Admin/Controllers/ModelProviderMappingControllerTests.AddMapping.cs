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
            var actionResult = await _controller.CreateMapping(ToCreateRequest(mapping));

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
                ProviderId = 1,
                ProviderModelId = "gpt-4-old"
            };

            // Mock GetAllMappingsAsync to return existing mapping with same alias
            _mockService.Setup(x => x.GetAllMappingsAsync())
                .ReturnsAsync(new List<ModelProviderMapping> { existingMapping });

            // Act
            var actionResult = await _controller.CreateMapping(ToCreateRequest(mapping));

            // Assert
            var conflictResult = actionResult.Should().BeOfType<ConflictObjectResult>().Subject;
            var errorResponse = conflictResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
            errorResponse.error.ToString().Should().Contain("A mapping for alias 'existing-model' and provider 1 already exists");
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
            var actionResult = await _controller.CreateMapping(ToCreateRequest(mapping));

            // Assert
            var badRequestResult = actionResult.Should().BeOfType<BadRequestObjectResult>().Subject;
            var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
            errorResponse.error.ToString().Should().Contain("Failed to create");
        }

        [Fact]
        public async Task AddMapping_WithRequiredFieldsOnly_ShouldApplyCreateDefaults()
        {
            ModelProviderMapping? captured = null;
            _mockService.Setup(x => x.GetAllMappingsAsync()).ReturnsAsync([]);
            _mockService.Setup(x => x.AddMappingAsync(It.IsAny<ModelProviderMapping>()))
                .Callback<ModelProviderMapping>(mapping => { captured = mapping; mapping.Id = 42; })
                .ReturnsAsync(true);
            _mockService.Setup(x => x.GetMappingByIdAsync(42)).ReturnsAsync(() => captured);

            var result = await _controller.CreateMapping(new CreateModelProviderMappingDto
            {
                ModelAlias = "defaulted",
                ProviderId = 3,
                ProviderModelId = "provider/defaulted",
                ModelProviderTypeAssociationId = 9
            });

            result.Should().BeOfType<CreatedAtActionResult>();
            captured.Should().NotBeNull();
            captured!.IsEnabled.Should().BeTrue();
            captured.RoutingPriority.Should().Be(0);
            captured.RoutingWeight.Should().Be(1.0m);
        }

        #endregion

        private static CreateModelProviderMappingDto ToCreateRequest(ModelProviderMapping mapping) => new()
        {
            ModelAlias = mapping.ModelAlias,
            ProviderId = mapping.ProviderId,
            ProviderModelId = mapping.ProviderModelId,
            ModelProviderTypeAssociationId = mapping.ModelProviderTypeAssociationId,
            IsEnabled = mapping.IsEnabled,
            Priority = mapping.RoutingPriority,
            Weight = mapping.RoutingWeight,
            ProviderOptions = mapping.ProviderOptions
        };
    }
}
