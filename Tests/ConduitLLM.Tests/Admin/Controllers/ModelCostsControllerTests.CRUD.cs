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
                new ModelCostsController(null!, _mockValidator.Object, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullValidator_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new ModelCostsController(_mockService.Object, null!, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new ModelCostsController(_mockService.Object, _mockValidator.Object, null!));
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
            var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
            createdResult.ActionName.Should().Be(nameof(ModelCostsController.GetModelCostById));
            createdResult.RouteValues!["id"].Should().Be(10);

            var returnedCost = createdResult.Value.Should().BeOfType<ModelCostDto>().Subject;
            returnedCost.CostName.Should().Be("New Model Pricing");
        }

        [Fact]
        public async Task CreateModelCost_WithDuplicateCostName_ShouldPropagateException()
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
            var act = async () => await _controller.CreateModelCost(createDto);

            // Assert - exception propagates to AdminExceptionMiddleware, which owns error mapping
            await act.Should().ThrowAsync<InvalidOperationException>();
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
            result.Should().BeOfType<NoContentResult>();
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
            var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequestResult.Value.Should().Be("ID in route must match ID in body");
        }

        [Fact]
        public async Task UpdateModelCost_WithNonExistingId_ShouldPropagateException()
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
            var act = async () => await _controller.UpdateModelCost(999, updateDto);

            // Assert - controller throws KeyNotFoundException; AdminExceptionMiddleware maps it to 404
            await act.Should().ThrowAsync<KeyNotFoundException>();
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
            result.Should().BeOfType<NoContentResult>();
        }

        [Fact]
        public async Task DeleteModelCost_WithNonExistingId_ShouldPropagateException()
        {
            // Arrange
            _mockService.Setup(x => x.DeleteModelCostAsync(999))
                .ReturnsAsync(false);

            // Act
            var act = async () => await _controller.DeleteModelCost(999);

            // Assert - controller throws KeyNotFoundException; AdminExceptionMiddleware maps it to 404
            await act.Should().ThrowAsync<KeyNotFoundException>();
        }

        #endregion
    }
}