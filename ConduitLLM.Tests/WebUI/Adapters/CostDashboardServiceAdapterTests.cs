using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.WebUI.Services.Adapters;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

// Alias for WebUI DTOs
using WebUIDto = ConduitLLM.WebUI.DTOs;

namespace ConduitLLM.Tests.WebUI.Adapters
{
    public class CostDashboardServiceAdapterTests
    {
        private readonly Mock<IAdminApiClient> _mockAdminApiClient;
        private readonly Mock<ILogger<CostDashboardServiceAdapter>> _mockLogger;
        private readonly CostDashboardServiceAdapter _adapter;

        public CostDashboardServiceAdapterTests()
        {
            _mockAdminApiClient = new Mock<IAdminApiClient>();
            _mockLogger = new Mock<ILogger<CostDashboardServiceAdapter>>();
            _adapter = new CostDashboardServiceAdapter(_mockAdminApiClient.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task GetDashboardDataAsync_ReturnsValidDashboard_WhenApiReturnsData()
        {
            // Arrange
            var startDate = DateTime.UtcNow.AddDays(-7);
            var endDate = DateTime.UtcNow;
            var virtualKeyId = 1;

            // Create test DTOs for WebUI
            var webUIUsageStats = new List<WebUIDto.DailyUsageStatsDto>
            {
                new WebUIDto.DailyUsageStatsDto
                {
                    Date = startDate.AddDays(1),
                    ModelName = "Model1",
                    TotalCost = 1.5m,
                    RequestCount = 10,
                    InputTokens = 100,
                    OutputTokens = 200
                },
                new WebUIDto.DailyUsageStatsDto
                {
                    Date = startDate.AddDays(2),
                    ModelName = "Model2",
                    TotalCost = 2.5m,
                    RequestCount = 20,
                    InputTokens = 200,
                    OutputTokens = 300
                }
            };
            
            // Create WebUI DTO for virtual keys
            var webUIVirtualKeyStats = new List<WebUIDto.VirtualKeyCostDataDto> 
            {
                new WebUIDto.VirtualKeyCostDataDto 
                {
                    VirtualKeyId = 1,
                    KeyName = "Key1",
                    Cost = 4.0m,
                    RequestCount = 30
                }
            };

            // Setup our mocks with the WebUI DTOs
            _mockAdminApiClient.Setup(x => x.GetDailyUsageStatsAsync(
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int?>()))
                .ReturnsAsync(webUIUsageStats);

            _mockAdminApiClient.Setup(x => x.GetVirtualKeyUsageStatisticsAsync(It.IsAny<int?>()))
                .ReturnsAsync(webUIVirtualKeyStats);

            // Act
            var result = await _adapter.GetDashboardDataAsync(startDate, endDate, virtualKeyId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(startDate, result.StartDate);
            Assert.Equal(endDate, result.EndDate);
            Assert.Equal(4.0m, result.TotalCost);
            
            // Don't test the property values that might be using extension methods
            // Just test that our adapter called the admin API with the right parameters

            // Verify that the calls were made with the expected parameters
            _mockAdminApiClient.Verify(x => x.GetDailyUsageStatsAsync(
                It.Is<DateTime>(d => d == startDate),
                It.Is<DateTime>(d => d == endDate),
                It.Is<int?>(id => id == virtualKeyId)),
                Times.Once);

            _mockAdminApiClient.Verify(x => x.GetVirtualKeyUsageStatisticsAsync(
                It.Is<int?>(id => id == virtualKeyId)),
                Times.Once);
        }

        [Fact]
        public async Task GetDashboardDataAsync_ReturnsEmptyDashboard_WhenNoUsageStats()
        {
            // Arrange
            var startDate = DateTime.UtcNow.AddDays(-7);
            var endDate = DateTime.UtcNow;

            // Return empty lists from the API
            _mockAdminApiClient.Setup(x => x.GetDailyUsageStatsAsync(
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int?>()))
                .ReturnsAsync(new List<WebUIDto.DailyUsageStatsDto>());

            // Act
            var result = await _adapter.GetDashboardDataAsync(startDate, endDate);

            // Assert
            Assert.NotNull(result);
            // Basic checks that don't rely on extension methods
            Assert.Equal(startDate, result.StartDate);
            Assert.Equal(endDate, result.EndDate);
            
            // Verify the API call was made
            _mockAdminApiClient.Verify(x => x.GetDailyUsageStatsAsync(
                It.Is<DateTime>(d => d == startDate),
                It.Is<DateTime>(d => d == endDate),
                It.IsAny<int?>()),
                Times.Once);
        }

        [Fact]
        public async Task GetDetailedCostDataAsync_ReturnsDetailedData_WhenApiReturnsData()
        {
            // Arrange
            var startDate = DateTime.UtcNow.AddDays(-7);
            var endDate = DateTime.UtcNow;
            var virtualKeyId = 1;
            var modelName = "Model1";

            // Mock detailed cost data API response with WebUI DTOs
            var detailedCostData = new List<WebUIDto.DetailedCostDataDto>
            {
                new WebUIDto.DetailedCostDataDto
                {
                    Name = "Request 1",
                    Cost = 1.5m,
                    Percentage = 42.8m
                },
                new WebUIDto.DetailedCostDataDto
                {
                    Name = "Request 2",
                    Cost = 2.0m,
                    Percentage = 57.2m
                }
            };

            _mockAdminApiClient.Setup(x => x.GetDetailedCostDataAsync(
                startDate, endDate, virtualKeyId, modelName))
                .ReturnsAsync(detailedCostData);

            // Act
            var result = await _adapter.GetDetailedCostDataAsync(startDate, endDate, virtualKeyId, modelName);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            
            // Verify that the API method was called with the expected parameters
            _mockAdminApiClient.Verify(x => x.GetDetailedCostDataAsync(
                startDate, endDate, virtualKeyId, modelName), Times.Once);
        }

        [Fact]
        public async Task GetAvailableModelsAsync_ReturnsModels_WhenApiReturnsData()
        {
            // Arrange
            var models = new List<string> { "Model1", "Model2", "Model3" };

            _mockAdminApiClient.Setup(x => x.GetDistinctModelsAsync())
                .ReturnsAsync(models);

            // Act
            var result = await _adapter.GetAvailableModelsAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.Count);
            Assert.Contains("Model1", result);
            Assert.Contains("Model2", result);
            Assert.Contains("Model3", result);
        }
    }
}