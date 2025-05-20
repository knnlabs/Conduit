using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.DTOs.VirtualKey;
using ConduitLLM.Tests.Extensions;
using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.WebUI.Services.Adapters;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

// We'll use fully qualified names instead of namespace aliases to avoid conflicts

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

            var usageStats = new List<ConduitLLM.Configuration.DTOs.DailyUsageStatsDto>
            {
                new ConduitLLM.Configuration.DTOs.DailyUsageStatsDto
                {
                    Date = startDate.AddDays(1),
                    ModelName = "Model1",
                    TotalCost = 1.5m,
                    RequestCount = 10,
                    InputTokens = 100,
                    OutputTokens = 200
                },
                new DailyUsageStatsDto
                {
                    Date = startDate.AddDays(2),
                    ModelName = "Model2",
                    TotalCost = 2.5m,
                    RequestCount = 20,
                    InputTokens = 200,
                    OutputTokens = 300
                }
            };

            var virtualKeyStats = new List<ConduitLLM.Configuration.DTOs.VirtualKeyCostDataDto>
            {
                new ConduitLLM.Configuration.DTOs.VirtualKeyCostDataDto
                {
                    VirtualKeyId = 1,
                    VirtualKeyName = "Key1",
                    TotalCost = 4.0m,
                    RequestCount = 30
                }
            };

            _mockAdminApiClient.Setup(x => x.GetDailyUsageStatsAsync(
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int?>()))
                .Returns(Task.FromResult<IEnumerable<ConduitLLM.Configuration.DTOs.DailyUsageStatsDto>>(usageStats));

            _mockAdminApiClient.Setup(x => x.GetVirtualKeyUsageStatisticsAsync(It.IsAny<int?>()))
                .ReturnsAsync(virtualKeyStats);

            // Act
            var result = await _adapter.GetDashboardDataAsync(startDate, endDate, virtualKeyId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(startDate, result.StartDate);
            Assert.Equal(endDate, result.EndDate);
            Assert.Equal(4.0m, result.TotalCost);
            Assert.Equal(30, result.TotalRequests);
            Assert.Equal(300, result.TotalInputTokens);
            Assert.Equal(500, result.TotalOutputTokens);
            
            // Verify cost trends
            Assert.Equal(2, result.CostTrends.Count);
            var firstTrend = result.CostTrends.First();
            Assert.Equal(startDate.AddDays(1).Date, firstTrend.Date);
            Assert.Equal(1.5m, firstTrend.Cost);
            
            // Verify cost by model
            Assert.Equal(2, result.CostByModel.Count);
            var topModel = result.CostByModel.First();
            Assert.Equal("Model2", topModel.ModelName);
            Assert.Equal(2.5m, topModel.Cost);
            
            // Verify cost by virtual key
            Assert.Single(result.CostByVirtualKey);
            var virtualKeyData = result.CostByVirtualKey.First();
            Assert.Equal(1, virtualKeyData.VirtualKeyId);
            Assert.Equal("Key1", virtualKeyData.VirtualKeyName);
            Assert.Equal(4.0m, virtualKeyData.Cost);
        }

        [Fact]
        public async Task GetDashboardDataAsync_ReturnsEmptyDashboard_WhenNoUsageStats()
        {
            // Arrange
            var startDate = DateTime.UtcNow.AddDays(-7);
            var endDate = DateTime.UtcNow;

            _mockAdminApiClient.Setup(x => x.GetDailyUsageStatsAsync(
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int?>()))
                .ReturnsAsync(new List<DailyUsageStatsDto>());

            // Act
            var result = await _adapter.GetDashboardDataAsync(startDate, endDate);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(startDate, result.StartDate);
            Assert.Equal(endDate, result.EndDate);
            Assert.Equal(0, result.TotalCost);
            Assert.Equal(0, result.TotalRequests);
            Assert.Equal(0, result.TotalInputTokens);
            Assert.Equal(0, result.TotalOutputTokens);
            Assert.Empty(result.CostTrends);
            Assert.Empty(result.CostByModel);
            Assert.Empty(result.CostByVirtualKey);
        }

        [Fact]
        public async Task GetDashboardDataAsync_FiltersModelCorrectly_WhenModelNameProvided()
        {
            // Arrange
            var startDate = DateTime.UtcNow.AddDays(-7);
            var endDate = DateTime.UtcNow;
            var modelName = "Model1";

            var usageStats = new List<ConduitLLM.Configuration.DTOs.DailyUsageStatsDto>
            {
                new ConduitLLM.Configuration.DTOs.DailyUsageStatsDto
                {
                    Date = startDate.AddDays(1),
                    ModelName = "Model1",
                    TotalCost = 1.5m,
                    RequestCount = 10,
                    InputTokens = 100,
                    OutputTokens = 200
                },
                new DailyUsageStatsDto
                {
                    Date = startDate.AddDays(2),
                    ModelName = "Model2",
                    TotalCost = 2.5m,
                    RequestCount = 20,
                    InputTokens = 200,
                    OutputTokens = 300
                }
            };

            var virtualKeyStats = new List<ConduitLLM.Configuration.DTOs.VirtualKeyCostDataDto>
            {
                new ConduitLLM.Configuration.DTOs.VirtualKeyCostDataDto
                {
                    VirtualKeyId = 1,
                    VirtualKeyName = "Key1",
                    TotalCost = 1.5m,
                    RequestCount = 10
                }
            };

            _mockAdminApiClient.Setup(x => x.GetDailyUsageStatsAsync(
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int?>()))
                .Returns(Task.FromResult<IEnumerable<ConduitLLM.Configuration.DTOs.DailyUsageStatsDto>>(usageStats));

            _mockAdminApiClient.Setup(x => x.GetVirtualKeyUsageStatisticsAsync(It.IsAny<int?>()))
                .ReturnsAsync(virtualKeyStats);

            // Act
            var result = await _adapter.GetDashboardDataAsync(startDate, endDate, null, modelName);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result.CostByModel);
            Assert.Equal("Model1", result.CostByModel.First().ModelName);
            Assert.Equal(1.5m, result.TotalCost);
            Assert.Equal(10, result.TotalRequests);
        }

        [Fact]
        public async Task GetDashboardDataAsync_UsesDefaultDates_WhenDatesNotProvided()
        {
            // Arrange
            var usageStats = new List<ConduitLLM.Configuration.DTOs.DailyUsageStatsDto>
            {
                new ConduitLLM.Configuration.DTOs.DailyUsageStatsDto
                {
                    Date = DateTime.UtcNow.AddDays(-1),
                    ModelName = "Model1",
                    TotalCost = 1.5m,
                    RequestCount = 10,
                    InputTokens = 100,
                    OutputTokens = 200
                }
            };

            var virtualKeyStats = new List<ConduitLLM.Configuration.DTOs.VirtualKeyCostDataDto>
            {
                new ConduitLLM.Configuration.DTOs.VirtualKeyCostDataDto
                {
                    VirtualKeyId = 1,
                    VirtualKeyName = "Key1",
                    TotalCost = 1.5m,
                    RequestCount = 10
                }
            };

            _mockAdminApiClient.Setup(x => x.GetDailyUsageStatsAsync(
                It.Is<DateTime>(d => d <= DateTime.UtcNow.AddDays(-29) && d >= DateTime.UtcNow.AddDays(-31)),
                It.Is<DateTime>(d => d >= DateTime.UtcNow.AddDays(-1) && d <= DateTime.UtcNow.AddDays(1)),
                It.IsAny<int?>()))
                .ReturnsAsync(usageStats);

            _mockAdminApiClient.Setup(x => x.GetVirtualKeyUsageStatisticsAsync(It.IsAny<int?>()))
                .ReturnsAsync(virtualKeyStats);

            // Act
            var result = await _adapter.GetDashboardDataAsync(null, null);

            // Assert
            Assert.NotNull(result);
            // Default start date should be 30 days ago
            Assert.True(result.StartDate <= DateTime.UtcNow.AddDays(-29) && result.StartDate >= DateTime.UtcNow.AddDays(-31));
            // Default end date should be today
            Assert.True(result.EndDate >= DateTime.UtcNow.AddDays(-1) && result.EndDate <= DateTime.UtcNow.AddDays(1));
        }

        [Fact]
        public async Task GetDashboardDataAsync_HandlesExceptions_AndReturnsEmptyDashboard()
        {
            // Arrange
            var startDate = DateTime.UtcNow.AddDays(-7);
            var endDate = DateTime.UtcNow;

            _mockAdminApiClient.Setup(x => x.GetDailyUsageStatsAsync(
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int?>()))
                .ThrowsAsync(new Exception("Test exception"));

            // Act
            var result = await _adapter.GetDashboardDataAsync(startDate, endDate);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(0, result.TotalCost);
            Assert.Equal(0, result.TotalRequests);
            
            // Verify logging
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => true),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GetVirtualKeysAsync_ReturnsVirtualKeys_WhenApiReturnsData()
        {
            // Arrange
            var virtualKeyDtos = new List<ConduitLLM.Configuration.DTOs.VirtualKey.VirtualKeyDto>
            {
                new ConduitLLM.Configuration.DTOs.VirtualKey.VirtualKeyDto
                {
                    Id = 1,
                    Name = "Key1",
                    Description = "Description1",
                    IsActive = true,
                    UsageLimit = 100,
                    RateLimit = 10
                },
                new ConduitLLM.Configuration.DTOs.VirtualKey.VirtualKeyDto
                {
                    Id = 2,
                    Name = "Key2",
                    Description = "Description2",
                    IsActive = false,
                    UsageLimit = 200,
                    RateLimit = 20
                }
            };

            _mockAdminApiClient.Setup(x => x.GetAllVirtualKeysAsync())
                .ReturnsAsync(virtualKeyDtos);

            // Act
            var result = await _adapter.GetVirtualKeysAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            
            var firstKey = result.First();
            Assert.Equal(1, firstKey.Id);
            Assert.Equal("Key1", firstKey.Name);
            Assert.Equal("Description1", firstKey.Description);
            Assert.True(firstKey.IsActive);
            Assert.Equal(100, firstKey.UsageLimit);
            Assert.Equal(10, firstKey.RateLimit);
        }

        [Fact]
        public async Task GetVirtualKeysAsync_ReturnsEmptyList_WhenApiThrowsException()
        {
            // Arrange
            _mockAdminApiClient.Setup(x => x.GetAllVirtualKeysAsync())
                .ThrowsAsync(new Exception("Test exception"));

            // Act
            var result = await _adapter.GetVirtualKeysAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
            
            // Verify logging
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => true),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
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

        [Fact]
        public async Task GetAvailableModelsAsync_ReturnsEmptyList_WhenApiThrowsException()
        {
            // Arrange
            _mockAdminApiClient.Setup(x => x.GetDistinctModelsAsync())
                .ThrowsAsync(new Exception("Test exception"));

            // Act
            var result = await _adapter.GetAvailableModelsAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
            
            // Verify logging
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => true),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GetDetailedCostDataAsync_ReturnsDetailedData_WhenApiReturnsLogs()
        {
            // Arrange
            var startDate = DateTime.UtcNow.AddDays(-7);
            var endDate = DateTime.UtcNow;
            var virtualKeyId = 1;
            var modelName = "Model1";

            var logs = new ConfigDTOs.PagedResult<ConfigDTOs.RequestLogDto>
            {
                Items = new List<ConfigDTOs.RequestLogDto>
                {
                    new ConfigDTOs.RequestLogDto
                    {
                        Id = 1,
                        Timestamp = startDate.AddDays(1),
                        VirtualKeyId = 1,
                        VirtualKeyName = "Key1",
                        ModelId = "Model1",
                        InputTokens = 100,
                        OutputTokens = 200,
                        Cost = 1.5m,
                        ResponseTimeMs = 500,
                        StatusCode = 200
                    },
                    new ConfigDTOs.RequestLogDto
                    {
                        Id = 2,
                        Timestamp = startDate.AddDays(2),
                        VirtualKeyId = 1,
                        VirtualKeyName = "Key1",
                        ModelId = "Model1",
                        InputTokens = 150,
                        OutputTokens = 250,
                        Cost = 2.0m,
                        ResponseTimeMs = 600,
                        StatusCode = 500
                    }
                },
                Page = 1,
                PageSize = 20,
                TotalCount = 2,
                TotalPages = 1
            };

            _mockAdminApiClient.Setup(x => x.GetRequestLogsAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int?>(), 
                It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync(logs);

            // Act
            var result = await _adapter.GetDetailedCostDataAsync(startDate, endDate, virtualKeyId, modelName);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            
            var firstLog = result[0];
            Assert.Equal(1, firstLog.VirtualKeyId);
            Assert.Equal("Key1", firstLog.VirtualKeyName);
            Assert.Equal("Model1", firstLog.ModelName);
            Assert.Equal(100, firstLog.InputTokens);
            Assert.Equal(200, firstLog.OutputTokens);
            Assert.Equal(300, firstLog.TotalTokens);
            Assert.Equal(1.5m, firstLog.Cost);
            Assert.Equal(500, firstLog.LatencyMs);
            Assert.True(firstLog.Success);
            Assert.Null(firstLog.ErrorMessage);
            
            var secondLog = result[1];
            Assert.False(secondLog.Success);
            Assert.Equal("Error message", secondLog.ErrorMessage);
        }

        [Fact]
        public async Task GetDetailedCostDataAsync_ReturnsEmptyList_WhenApiReturnsNoLogs()
        {
            // Arrange
            var startDate = DateTime.UtcNow.AddDays(-7);
            var endDate = DateTime.UtcNow;

            _mockAdminApiClient.Setup(x => x.GetRequestLogsAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int?>(),
                It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync(new ConduitLLM.Configuration.DTOs.PagedResult<ConduitLLM.Configuration.DTOs.RequestLogDto>
                {
                    Items = new List<ConduitLLM.Configuration.DTOs.RequestLogDto>(),
                    Page = 1,
                    PageSize = 20,
                    TotalCount = 0,
                    TotalPages = 0
                });

            // Act
            var result = await _adapter.GetDetailedCostDataAsync(startDate, endDate);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetDetailedCostDataAsync_ReturnsEmptyList_WhenApiThrowsException()
        {
            // Arrange
            var startDate = DateTime.UtcNow.AddDays(-7);
            var endDate = DateTime.UtcNow;

            _mockAdminApiClient.Setup(x => x.GetRequestLogsAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int?>(),
                It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .ThrowsAsync(new Exception("Test exception"));

            // Act
            var result = await _adapter.GetDetailedCostDataAsync(startDate, endDate);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
            
            // Verify logging
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => true),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
    }
}