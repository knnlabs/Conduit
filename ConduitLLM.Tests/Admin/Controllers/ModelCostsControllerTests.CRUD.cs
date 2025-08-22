using ConduitLLM.Admin.Controllers;
using ConduitLLM.Configuration.DTOs;

using FluentAssertions;

using Microsoft.AspNetCore.Mvc;

using Moq;

namespace ConduitLLM.Tests.Admin.Controllers
{
    public partial class ModelCostsControllerTests
    {
        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullService_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new ModelCostsController(null!, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new ModelCostsController(_mockService.Object, null!));
        }

        #endregion

        #region CreateModelCost Tests

        [Fact]
        public async Task CreateModelCost_WithValidData_ShouldReturnCreated()
        {
            // Arrange
            var createDto = new CreateModelCostDto
            {
                CostName = "New Model Pricing",
                InputCostPerMillionTokens = 10.00m,
                OutputCostPerMillionTokens = 20.00m
            };

            var createdDto = new ModelCostDto
            {
                Id = 10,
                CostName = createDto.CostName,
                InputCostPerMillionTokens = createDto.InputCostPerMillionTokens,
                OutputCostPerMillionTokens = createDto.OutputCostPerMillionTokens
            };

            _mockService.Setup(x => x.CreateModelCostAsync(It.IsAny<CreateModelCostDto>()))
                .ReturnsAsync(createdDto);

            // Act
            var result = await _controller.CreateModelCost(createDto);

            // Assert
            var createdResult = Assert.IsType<CreatedAtActionResult>(result);
            createdResult.ActionName.Should().Be(nameof(ModelCostsController.GetModelCostById));
            createdResult.RouteValues!["id"].Should().Be(10);
            
            var returnedCost = Assert.IsType<ModelCostDto>(createdResult.Value);
            returnedCost.CostName.Should().Be("New Model Pricing");
        }

        [Fact]
        public async Task CreateModelCost_WithDuplicateCostName_ShouldReturnBadRequest()
        {
            // Arrange
            var createDto = new CreateModelCostDto
            {
                CostName = "Existing Cost",
                InputCostPerMillionTokens = 10.00m
            };

            _mockService.Setup(x => x.CreateModelCostAsync(It.IsAny<CreateModelCostDto>()))
                .ThrowsAsync(new InvalidOperationException("Model cost with this name already exists"));

            // Act
            var result = await _controller.CreateModelCost(createDto);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            badRequestResult.Value.Should().Be("Model cost with this name already exists");
        }

        #endregion

        #region UpdateModelCost Tests

        [Fact]
        public async Task UpdateModelCost_WithValidData_ShouldReturnNoContent()
        {
            // Arrange
            var updateDto = new UpdateModelCostDto
            {
                Id = 1,
                CostName = "GPT-4 Updated Pricing",
                InputCostPerMillionTokens = 20.00m,
                OutputCostPerMillionTokens = 40.00m
            };

            _mockService.Setup(x => x.UpdateModelCostAsync(It.IsAny<UpdateModelCostDto>()))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.UpdateModelCost(1, updateDto);

            // Assert
            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task UpdateModelCost_WithMismatchedIds_ShouldReturnBadRequest()
        {
            // Arrange
            var updateDto = new UpdateModelCostDto
            {
                Id = 2,
                CostName = "Model Pricing",
                InputCostPerMillionTokens = 20.00m
            };

            // Act
            var result = await _controller.UpdateModelCost(1, updateDto);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            badRequestResult.Value.Should().Be("ID in route must match ID in body");
        }

        [Fact]
        public async Task UpdateModelCost_WithNonExistingId_ShouldReturnNotFound()
        {
            // Arrange
            var updateDto = new UpdateModelCostDto
            {
                Id = 999,
                CostName = "Non-existent Model",
                InputCostPerMillionTokens = 20.00m
            };

            _mockService.Setup(x => x.UpdateModelCostAsync(It.IsAny<UpdateModelCostDto>()))
                .ReturnsAsync(false);

            // Act
            var result = await _controller.UpdateModelCost(999, updateDto);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            var errorObj = notFoundResult.Value as dynamic;
            ((string)errorObj.error).Should().Be("Model cost not found");
        }

        #endregion

        #region DeleteModelCost Tests

        [Fact]
        public async Task DeleteModelCost_WithExistingId_ShouldReturnNoContent()
        {
            // Arrange
            _mockService.Setup(x => x.DeleteModelCostAsync(1))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.DeleteModelCost(1);

            // Assert
            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task DeleteModelCost_WithNonExistingId_ShouldReturnNotFound()
        {
            // Arrange
            _mockService.Setup(x => x.DeleteModelCostAsync(999))
                .ReturnsAsync(false);

            // Act
            var result = await _controller.DeleteModelCost(999);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            var errorObj = notFoundResult.Value as dynamic;
            ((string)errorObj.error).Should().Be("Model cost not found");
        }

        #endregion
    }
}