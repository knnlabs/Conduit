using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ConduitLLM.Admin.Services;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.DTOs.Costs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;

namespace ConduitLLM.Tests.Admin.Services
{
    public class AnalyticsServiceTests
    {
        private readonly Mock<IRequestLogRepository> _mockRequestLogRepository;
        private readonly Mock<IVirtualKeyRepository> _mockVirtualKeyRepository;
        private readonly IMemoryCache _memoryCache;
        private readonly Mock<ILogger<AnalyticsService>> _mockLogger;
        private readonly AnalyticsService _service;

        public AnalyticsServiceTests()
        {
            _mockRequestLogRepository = new Mock<IRequestLogRepository>();
            _mockVirtualKeyRepository = new Mock<IVirtualKeyRepository>();
            _memoryCache = new MemoryCache(new MemoryCacheOptions());
            _mockLogger = new Mock<ILogger<AnalyticsService>>();
            
            _service = new AnalyticsService(
                _mockRequestLogRepository.Object,
                _mockVirtualKeyRepository.Object,
                _memoryCache,
                _mockLogger.Object
            );
        }

        #region GetLogsAsync Tests

        [Fact]
        public async Task GetLogsAsync_ReturnsPagedResults()
        {
            // Arrange
            var testLogs = GenerateTestLogs(25);
            // Return only the first 10 items for page 1, pageSize 10
            var pagedLogs = testLogs.Take(10).ToList();
            _mockRequestLogRepository
                .Setup(x => x.GetByDateRangePaginatedAsync(
                    It.IsAny<DateTime>(), 
                    It.IsAny<DateTime>(), 
                    It.IsAny<int>(), 
                    It.IsAny<int>(), 
                    default(CancellationToken)))
                .ReturnsAsync((pagedLogs, 25));

            // Act
            var result = await _service.GetLogsAsync(page: 1, pageSize: 10);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1, result.Page);
            Assert.Equal(10, result.PageSize);
            Assert.Equal(25, result.TotalItems);
            Assert.Equal(3, result.TotalPages);
            Assert.Equal(10, result.Items.Count);
        }

        [Fact]
        public async Task GetLogsAsync_FiltersById()
        {
            // Arrange
            var testLogs = GenerateTestLogs(10);
            testLogs[3].VirtualKeyId = 999;
            
            _mockRequestLogRepository
                .Setup(x => x.GetByDateRangePaginatedAsync(
                    It.IsAny<DateTime>(), 
                    It.IsAny<DateTime>(), 
                    It.IsAny<int>(), 
                    It.IsAny<int>(), 
                    default(CancellationToken)))
                .ReturnsAsync((testLogs, 10));

            // Act
            var result = await _service.GetLogsAsync(virtualKeyId: 999);

            // Assert
            Assert.Single(result.Items);
            Assert.Equal(999, result.Items.First().VirtualKeyId);
        }

        [Fact]
        public async Task GetLogsAsync_FiltersById_Model()
        {
            // Arrange
            var testLogs = new List<RequestLog>
            {
                new() { Id = 1, ModelName = "gpt-3.5-turbo", VirtualKeyId = 1, Timestamp = DateTime.UtcNow },
                new() { Id = 2, ModelName = "claude-3", VirtualKeyId = 1, Timestamp = DateTime.UtcNow },
                new() { Id = 3, ModelName = "gpt-4-turbo", VirtualKeyId = 1, Timestamp = DateTime.UtcNow },
                new() { Id = 4, ModelName = "gpt-4", VirtualKeyId = 1, Timestamp = DateTime.UtcNow },
                new() { Id = 5, ModelName = "claude-2", VirtualKeyId = 1, Timestamp = DateTime.UtcNow }
            };
            
            _mockRequestLogRepository
                .Setup(x => x.GetByDateRangePaginatedAsync(
                    It.IsAny<DateTime>(), 
                    It.IsAny<DateTime>(), 
                    It.IsAny<int>(), 
                    It.IsAny<int>(), 
                    default(CancellationToken)))
                .ReturnsAsync((testLogs, 5));

            // Act
            var result = await _service.GetLogsAsync(model: "gpt-4");

            // Assert
            Assert.Equal(2, result.Items.Count); // Should match both "gpt-4" and "gpt-4-turbo"
            Assert.All(result.Items, item => Assert.Contains("gpt-4", item.ModelName));
        }

        #endregion

        #region GetLogByIdAsync Tests

        [Fact]
        public async Task GetLogByIdAsync_ReturnsLog_WhenExists()
        {
            // Arrange
            var testLog = new RequestLog
            {
                Id = 123,
                VirtualKeyId = 1,
                ModelName = "gpt-4",
                Cost = 0.05m,
                Timestamp = DateTime.UtcNow
            };
            
            _mockRequestLogRepository
                .Setup(x => x.GetByIdAsync(123, It.IsAny<CancellationToken>()))
                .ReturnsAsync(testLog);

            // Act
            var result = await _service.GetLogByIdAsync(123);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(123, result.Id);
            Assert.Equal("gpt-4", result.ModelName);
        }

        [Fact]
        public async Task GetLogByIdAsync_ReturnsNull_WhenNotExists()
        {
            // Arrange
            _mockRequestLogRepository
                .Setup(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((RequestLog?)null);

            // Act
            var result = await _service.GetLogByIdAsync(999);

            // Assert
            Assert.Null(result);
        }

        #endregion

        #region GetDistinctModelsAsync Tests

        [Fact]
        public async Task GetDistinctModelsAsync_ReturnsUniqueModels()
        {
            // Arrange
            var testLogs = new List<RequestLog>
            {
                new() { ModelName = "gpt-4" },
                new() { ModelName = "gpt-3.5-turbo" },
                new() { ModelName = "gpt-4" }, // Duplicate
                new() { ModelName = "claude-3" }
            };
            
            _mockRequestLogRepository
                .Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(testLogs);

            // Act
            var result = await _service.GetDistinctModelsAsync();

            // Assert
            var models = result.ToList();
            Assert.Equal(3, models.Count);
            Assert.Contains("gpt-4", models);
            Assert.Contains("gpt-3.5-turbo", models);
            Assert.Contains("claude-3", models);
        }

        [Fact]
        public async Task GetDistinctModelsAsync_UsesCaching()
        {
            // Arrange
            var testLogs = new List<RequestLog>
            {
                new() { ModelName = "gpt-4" }
            };
            
            _mockRequestLogRepository
                .Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(testLogs);

            // Act - Call twice
            var result1 = await _service.GetDistinctModelsAsync();
            var result2 = await _service.GetDistinctModelsAsync();

            // Assert - Repository should only be called once due to caching
            _mockRequestLogRepository.Verify(x => x.GetAllAsync(It.IsAny<CancellationToken>()), Times.Once);
            Assert.Equal(result1, result2);
        }

        #endregion

        #region GetCostSummaryAsync Tests

        [Fact]
        public async Task GetCostSummaryAsync_CalculatesTotals()
        {
            // Arrange
            var testLogs = new List<RequestLog>
            {
                new() { 
                    ModelName = "gpt-4", 
                    Cost = 0.05m, 
                    Timestamp = DateTime.UtcNow.AddHours(-12), // Within last 24 hours
                    InputTokens = 100,
                    OutputTokens = 50
                },
                new() { 
                    ModelName = "gpt-3.5-turbo", 
                    Cost = 0.02m, 
                    Timestamp = DateTime.UtcNow.AddDays(-2),
                    InputTokens = 200,
                    OutputTokens = 100
                }
            };
            
            var virtualKeys = new List<VirtualKey>
            {
                new() { Id = 1, KeyName = "Test Key 1" }
            };
            
            _mockRequestLogRepository
                .Setup(x => x.GetByDateRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(testLogs);
            
            _mockVirtualKeyRepository
                .Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(virtualKeys);

            // Act
            var result = await _service.GetCostSummaryAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(0.07m, result.TotalCost);
            Assert.True(result.Last24HoursCost > 0);
            Assert.NotEmpty(result.TopModelsBySpend);
        }

        [Fact]
        public async Task GetCostSummaryAsync_GroupsByModel()
        {
            // Arrange
            var testLogs = new List<RequestLog>
            {
                new() { ModelName = "gpt-4", Cost = 0.05m, Timestamp = DateTime.UtcNow },
                new() { ModelName = "gpt-4", Cost = 0.03m, Timestamp = DateTime.UtcNow },
                new() { ModelName = "claude-3", Cost = 0.02m, Timestamp = DateTime.UtcNow }
            };
            
            _mockRequestLogRepository
                .Setup(x => x.GetByDateRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(testLogs);
            
            _mockVirtualKeyRepository
                .Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<VirtualKey>());

            // Act
            var result = await _service.GetCostSummaryAsync();

            // Assert
            var gpt4Cost = result.TopModelsBySpend.FirstOrDefault(m => m.Name == "gpt-4");
            Assert.NotNull(gpt4Cost);
            Assert.Equal(0.08m, gpt4Cost.Cost);
            Assert.Equal(2, gpt4Cost.RequestCount);
        }

        #endregion

        #region GetAnalyticsSummaryAsync Tests

        [Fact]
        public async Task GetAnalyticsSummaryAsync_CalculatesMetrics()
        {
            // Arrange
            var testLogs = new List<RequestLog>
            {
                new() { 
                    ModelName = "gpt-4", 
                    Cost = 0.05m,
                    InputTokens = 100,
                    OutputTokens = 50,
                    ResponseTimeMs = 1500,
                    StatusCode = 200,
                    Timestamp = DateTime.UtcNow,
                    VirtualKeyId = 1
                },
                new() { 
                    ModelName = "gpt-3.5-turbo", 
                    Cost = 0.02m,
                    InputTokens = 200,
                    OutputTokens = 100,
                    ResponseTimeMs = 800,
                    StatusCode = 200,
                    Timestamp = DateTime.UtcNow,
                    VirtualKeyId = 2
                },
                new() { 
                    ModelName = "gpt-4", 
                    Cost = 0.00m,
                    InputTokens = 50,
                    OutputTokens = 0,
                    ResponseTimeMs = 500,
                    StatusCode = 429, // Error
                    Timestamp = DateTime.UtcNow,
                    VirtualKeyId = 1
                }
            };
            
            var virtualKeys = new List<VirtualKey>
            {
                new() { Id = 1, KeyName = "Production Key" },
                new() { Id = 2, KeyName = "Development Key" }
            };
            
            _mockRequestLogRepository
                .Setup(x => x.GetByDateRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(testLogs);
            
            _mockVirtualKeyRepository
                .Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(virtualKeys);

            // Act
            var result = await _service.GetAnalyticsSummaryAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.TotalRequests);
            Assert.Equal(0.07m, result.TotalCost);
            Assert.Equal(350, result.TotalInputTokens);
            Assert.Equal(150, result.TotalOutputTokens);
            Assert.Equal(2, result.UniqueVirtualKeys);
            Assert.Equal(2, result.UniqueModels);
            Assert.True(result.SuccessRate > 66 && result.SuccessRate < 67); // 2/3 success
            Assert.Equal(2, result.TopModels.Count);
            Assert.Equal(2, result.TopVirtualKeys.Count);
        }

        #endregion

        #region GetVirtualKeyUsageAsync Tests

        [Fact]
        public async Task GetVirtualKeyUsageAsync_FiltersById()
        {
            // Arrange
            var testLogs = new List<RequestLog>
            {
                new() { 
                    VirtualKeyId = 1, 
                    ModelName = "gpt-4", 
                    Cost = 0.05m,
                    InputTokens = 100,
                    OutputTokens = 50,
                    ResponseTimeMs = 1500,
                    Timestamp = DateTime.UtcNow
                },
                new() { 
                    VirtualKeyId = 2, // Different key
                    ModelName = "gpt-3.5-turbo", 
                    Cost = 0.02m,
                    InputTokens = 200,
                    OutputTokens = 100,
                    ResponseTimeMs = 800,
                    Timestamp = DateTime.UtcNow
                },
                new() { 
                    VirtualKeyId = 1, 
                    ModelName = "gpt-4", 
                    Cost = 0.03m,
                    InputTokens = 150,
                    OutputTokens = 75,
                    ResponseTimeMs = 1200,
                    Timestamp = DateTime.UtcNow
                }
            };
            
            _mockRequestLogRepository
                .Setup(x => x.GetByDateRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(testLogs);

            // Act
            var result = await _service.GetVirtualKeyUsageAsync(1);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.TotalRequests);
            Assert.Equal(0.08m, result.TotalCost);
            Assert.Equal(250, result.TotalInputTokens);
            Assert.Equal(125, result.TotalOutputTokens);
            Assert.Equal(1350, result.AverageResponseTimeMs); // (1500 + 1200) / 2
            Assert.Single(result.ModelUsage);
            Assert.Equal("gpt-4", result.ModelUsage.Keys.First());
        }

        #endregion

        #region ExportAnalyticsAsync Tests

        [Fact]
        public async Task ExportAnalyticsAsync_CSV_GeneratesCorrectFormat()
        {
            // Arrange
            var testLogs = new List<RequestLog>
            {
                new() { 
                    Id = 1,
                    VirtualKeyId = 1, 
                    ModelName = "gpt-4", 
                    RequestType = "chat",
                    Cost = 0.05m,
                    InputTokens = 100,
                    OutputTokens = 50,
                    ResponseTimeMs = 1500.5,
                    StatusCode = 200,
                    Timestamp = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc)
                }
            };
            
            _mockRequestLogRepository
                .Setup(x => x.GetByDateRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(testLogs);

            // Act
            var result = await _service.ExportAnalyticsAsync("csv");
            var csv = System.Text.Encoding.UTF8.GetString(result);

            // Assert
            Assert.Contains("Timestamp,VirtualKeyId,Model,RequestType,InputTokens,OutputTokens,Cost,ResponseTime,StatusCode", csv);
            Assert.Contains("2024-01-15 10:30:00,1,gpt-4,chat,100,50,0.050000,1500.50,200", csv);
        }

        [Fact]
        public async Task ExportAnalyticsAsync_JSON_GeneratesCorrectFormat()
        {
            // Arrange
            var testLogs = new List<RequestLog>
            {
                new() { 
                    Id = 1,
                    VirtualKeyId = 1, 
                    ModelName = "gpt-4",
                    RequestType = "chat",
                    Cost = 0.05m,
                    Timestamp = DateTime.UtcNow
                }
            };
            
            _mockRequestLogRepository
                .Setup(x => x.GetByDateRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(testLogs);

            // Act
            var result = await _service.ExportAnalyticsAsync("json");
            var json = System.Text.Encoding.UTF8.GetString(result);

            // Assert
            Assert.Contains("\"ModelName\":", json);
            Assert.Contains("\"gpt-4\"", json);
            Assert.Contains("\"Cost\":", json);
        }

        #endregion

        #region Helper Methods

        private List<RequestLog> GenerateTestLogs(int count)
        {
            var logs = new List<RequestLog>();
            var models = new[] { "gpt-4", "gpt-3.5-turbo", "claude-3" };
            var random = new Random();
            
            for (int i = 0; i < count; i++)
            {
                logs.Add(new RequestLog
                {
                    Id = i + 1,
                    VirtualKeyId = random.Next(1, 5),
                    ModelName = models[random.Next(models.Length)],
                    RequestType = "chat",
                    InputTokens = random.Next(50, 500),
                    OutputTokens = random.Next(50, 500),
                    Cost = (decimal)(random.NextDouble() * 0.1),
                    ResponseTimeMs = random.Next(500, 3000),
                    StatusCode = random.Next(10) > 8 ? 429 : 200,
                    Timestamp = DateTime.UtcNow.AddDays(-random.Next(30)),
                    UserId = $"user{random.Next(1, 10)}",
                    ClientIp = $"192.168.1.{random.Next(1, 255)}",
                    RequestPath = "/v1/chat/completions"
                });
            }
            
            return logs;
        }

        #endregion
    }
}