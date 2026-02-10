using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;

using FluentAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Moq;

namespace ConduitLLM.Tests.Admin.Controllers
{
    /// <summary>
    /// Get mapping tests for ModelProviderMappingControllerTests
    /// </summary>
    public partial class ModelProviderMappingControllerTests
    {
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
                    ModelProviderTypeAssociationId = 1,
                    ProviderId = 1,
                    ProviderModelId = "gpt-4-turbo",
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
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var returnedMappings = okResult.Value.Should().BeAssignableTo<IEnumerable<ModelProviderMappingDto>>().Subject;
            returnedMappings.Should().HaveCount(2);
            returnedMappings.First().ModelProviderTypeAssociationId.Should().Be(1);
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
            var statusCodeResult = result.Should().BeOfType<ObjectResult>().Subject;
            statusCodeResult.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
            var errorResponse = statusCodeResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
            errorResponse.Code.Should().Be("internal_error");
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
                ModelProviderTypeAssociationId = 1,
                ProviderId = 1,
                ProviderModelId = "gpt-4-turbo"
            };

            _mockService.Setup(x => x.GetMappingByIdAsync(1))
                .ReturnsAsync(mapping);

            // Act
            var result = await _controller.GetMappingById(1);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var returnedMapping = okResult.Value.Should().BeOfType<ModelProviderMappingDto>().Subject;
            returnedMapping.ModelProviderTypeAssociationId.Should().Be(1);
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
            var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
            var errorResponse = notFoundResult.Value.Should().BeOfType<ErrorResponseDto>().Subject;
            errorResponse.Code.Should().Be("not_found");
        }

        #endregion

        #region GetMappingByModelId Tests - REMOVED: Method doesn't exist in controller

        #endregion
    }
}