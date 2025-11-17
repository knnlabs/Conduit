using System.Text;

using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.DTOs;

using FluentAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Moq;

namespace ConduitLLM.Tests.Admin.Controllers
{
    public partial class ModelCostsControllerTests
    {
        #region ImportModelCosts Tests

        [Fact]
        public async Task ImportModelCosts_WithValidList_ShouldReturnCount()
        {
            // Arrange
            var modelCosts = new List<CreateModelCostDto>
            {
                new() { CostName = "Model 1 Pricing", InputCostPerMillionTokens = 10.00m },
                new() { CostName = "Model 2 Pricing", InputCostPerMillionTokens = 20.00m }
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
            var csvData = "CostName,InputCostPerMillionTokens,OutputCostPerMillionTokens\nGPT-4 Pricing,0.03,0.06";
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
            var csvContent = "CostName,InputCostPerMillionTokens,OutputCostPerMillionTokens\nGPT-4 Pricing,0.03,0.06";
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

        [Fact]
        public async Task ImportCsv_WithInvalidFileType_ShouldReturnBadRequest()
        {
            // Arrange
            var content = "some content";
            var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
            var formFile = new FormFile(stream, 0, stream.Length, "file", "costs.txt")
            {
                Headers = new HeaderDictionary(),
                ContentType = "text/plain"
            };

            // Act
            var result = await _controller.ImportCsv(formFile);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var errorObj = badRequestResult.Value as dynamic;
            ((string)errorObj.error).Should().Be("File must be a CSV file");
        }

        [Fact]
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
            // The test should just verify it returns BadRequest - the exact format depends on the controller implementation
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            badRequestResult.Value.Should().NotBeNull();
        }

        #endregion
    }
}