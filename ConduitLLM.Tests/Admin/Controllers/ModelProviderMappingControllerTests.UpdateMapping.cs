using System.Threading.Tasks;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Extensions;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

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
    }
}