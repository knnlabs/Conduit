using ConduitLLM.Tests.Admin.TestHelpers;
using ConduitLLM.Configuration.DTOs;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace ConduitLLM.Tests.Admin.Controllers
{
    public partial class ModelCostsControllerTests
    {
        #region GetAllModelCosts Tests

        [Fact]
        public async Task GetAllModelCosts_WithCosts_ShouldReturnOkWithList()
        {
            // Arrange
            var costs = new List<ModelCostDto>
            {
                new() { Id = 1, CostName = "GPT-4 Pricing", InputCostPerMillionTokens = 30.00m, OutputCostPerMillionTokens = 60.00m },
                new() { Id = 2, CostName = "Claude-3 Pricing", InputCostPerMillionTokens = 20.00m, OutputCostPerMillionTokens = 40.00m }
            };

            _mockService.Setup(x => x.GetAllModelCostsAsync())
                .ReturnsAsync(costs);

            // Act
            var result = await _controller.GetAllModelCosts();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedCosts = Assert.IsAssignableFrom<IEnumerable<ModelCostDto>>(okResult.Value);
            returnedCosts.Should().HaveCount(2);
            returnedCosts.First().CostName.Should().Be("GPT-4 Pricing");
        }

        [Fact]
        public async Task GetAllModelCosts_WithPagination_ShouldReturnPaginatedResponse()
        {
            // Arrange
            var costs = new List<ModelCostDto>();
            for (int i = 1; i <= 25; i++)
            {
                costs.Add(new() { Id = i, CostName = $"Model-{i} Pricing", InputCostPerMillionTokens = i * 10m, OutputCostPerMillionTokens = i * 20m });
            }

            _mockService.Setup(x => x.GetAllModelCostsAsync())
                .ReturnsAsync(costs);

            // Act
            var result = await _controller.GetAllModelCosts(page: 2, pageSize: 10);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            okResult.Value.Should().NotBeNull();
            
            // Use reflection to access anonymous type properties
            var responseType = okResult.Value!.GetType();
            var totalCount = (int)responseType.GetProperty("totalCount")!.GetValue(okResult.Value)!;
            var page = (int)responseType.GetProperty("page")!.GetValue(okResult.Value)!;
            var pageSize = (int)responseType.GetProperty("pageSize")!.GetValue(okResult.Value)!;
            var totalPages = (int)responseType.GetProperty("totalPages")!.GetValue(okResult.Value)!;
            var items = responseType.GetProperty("items")!.GetValue(okResult.Value) as IEnumerable<ModelCostDto>;
            
            // Verify pagination metadata
            totalCount.Should().Be(25);
            page.Should().Be(2);
            pageSize.Should().Be(10);
            totalPages.Should().Be(3);
            
            // Verify items
            items.Should().NotBeNull();
            items!.Count().Should().Be(10);
            items!.First().Id.Should().Be(11); // First item on page 2
            items!.Last().Id.Should().Be(20);  // Last item on page 2
        }

        [Fact]
        public async Task GetAllModelCosts_WithPaginationLastPage_ShouldReturnPartialPage()
        {
            // Arrange
            var costs = new List<ModelCostDto>();
            for (int i = 1; i <= 25; i++)
            {
                costs.Add(new() { Id = i, CostName = $"Model-{i} Pricing", InputCostPerMillionTokens = i * 10m, OutputCostPerMillionTokens = i * 20m });
            }

            _mockService.Setup(x => x.GetAllModelCostsAsync())
                .ReturnsAsync(costs);

            // Act
            var result = await _controller.GetAllModelCosts(page: 3, pageSize: 10);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            okResult.Value.Should().NotBeNull();
            
            // Use reflection to access anonymous type properties
            var responseType = okResult.Value!.GetType();
            var totalCount = (int)responseType.GetProperty("totalCount")!.GetValue(okResult.Value)!;
            var page = (int)responseType.GetProperty("page")!.GetValue(okResult.Value)!;
            var pageSize = (int)responseType.GetProperty("pageSize")!.GetValue(okResult.Value)!;
            var totalPages = (int)responseType.GetProperty("totalPages")!.GetValue(okResult.Value)!;
            var items = responseType.GetProperty("items")!.GetValue(okResult.Value) as IEnumerable<ModelCostDto>;
            
            // Verify pagination metadata
            totalCount.Should().Be(25);
            page.Should().Be(3);
            pageSize.Should().Be(10);
            totalPages.Should().Be(3);
            
            // Verify items - should only have 5 items on last page
            items.Should().NotBeNull();
            items!.Count().Should().Be(5);
            items!.First().Id.Should().Be(21); // First item on page 3
            items!.Last().Id.Should().Be(25);  // Last item
        }

        [Fact]
        public async Task GetAllModelCosts_WithOnlyPageParameter_ShouldReturnAllItems()
        {
            // Arrange
            var costs = new List<ModelCostDto>
            {
                new() { Id = 1, CostName = "GPT-4 Pricing", InputCostPerMillionTokens = 30.00m, OutputCostPerMillionTokens = 60.00m },
                new() { Id = 2, CostName = "Claude-3 Pricing", InputCostPerMillionTokens = 20.00m, OutputCostPerMillionTokens = 40.00m }
            };

            _mockService.Setup(x => x.GetAllModelCostsAsync())
                .ReturnsAsync(costs);

            // Act - Only page provided, no pageSize
            var result = await _controller.GetAllModelCosts(page: 1, pageSize: null);

            // Assert - Should return all items without pagination
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedCosts = Assert.IsAssignableFrom<IEnumerable<ModelCostDto>>(okResult.Value);
            returnedCosts.Should().HaveCount(2);
        }

        [Fact]
        public async Task GetAllModelCosts_WithException_ShouldReturn500()
        {
            // Arrange
            _mockService.Setup(x => x.GetAllModelCostsAsync())
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _controller.GetAllModelCosts();

            // Assert
            var statusCodeResult = Assert.IsType<ObjectResult>(result);
            statusCodeResult.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
            
            _mockLogger.VerifyLogWithAnyException(LogLevel.Error, "Error getting all model costs");
        }

        #endregion

        #region GetModelCostById Tests

        [Fact]
        public async Task GetModelCostById_WithExistingId_ShouldReturnOkWithCost()
        {
            // Arrange
            var cost = new ModelCostDto
            {
                Id = 1,
                CostName = "GPT-4 Pricing",
                InputCostPerMillionTokens = 30.00m,
                OutputCostPerMillionTokens = 60.00m
            };

            _mockService.Setup(x => x.GetModelCostByIdAsync(1))
                .ReturnsAsync(cost);

            // Act
            var result = await _controller.GetModelCostById(1);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedCost = Assert.IsType<ModelCostDto>(okResult.Value);
            returnedCost.Id.Should().Be(1);
            returnedCost.InputCostPerMillionTokens.Should().Be(30.00m);
        }

        [Fact]
        public async Task GetModelCostById_WithNonExistingId_ShouldReturnNotFound()
        {
            // Arrange
            _mockService.Setup(x => x.GetModelCostByIdAsync(999))
                .ReturnsAsync((ModelCostDto?)null);

            // Act
            var result = await _controller.GetModelCostById(999);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            var errorObj = notFoundResult.Value as dynamic;
            ((string)errorObj.error).Should().Be("Model cost not found");
        }

        #endregion

        #region GetModelCostsByProvider Tests

        [Fact]
        public async Task GetModelCostsByProvider_WithExistingProvider_ShouldReturnCosts()
        {
            // Arrange
            var costs = new List<ModelCostDto>
            {
                new() { Id = 1, CostName = "GPT-3.5 Pricing", InputCostPerMillionTokens = 1.00m },
                new() { Id = 2, CostName = "GPT-4 Pricing", InputCostPerMillionTokens = 30.00m }
            };

            _mockService.Setup(x => x.GetModelCostsByProviderAsync(1))
                .ReturnsAsync(costs);

            // Act
            var result = await _controller.GetModelCostsByProvider(1);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedCosts = Assert.IsAssignableFrom<IEnumerable<ModelCostDto>>(okResult.Value);
            returnedCosts.Should().HaveCount(2);
        }

        [Fact]
        public async Task GetModelCostsByProvider_WithEmptyProvider_ShouldReturnEmptyList()
        {
            // Arrange
            _mockService.Setup(x => x.GetModelCostsByProviderAsync(999))
                .ReturnsAsync(new List<ModelCostDto>());

            // Act
            var result = await _controller.GetModelCostsByProvider(999);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedCosts = Assert.IsAssignableFrom<IEnumerable<ModelCostDto>>(okResult.Value);
            returnedCosts.Should().BeEmpty();
        }

        #endregion

        #region GetModelCostByCostName Tests

        [Fact]
        public async Task GetModelCostByCostName_WithMatchingCostName_ShouldReturnCost()
        {
            // Arrange
            var cost = new ModelCostDto
            {
                Id = 1,
                CostName = "GPT-4 Turbo Pricing",
                InputCostPerMillionTokens = 10.00m
            };

            _mockService.Setup(x => x.GetModelCostByCostNameAsync("GPT-4 Turbo Pricing"))
                .ReturnsAsync(cost);

            // Act
            var result = await _controller.GetModelCostByCostName("GPT-4 Turbo Pricing");

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedCost = Assert.IsType<ModelCostDto>(okResult.Value);
            returnedCost.CostName.Should().Be("GPT-4 Turbo Pricing");
        }

        [Fact]
        public async Task GetModelCostByCostName_WithNoMatch_ShouldReturnNotFound()
        {
            // Arrange
            _mockService.Setup(x => x.GetModelCostByCostNameAsync("Unknown Cost"))
                .ReturnsAsync((ModelCostDto?)null);

            // Act
            var result = await _controller.GetModelCostByCostName("Unknown Cost");

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            var errorObj = notFoundResult.Value as dynamic;
            ((string)errorObj.error).Should().Be("Model cost not found");
        }

        #endregion

        #region GetModelCostOverview Tests

        [Fact]
        public async Task GetModelCostOverview_WithValidDates_ShouldReturnOverview()
        {
            // Arrange
            var startDate = new DateTime(2024, 1, 1);
            var endDate = new DateTime(2024, 1, 31);
            
            var overview = new List<ModelCostOverviewDto>
            {
                new() { Model = "gpt-4", TotalCost = 150.50m, RequestCount = 1000 },
                new() { Model = "gpt-3.5", TotalCost = 200.75m, RequestCount = 1500 }
            };

            _mockService.Setup(x => x.GetModelCostOverviewAsync(startDate, endDate))
                .ReturnsAsync(overview);

            // Act
            var result = await _controller.GetModelCostOverview(startDate, endDate);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedOverview = Assert.IsAssignableFrom<IEnumerable<ModelCostOverviewDto>>(okResult.Value);
            returnedOverview.Should().HaveCount(2);
            returnedOverview.Sum(o => o.TotalCost).Should().Be(351.25m);
        }

        [Fact]
        public async Task GetModelCostOverview_WithInvalidDates_ShouldReturnBadRequest()
        {
            // Arrange
            var startDate = new DateTime(2024, 2, 1);
            var endDate = new DateTime(2024, 1, 1); // End before start

            // Act
            var result = await _controller.GetModelCostOverview(startDate, endDate);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            badRequestResult.Value.Should().Be("Start date cannot be after end date");
        }

        #endregion
    }
}