using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Tests.Admin.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

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
    }
}