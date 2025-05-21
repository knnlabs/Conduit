using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.WebUI.Services.Providers;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using CostDTOs = ConduitLLM.Configuration.DTOs.Costs;
using WebUIDto = ConduitLLM.WebUI.DTOs;
using ConduitLLM.Configuration.DTOs.VirtualKey;

namespace ConduitLLM.Tests.WebUI.Providers
{
    public class CostDashboardServiceProviderTests
    {
        private readonly Mock<IAdminApiClient> _mockAdminApiClient;
        private readonly Mock<ILogger<CostDashboardServiceProvider>> _mockLogger;
        private readonly CostDashboardServiceProvider _provider;

        public CostDashboardServiceProviderTests()
        {
            _mockAdminApiClient = new Mock<IAdminApiClient>();
            _mockLogger = new Mock<ILogger<CostDashboardServiceProvider>>();
            _provider = new CostDashboardServiceProvider(_mockAdminApiClient.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task GetDashboardDataAsync_ReturnsValidDashboard_WhenApiReturnsData()
        {
            // Arrange
            var startDate = DateTime.UtcNow.AddDays(-7);
            var endDate = DateTime.UtcNow;
            var virtualKeyId = 1;
            var modelName = "Model1";

            // Create dashboard data to return from the API
            var dashboardData = new CostDTOs.CostDashboardDto
            {
                TotalCost = 4.0m,
                StartDate = startDate,
                EndDate = endDate,
                Last24HoursCost = 1.0m,
                Last7DaysCost = 3.0m,
                Last30DaysCost = 4.0m,
                TopModelsBySpend = new List<CostDTOs.DetailedCostDataDto>
                {
                    new CostDTOs.DetailedCostDataDto { Name = "Model1", Cost = 2.5m, Percentage = 62.5m },
                    new CostDTOs.DetailedCostDataDto { Name = "Model2", Cost = 1.5m, Percentage = 37.5m }
                },
                TopProvidersBySpend = new List<CostDTOs.DetailedCostDataDto>
                {
                    new CostDTOs.DetailedCostDataDto { Name = "Provider1", Cost = 4.0m, Percentage = 100m }
                },
                TopVirtualKeysBySpend = new List<CostDTOs.DetailedCostDataDto>
                {
                    new CostDTOs.DetailedCostDataDto { Name = "Key1", Cost = 4.0m, Percentage = 100m }
                }
            };

            // Setup API client mock to return the dashboard data
            _mockAdminApiClient.Setup(x => x.GetCostDashboardAsync(
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<int?>(), It.IsAny<string>()))
                .ReturnsAsync(dashboardData);

            // Act
            var result = await _provider.GetDashboardDataAsync(startDate, endDate, virtualKeyId, modelName);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(startDate, result.StartDate);
            Assert.Equal(endDate, result.EndDate);
            Assert.Equal(4.0m, result.TotalCost);
            Assert.Equal(1.0m, result.Last24HoursCost);
            Assert.Equal(3.0m, result.Last7DaysCost);
            Assert.Equal(4.0m, result.Last30DaysCost);
            Assert.Equal(2, result.TopModelsBySpend.Count);
            Assert.Equal(1, result.TopProvidersBySpend.Count);
            Assert.Equal(1, result.TopVirtualKeysBySpend.Count);

            // Verify the API was called with the correct parameters
            _mockAdminApiClient.Verify(x => x.GetCostDashboardAsync(
                It.Is<DateTime?>(d => d == startDate),
                It.Is<DateTime?>(d => d == endDate),
                It.Is<int?>(id => id == virtualKeyId),
                It.Is<string>(m => m == modelName)),
                Times.Once);
        }

        [Fact]
        public async Task GetDashboardDataAsync_ReturnsEmptyDashboard_WhenApiReturnsNull()
        {
            // Arrange
            var startDate = DateTime.UtcNow.AddDays(-7);
            var endDate = DateTime.UtcNow;

            // Setup API client mock to return null
            _mockAdminApiClient.Setup(x => x.GetCostDashboardAsync(
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<int?>(), It.IsAny<string>()))
                .ReturnsAsync((CostDTOs.CostDashboardDto)null);

            // Act
            var result = await _provider.GetDashboardDataAsync(startDate, endDate);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(startDate, result.StartDate);
            Assert.Equal(endDate, result.EndDate);
            Assert.Equal(0, result.TotalCost);
            Assert.Equal(0, result.Last24HoursCost);
            Assert.Equal(0, result.Last7DaysCost);
            Assert.Equal(0, result.Last30DaysCost);
            Assert.Empty(result.TopModelsBySpend);
            Assert.Empty(result.TopProvidersBySpend);
            Assert.Empty(result.TopVirtualKeysBySpend);

            // Verify the API call was made
            _mockAdminApiClient.Verify(x => x.GetCostDashboardAsync(
                It.Is<DateTime?>(d => d == startDate),
                It.Is<DateTime?>(d => d == endDate),
                It.IsAny<int?>(),
                It.IsAny<string>()),
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
            var result = await _provider.GetDetailedCostDataAsync(startDate, endDate, virtualKeyId, modelName);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.Equal("Request 1", result[0].Name);
            Assert.Equal(1.5m, result[0].Cost);
            Assert.Equal(42.8m, result[0].Percentage);
            Assert.Equal("Request 2", result[1].Name);
            Assert.Equal(2.0m, result[1].Cost);
            Assert.Equal(57.2m, result[1].Percentage);

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
            var result = await _provider.GetAvailableModelsAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.Count);
            Assert.Contains("Model1", result);
            Assert.Contains("Model2", result);
            Assert.Contains("Model3", result);
        }

        [Fact]
        public async Task GetVirtualKeysAsync_ReturnsVirtualKeys_WhenApiReturnsData()
        {
            // Arrange
            var virtualKeys = new List<VirtualKeyDto>
            {
                new VirtualKeyDto { Id = 1, KeyName = "Key1" },
                new VirtualKeyDto { Id = 2, KeyName = "Key2" }
            };

            _mockAdminApiClient.Setup(x => x.GetAllVirtualKeysAsync())
                .ReturnsAsync(virtualKeys);

            // Act
            var result = await _provider.GetVirtualKeysAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.Equal(1, result[0].Id);
            Assert.Equal("Key1", result[0].KeyName);
            Assert.Equal(2, result[1].Id);
            Assert.Equal("Key2", result[1].KeyName);
        }
    }
}