using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;

using FluentAssertions;

using Microsoft.AspNetCore.Mvc;

using Moq;

namespace ConduitLLM.Tests.Admin.Controllers
{
    /// <summary>
    /// Delete mapping tests for ModelProviderMappingControllerTests
    /// </summary>
    public partial class ModelProviderMappingControllerTests
    {
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
    }
}