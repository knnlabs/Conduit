using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.WebUI.Services.Adapters;
using ConduitLLM.Tests.Extensions;
using ConduitLLM.Tests.WebUI.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

// Use type aliases to avoid ambiguity
using DailyUsageStatsDto = ConduitLLM.Configuration.DTOs.DailyUsageStatsDto;
using CostsDashboardDto = ConduitLLM.Configuration.DTOs.Costs.CostDashboardDto;
using CostsDetailedCostDataDto = ConduitLLM.Configuration.DTOs.Costs.DetailedCostDataDto;

namespace ConduitLLM.Tests.RepositoryServices
{
    public class CostDashboardServiceTests
    {
        private readonly Mock<IAdminApiClient> _mockAdminApiClient;
        private readonly Mock<ILogger<CostDashboardServiceAdapter>> _mockLogger;
        private readonly CostDashboardServiceAdapter _service;

        public CostDashboardServiceTests()
        {
            _mockAdminApiClient = new Mock<IAdminApiClient>();
            _mockLogger = new Mock<ILogger<CostDashboardServiceAdapter>>();
            _service = new CostDashboardServiceAdapter(_mockAdminApiClient.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task GetDashboardDataAsync_ShouldReturnDashboardData_WithCorrectDateRange()
        {
            // Arrange
            var startDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var endDate = new DateTime(2025, 1, 31, 23, 59, 59, DateTimeKind.Utc);
            
            // Mock cost dashboard data
            var mockDashboardData = new CostsDashboardDto
            {
                StartDate = startDate,
                EndDate = endDate,
                TotalCost = 0.045m,
                TimeFrame = "custom",
                TopModelsBySpend = new List<CostsDetailedCostDataDto>
                {
                    new CostsDetailedCostDataDto
                    {
                        Name = "gpt-4",
                        Cost = 0.025m,
                        Percentage = 55.6m
                    },
                    new CostsDetailedCostDataDto
                    {
                        Name = "claude-v1",
                        Cost = 0.02m,
                        Percentage = 44.4m
                    }
                },
                TopVirtualKeysBySpend = new List<CostsDetailedCostDataDto>
                {
                    new CostsDetailedCostDataDto
                    {
                        Name = "Test Key 1",
                        Cost = 0.025m,
                        Percentage = 55.6m
                    },
                    new CostsDetailedCostDataDto
                    {
                        Name = "Test Key 2",
                        Cost = 0.02m,
                        Percentage = 44.4m
                    }
                }
            };
            
            // Mock daily usage stats for cost trends
            var usageStats = new List<DailyUsageStatsDto>
            {
                new DailyUsageStatsDto
                {
                    Date = new DateTime(2025, 1, 5, 12, 0, 0, DateTimeKind.Utc),
                    ModelName = "gpt-4",
                    RequestCount = 1,
                    InputTokens = 100,
                    OutputTokens = 50,
                    TotalCost = 0.01m
                },
                new DailyUsageStatsDto
                {
                    Date = new DateTime(2025, 1, 10, 14, 0, 0, DateTimeKind.Utc),
                    ModelName = "claude-v1",
                    RequestCount = 1,
                    InputTokens = 200,
                    OutputTokens = 100,
                    TotalCost = 0.02m
                },
                new DailyUsageStatsDto
                {
                    Date = new DateTime(2025, 1, 15, 10, 0, 0, DateTimeKind.Utc),
                    ModelName = "gpt-4",
                    RequestCount = 1,
                    InputTokens = 150,
                    OutputTokens = 75,
                    TotalCost = 0.015m
                }
            };
            
            // Setup mocks
            _mockAdminApiClient.Setup(api => api.GetCostDashboardAsync(
                startDate, endDate, null, null))
                .ReturnsAsync(mockDashboardData);
                
            // Setup daily usage statistics
            // We need to convert our Configuration.DTOs.DailyUsageStatsDto to WebUI.DTOs.DailyUsageStatsDto
            var webUIDailyUsageStats = usageStats.Select(dto => new ConduitLLM.WebUI.DTOs.DailyUsageStatsDto
            {
                Date = dto.Date,
                ModelName = dto.ModelName,
                RequestCount = dto.RequestCount,
                InputTokens = dto.InputTokens,
                OutputTokens = dto.OutputTokens,
                Cost = dto.TotalCost
            }).ToList();
            
            _mockAdminApiClient.Setup(api => api.GetDailyUsageStatsAsync(
                startDate, endDate, null))
                .ReturnsAsync(webUIDailyUsageStats);
            
            // Act
            var result = await _service.GetDashboardDataAsync(startDate, endDate);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(startDate, result.StartDate);
            Assert.Equal(endDate, result.EndDate);
            Assert.Equal(0.045m, result.TotalCost);
            
            // Verify model data
            Assert.Equal(2, result.TopModelsBySpend.Count);
            var gpt4Data = result.TopModelsBySpend.FirstOrDefault(m => m.Name == "gpt-4");
            Assert.NotNull(gpt4Data);
            Assert.Equal(0.025m, gpt4Data.Cost);
            Assert.Equal(55.6m, gpt4Data.Percentage);
            
            // Verify virtual key data
            Assert.Equal(2, result.TopVirtualKeysBySpend.Count);
            var key1Data = result.TopVirtualKeysBySpend.FirstOrDefault(k => k.Name == "Test Key 1");
            Assert.NotNull(key1Data);
            Assert.Equal(0.025m, key1Data.Cost);
            Assert.Equal(55.6m, key1Data.Percentage);
        }

        [Fact]
        public async Task GetDashboardDataAsync_ShouldFilterByVirtualKeyId()
        {
            // Arrange
            var startDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var endDate = new DateTime(2025, 1, 31, 23, 59, 59, DateTimeKind.Utc);
            int virtualKeyId = 101;
            
            // Mock cost dashboard data for a specific virtual key
            var mockDashboardData = new CostsDashboardDto
            {
                StartDate = startDate,
                EndDate = endDate,
                TotalCost = 0.025m,
                TimeFrame = "custom",
                TopModelsBySpend = new List<CostsDetailedCostDataDto>
                {
                    new CostsDetailedCostDataDto
                    {
                        Name = "gpt-4",
                        Cost = 0.025m,
                        Percentage = 100m
                    }
                },
                TopVirtualKeysBySpend = new List<CostsDetailedCostDataDto>
                {
                    new CostsDetailedCostDataDto
                    {
                        Name = "Test Key 1",
                        Cost = 0.025m,
                        Percentage = 100m
                    }
                }
            };
            
            // Setup mocks with proper parameter matching
            _mockAdminApiClient.Setup(api => api.GetCostDashboardAsync(
                It.Is<DateTime?>(d => d == startDate),
                It.Is<DateTime?>(d => d == endDate),
                It.Is<int?>(id => id == virtualKeyId),
                It.IsAny<string>()))
                .ReturnsAsync(mockDashboardData);
            
            // Setup virtual key statistics
            var virtualKeyStats = new List<ConduitLLM.WebUI.DTOs.VirtualKeyCostDataDto>
            {
                new ConduitLLM.WebUI.DTOs.VirtualKeyCostDataDto
                {
                    VirtualKeyId = virtualKeyId,
                    KeyName = "Test Key 1",
                    Cost = 0.025m,
                    RequestCount = 1
                }
            };
            
            _mockAdminApiClient.Setup(api => api.GetVirtualKeyUsageStatisticsAsync(
                It.Is<int?>(id => id == virtualKeyId)))
                .ReturnsAsync(virtualKeyStats);
                
            // Also need to setup daily usage stats for this VK ID
            var dailyStats = new List<ConduitLLM.WebUI.DTOs.DailyUsageStatsDto>
            {
                new ConduitLLM.WebUI.DTOs.DailyUsageStatsDto
                {
                    Date = startDate.AddDays(5),
                    ModelName = "gpt-4",
                    RequestCount = 1,
                    InputTokens = 100,
                    OutputTokens = 50,
                    Cost = 0.025m
                }
            };
            
            _mockAdminApiClient.Setup(api => api.GetDailyUsageStatsAsync(
                It.Is<DateTime>(d => d == startDate),
                It.Is<DateTime>(d => d == endDate),
                It.Is<int?>(id => id == virtualKeyId)))
                .ReturnsAsync(dailyStats);
            
            // Act
            var result = await _service.GetDashboardDataAsync(startDate, endDate, virtualKeyId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(0.025m, result.TotalCost);
            
            // Check model data - should only include one model
            Assert.Single(result.TopModelsBySpend);
            Assert.Equal("gpt-4", result.TopModelsBySpend[0].Name);
            
            // Check virtual key data - should only include one key
            Assert.Single(result.TopVirtualKeysBySpend);
            Assert.Equal("Test Key 1", result.TopVirtualKeysBySpend[0].Name);
        }

        [Fact]
        public async Task GetDashboardDataAsync_ShouldFilterByModelName()
        {
            // Arrange
            var startDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var endDate = new DateTime(2025, 1, 31, 23, 59, 59, DateTimeKind.Utc);
            string modelName = "gpt-4";
            
            // Mock cost dashboard data filtered by model
            var mockDashboardData = new CostsDashboardDto
            {
                StartDate = startDate,
                EndDate = endDate,
                TotalCost = 0.025m,
                TimeFrame = "custom",
                TopModelsBySpend = new List<CostsDetailedCostDataDto>
                {
                    new CostsDetailedCostDataDto
                    {
                        Name = "gpt-4",
                        Cost = 0.025m,
                        Percentage = 100m
                    }
                },
                TopVirtualKeysBySpend = new List<CostsDetailedCostDataDto>
                {
                    new CostsDetailedCostDataDto
                    {
                        Name = "Test Key 1",
                        Cost = 0.025m,
                        Percentage = 100m
                    }
                }
            };
            
            // Setup mocks with proper parameter matching
            _mockAdminApiClient.Setup(api => api.GetCostDashboardAsync(
                It.Is<DateTime?>(d => d == startDate),
                It.Is<DateTime?>(d => d == endDate),
                It.IsAny<int?>(),
                It.Is<string?>(m => m == modelName)))
                .ReturnsAsync(mockDashboardData);
            
            // Setup virtual key statistics
            var virtualKeyStats = new List<ConduitLLM.WebUI.DTOs.VirtualKeyCostDataDto>
            {
                new ConduitLLM.WebUI.DTOs.VirtualKeyCostDataDto
                {
                    VirtualKeyId = 1,
                    KeyName = "Test Key 1",
                    Cost = 0.025m,
                    RequestCount = 1
                }
            };
            
            _mockAdminApiClient.Setup(api => api.GetVirtualKeyUsageStatisticsAsync(null))
                .ReturnsAsync(virtualKeyStats);
            
            // Setup WebUI DailyUsageStats data
            var webUIDailyUsageStats = new List<ConduitLLM.WebUI.DTOs.DailyUsageStatsDto>
            {
                new ConduitLLM.WebUI.DTOs.DailyUsageStatsDto
                {
                    Date = startDate.AddDays(5),
                    ModelName = modelName,
                    RequestCount = 1,
                    InputTokens = 100,
                    OutputTokens = 50,
                    Cost = 0.025m
                }
            };
            
            _mockAdminApiClient.Setup(api => api.GetDailyUsageStatsAsync(startDate, endDate, null))
                .ReturnsAsync(webUIDailyUsageStats);
                
            // Act
            var result = await _service.GetDashboardDataAsync(startDate, endDate, null, modelName);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(0.025m, result.TotalCost);
            
            // Check model data - should only include one model
            Assert.Single(result.TopModelsBySpend);
            Assert.Equal("gpt-4", result.TopModelsBySpend[0].Name);
        }

        [Fact]
        public async Task GetAvailableModelsAsync_ShouldReturnDistinctModels()
        {
            // Arrange
            var models = new List<string> { "gpt-4", "claude-v1", "gemini-pro" };
            
            _mockAdminApiClient.Setup(api => api.GetDistinctModelsAsync())
                .ReturnsAsync(models);
            
            // Act
            var result = await _service.GetAvailableModelsAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.Count);
            Assert.Contains("gpt-4", result);
            Assert.Contains("claude-v1", result);
            Assert.Contains("gemini-pro", result);
        }

        [Fact]
        public async Task GetDetailedCostDataAsync_ShouldReturnGroupedDetailedData()
        {
            // Arrange
            var startDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var endDate = new DateTime(2025, 1, 31, 23, 59, 59, DateTimeKind.Utc);
            
            // Mock detailed cost data
            var detailedCostData = new List<CostsDetailedCostDataDto>
            {
                new CostsDetailedCostDataDto
                {
                    Name = "gpt-4",
                    Cost = 0.025m,
                    Percentage = 55.6m
                },
                new CostsDetailedCostDataDto
                {
                    Name = "claude-v1",
                    Cost = 0.02m,
                    Percentage = 44.4m
                }
            };
            
            // Convert from Config DTOs to WebUI DTOs for the Setup
            var webUIDetailedCostData = detailedCostData.Select(d => new ConduitLLM.WebUI.DTOs.DetailedCostDataDto
            {
                Name = d.Name,
                Cost = d.Cost,
                Percentage = d.Percentage
            }).ToList();
            
            // Use ReturnsAsync for proper async mock behavior
            _mockAdminApiClient.Setup(api => api.GetDetailedCostDataAsync(
                startDate, endDate, null, null))
                .ReturnsAsync(webUIDetailedCostData);
            
            // Act
            var result = await _service.GetDetailedCostDataAsync(startDate, endDate);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            
            // Check result contains expected data
            var gpt4Data = result.FirstOrDefault(d => d.Name == "gpt-4");
            Assert.NotNull(gpt4Data);
            Assert.Equal(0.025m, gpt4Data.Cost);
            Assert.Equal(55.6m, gpt4Data.Percentage);
            
            var claudeData = result.FirstOrDefault(d => d.Name == "claude-v1");
            Assert.NotNull(claudeData);
            Assert.Equal(0.02m, claudeData.Cost);
            Assert.Equal(44.4m, claudeData.Percentage);
        }
    }
}