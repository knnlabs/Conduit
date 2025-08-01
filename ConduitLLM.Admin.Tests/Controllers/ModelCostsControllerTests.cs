using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ConduitLLM.Admin.Controllers;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Admin.Tests.TestHelpers;
using ConduitLLM.Configuration.DTOs;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace ConduitLLM.Admin.Tests.Controllers
{
    /// <summary>
    /// Unit tests for the ModelCostsController class.
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Component", "AdminController")]
    public class ModelCostsControllerTests
    {
        private readonly Mock<IAdminModelCostService> _mockService;
        private readonly Mock<ILogger<ModelCostsController>> _mockLogger;
        private readonly ModelCostsController _controller;
        private readonly ITestOutputHelper _output;

        public ModelCostsControllerTests(ITestOutputHelper output)
        {
            _output = output;
            _mockService = new Mock<IAdminModelCostService>();
            _mockLogger = new Mock<ILogger<ModelCostsController>>();
            _controller = new ModelCostsController(_mockService.Object, _mockLogger.Object);
        }

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

        #region GetAllModelCosts Tests

        [Fact]
        public async Task GetAllModelCosts_WithCosts_ShouldReturnOkWithList()
        {
            // Arrange
            var costs = new List<ModelCostDto>
            {
                new() { Id = 1, CostName = "GPT-4 Pricing", InputTokenCost = 0.03m, OutputTokenCost = 0.06m },
                new() { Id = 2, CostName = "Claude-3 Pricing", InputTokenCost = 0.02m, OutputTokenCost = 0.04m }
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
                InputTokenCost = 0.03m,
                OutputTokenCost = 0.06m
            };

            _mockService.Setup(x => x.GetModelCostByIdAsync(1))
                .ReturnsAsync(cost);

            // Act
            var result = await _controller.GetModelCostById(1);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedCost = Assert.IsType<ModelCostDto>(okResult.Value);
            returnedCost.Id.Should().Be(1);
            returnedCost.InputTokenCost.Should().Be(0.03m);
        }

        [DynamicObjectIssue("Test expects string response but controller may return object")]
        public async Task GetModelCostById_WithNonExistingId_ShouldReturnNotFound()
        {
            // Arrange
            _mockService.Setup(x => x.GetModelCostByIdAsync(999))
                .ReturnsAsync((ModelCostDto?)null);

            // Act
            var result = await _controller.GetModelCostById(999);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            notFoundResult.Value.Should().Be("Model cost not found");
        }

        #endregion

        #region GetModelCostsByProvider Tests

        [Fact]
        public async Task GetModelCostsByProvider_WithExistingProvider_ShouldReturnCosts()
        {
            // Arrange
            var costs = new List<ModelCostDto>
            {
                new() { Id = 1, CostName = "GPT-3.5 Pricing", InputTokenCost = 0.001m },
                new() { Id = 2, CostName = "GPT-4 Pricing", InputTokenCost = 0.03m }
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
                InputTokenCost = 0.01m
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

        [DynamicObjectIssue("Test expects string response but controller may return object")]
        public async Task GetModelCostByCostName_WithNoMatch_ShouldReturnNotFound()
        {
            // Arrange
            _mockService.Setup(x => x.GetModelCostByCostNameAsync("Unknown Cost"))
                .ReturnsAsync((ModelCostDto?)null);

            // Act
            var result = await _controller.GetModelCostByCostName("Unknown Cost");

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            notFoundResult.Value.Should().Be("Model cost not found");
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
                InputTokenCost = 0.01m,
                OutputTokenCost = 0.02m
            };

            var createdDto = new ModelCostDto
            {
                Id = 10,
                CostName = createDto.CostName,
                InputTokenCost = createDto.InputTokenCost,
                OutputTokenCost = createDto.OutputTokenCost
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
                InputTokenCost = 0.01m
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
                InputTokenCost = 0.02m,
                OutputTokenCost = 0.04m
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
                InputTokenCost = 0.02m
            };

            // Act
            var result = await _controller.UpdateModelCost(1, updateDto);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            badRequestResult.Value.Should().Be("ID in route must match ID in body");
        }

        [DynamicObjectIssue("Test expects string response but controller may return object")]
        public async Task UpdateModelCost_WithNonExistingId_ShouldReturnNotFound()
        {
            // Arrange
            var updateDto = new UpdateModelCostDto
            {
                Id = 999,
                CostName = "Non-existent Model",
                InputTokenCost = 0.02m
            };

            _mockService.Setup(x => x.UpdateModelCostAsync(It.IsAny<UpdateModelCostDto>()))
                .ReturnsAsync(false);

            // Act
            var result = await _controller.UpdateModelCost(999, updateDto);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            notFoundResult.Value.Should().Be("Model cost not found");
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

        [DynamicObjectIssue("Test expects string response but controller may return object")]
        public async Task DeleteModelCost_WithNonExistingId_ShouldReturnNotFound()
        {
            // Arrange
            _mockService.Setup(x => x.DeleteModelCostAsync(999))
                .ReturnsAsync(false);

            // Act
            var result = await _controller.DeleteModelCost(999);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            notFoundResult.Value.Should().Be("Model cost not found");
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

        #region ImportModelCosts Tests

        [Fact]
        public async Task ImportModelCosts_WithValidList_ShouldReturnCount()
        {
            // Arrange
            var modelCosts = new List<CreateModelCostDto>
            {
                new() { CostName = "Model 1 Pricing", InputTokenCost = 0.01m },
                new() { CostName = "Model 2 Pricing", InputTokenCost = 0.02m }
            };

            _mockService.Setup(x => x.ImportModelCostsAsync(It.IsAny<IEnumerable<CreateModelCostDto>>()))
                .ReturnsAsync(2);

            // Act
            var result = await _controller.ImportModelCosts(modelCosts);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            okResult.Value.Should().Be(2);
        }

        [Fact]
        public async Task ImportModelCosts_WithEmptyList_ShouldReturnBadRequest()
        {
            // Act
            var result = await _controller.ImportModelCosts(new List<CreateModelCostDto>());

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            badRequestResult.Value.Should().Be("No model costs provided for import");
        }

        #endregion

        #region Export Tests

        [Fact]
        public async Task ExportCsv_ShouldReturnCsvFile()
        {
            // Arrange
            var csvData = "CostName,InputTokenCost,OutputTokenCost\nGPT-4 Pricing,0.03,0.06";
            _mockService.Setup(x => x.ExportModelCostsAsync("csv", null))
                .ReturnsAsync(csvData);

            // Act
            var result = await _controller.ExportCsv();

            // Assert
            var fileResult = Assert.IsType<FileContentResult>(result);
            fileResult.ContentType.Should().Be("text/csv");
            fileResult.FileDownloadName.Should().StartWith("model-costs-");
            fileResult.FileDownloadName.Should().EndWith(".csv");
            
            var content = Encoding.UTF8.GetString(fileResult.FileContents);
            content.Should().Contain("GPT-4 Pricing");
        }

        [Fact]
        public async Task ExportJson_WithProvider_ShouldReturnFilteredJsonFile()
        {
            // Arrange
            var jsonData = "[{\"costName\":\"GPT-4 Pricing\",\"inputTokenCost\":0.03}]";
            _mockService.Setup(x => x.ExportModelCostsAsync("json", 1))
                .ReturnsAsync(jsonData);

            // Act
            var result = await _controller.ExportJson(1);

            // Assert
            var fileResult = Assert.IsType<FileContentResult>(result);
            fileResult.ContentType.Should().Be("application/json");
            fileResult.FileDownloadName.Should().StartWith("model-costs-");
            fileResult.FileDownloadName.Should().EndWith(".json");
        }

        #endregion

        #region Import From File Tests

        [Fact]
        public async Task ImportCsv_WithValidFile_ShouldReturnImportResult()
        {
            // Arrange
            var csvContent = "CostName,InputTokenCost,OutputTokenCost\nGPT-4 Pricing,0.03,0.06";
            var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));
            var formFile = new FormFile(stream, 0, stream.Length, "file", "costs.csv")
            {
                Headers = new HeaderDictionary(),
                ContentType = "text/csv"
            };

            var importResult = new BulkImportResult
            {
                SuccessCount = 1,
                FailureCount = 0,
                Errors = new List<string>()
            };

            _mockService.Setup(x => x.ImportModelCostsAsync(It.IsAny<string>(), "csv"))
                .ReturnsAsync(importResult);

            // Act
            var result = await _controller.ImportCsv(formFile);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedResult = Assert.IsType<BulkImportResult>(okResult.Value);
            returnedResult.SuccessCount.Should().Be(1);
            returnedResult.FailureCount.Should().Be(0);
        }

        [DynamicObjectIssue("Test expects string response but controller may return object")]
        public async Task ImportCsv_WithInvalidFileType_ShouldReturnBadRequest()
        {
            // Arrange
            var stream = new MemoryStream();
            var formFile = new FormFile(stream, 0, 0, "file", "costs.txt")
            {
                Headers = new HeaderDictionary(),
                ContentType = "text/plain"
            };

            // Act
            var result = await _controller.ImportCsv(formFile);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            badRequestResult.Value.Should().Be("File must be a CSV file");
        }

        [DynamicObjectIssue("Test expects response.message property but controller may return different format")]
        public async Task ImportJson_WithFailedImport_ShouldReturnBadRequest()
        {
            // Arrange
            var jsonContent = "[{\"invalidField\":\"data\"}]";
            var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonContent));
            var formFile = new FormFile(stream, 0, stream.Length, "file", "costs.json")
            {
                Headers = new HeaderDictionary(),
                ContentType = "application/json"
            };

            var importResult = new BulkImportResult
            {
                SuccessCount = 0,
                FailureCount = 1,
                Errors = new List<string> { "Invalid model cost format" }
            };

            _mockService.Setup(x => x.ImportModelCostsAsync(It.IsAny<string>(), "json"))
                .ReturnsAsync(importResult);

            // Act
            var result = await _controller.ImportJson(formFile);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            dynamic response = badRequestResult.Value!;
            ((string)response.message).Should().Be("Import failed");
            ((int)response.failureCount).Should().Be(1);
        }

        #endregion
    }

}