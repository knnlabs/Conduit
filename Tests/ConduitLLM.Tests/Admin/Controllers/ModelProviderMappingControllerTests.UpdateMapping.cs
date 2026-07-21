using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Extensions;

using FluentAssertions;

using Microsoft.AspNetCore.Mvc;

using Moq;

namespace ConduitLLM.Tests.Admin.Controllers
{
    /// <summary>
    /// Update mapping tests for ModelProviderMappingControllerTests
    /// </summary>
    public partial class ModelProviderMappingControllerTests
    {
        #region UpdateMapping Tests

        [Fact]
        public async Task UpdateMapping_WithValidMapping_ShouldReturnNoContent()
        {
            // Arrange
            var mapping = new ModelProviderMapping
            {
                Id = 1,
                ModelAlias = "gpt-4",
                ModelProviderTypeAssociationId = 1,
                ProviderId = 1,
                ProviderModelId = "gpt-4-turbo-updated"
            };

            // Mock that the mapping exists
            _mockService.Setup(x => x.GetMappingByIdAsync(1))
                .ReturnsAsync(mapping);

            _mockService.Setup(x => x.UpdateMappingAsync(It.IsAny<ModelProviderMapping>()))
                .ReturnsAsync(true);

            // Act
            var actionResult = await _controller.UpdateMapping(1, ToUpdateRequest(mapping));

            // Assert
            actionResult.Should().BeOfType<NoContentResult>();
        }

        [Fact]
        public async Task UpdateMapping_WithNonExistingId_ShouldPropagateException()
        {
            // Arrange
            var mapping = new ModelProviderMapping
            {
                Id = 999,
                ModelAlias = "gpt-4",
                ModelProviderTypeAssociationId = 1,
                ProviderId = 1,
                ProviderModelId = "gpt-4"
            };

            // Mock that the mapping doesn't exist
            _mockService.Setup(x => x.GetMappingByIdAsync(999))
                .ReturnsAsync((ModelProviderMapping?)null);

            // Act & Assert — not-found now throws KeyNotFoundException, mapped in AdminExceptionMiddleware
            var act = async () => await _controller.UpdateMapping(999, ToUpdateRequest(mapping));
            await act.Should().ThrowAsync<KeyNotFoundException>();
        }

        #endregion

        private static UpdateModelProviderMappingDto ToUpdateRequest(ModelProviderMapping mapping) => new()
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
