using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.DTOs.VirtualKey;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.WebUI.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.WebUI.Providers
{
    public class AdminRequestLogTests
    {
        private readonly Mock<IAdminApiClient> _mockAdminApiClient;
        private readonly Mock<ILogger<AdminApiClient>> _loggerMock;
        private readonly IRequestLogService _service;

        public AdminRequestLogTests()
        {
            // Create a mock that implements both interfaces
            _mockAdminApiClient = new Mock<IAdminApiClient>();
            Mock<IRequestLogService> mockService = _mockAdminApiClient.As<IRequestLogService>();
            _loggerMock = new Mock<ILogger<AdminApiClient>>();
            
            // Use the mock as the service
            _service = mockService.Object;
        }

        [Fact]
        public async Task CreateRequestLogAsync_ReturnsLog_WhenCreationSucceeds()
        {
            // Arrange
            int virtualKeyId = 1;
            string modelName = "gpt-4";
            string requestType = "completion";
            int inputTokens = 100;
            int outputTokens = 50;
            decimal cost = 0.25m;
            double responseTimeMs = 1500;
            string userId = "user123";
            string clientIp = "192.168.1.1";
            string requestPath = "/api/chat/completions";
            int statusCode = 200;

            var requestLogDto = new RequestLogDto
            {
                Id = 1,
                VirtualKeyId = virtualKeyId,
                ModelName = modelName,
                RequestType = requestType,
                InputTokens = inputTokens,
                OutputTokens = outputTokens,
                Cost = cost,
                ResponseTimeMs = responseTimeMs,
                UserId = userId,
                ClientIp = clientIp,
                RequestPath = requestPath,
                StatusCode = statusCode,
                Timestamp = DateTime.UtcNow
            };

            _mockAdminApiClient.Setup(c => c.CreateRequestLogAsync(It.IsAny<RequestLogDto>()))
                .ReturnsAsync(requestLogDto);

            // Act
            var result = await _service.CreateRequestLogAsync(
                virtualKeyId,
                modelName,
                requestType,
                inputTokens,
                outputTokens,
                cost,
                responseTimeMs,
                userId,
                clientIp,
                requestPath,
                statusCode);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1, result.Id);
            Assert.Equal(virtualKeyId, result.VirtualKeyId);
            Assert.Equal(modelName, result.ModelName);
            Assert.Equal(requestType, result.RequestType);
            Assert.Equal(inputTokens, result.InputTokens);
            Assert.Equal(outputTokens, result.OutputTokens);
            Assert.Equal(cost, result.Cost);
            Assert.Equal(responseTimeMs, result.ResponseTimeMs);
            Assert.Equal(userId, result.UserId);
            Assert.Equal(clientIp, result.ClientIp);
            Assert.Equal(requestPath, result.RequestPath);
            Assert.Equal(statusCode, result.StatusCode);
            
            _mockAdminApiClient.Verify(c => c.CreateRequestLogAsync(It.Is<RequestLogDto>(dto =>
                dto.VirtualKeyId == virtualKeyId &&
                dto.ModelName == modelName &&
                dto.RequestType == requestType &&
                dto.InputTokens == inputTokens &&
                dto.OutputTokens == outputTokens &&
                dto.Cost == cost &&
                dto.ResponseTimeMs == responseTimeMs &&
                dto.UserId == userId &&
                dto.ClientIp == clientIp &&
                dto.RequestPath == requestPath &&
                dto.StatusCode == statusCode
            )), Times.Once);
        }

        [Fact]
        public async Task GetAllKeysUsageSummaryAsync_ReturnsKeySummaries_WhenApiReturnsData()
        {
            // Arrange
            var keyUsageStats = new List<WebUIDTOs.VirtualKeyCostDataDto>
            {
                new WebUIDTOs.VirtualKeyCostDataDto
                {
                    VirtualKeyId = 1,
                    KeyName = "Key1",
                    Cost = 10.5m,
                    RequestCount = 100,
                    InputTokens = 5000,
                    OutputTokens = 2500,
                    AverageResponseTimeMs = 1200,
                    LastUsedAt = DateTime.UtcNow.AddDays(-1)
                },
                new WebUIDTOs.VirtualKeyCostDataDto
                {
                    VirtualKeyId = 2,
                    KeyName = "Key2",
                    Cost = 5.25m,
                    RequestCount = 50,
                    InputTokens = 2500,
                    OutputTokens = 1250,
                    AverageResponseTimeMs = 800,
                    LastUsedAt = DateTime.UtcNow.AddDays(-2)
                }
            };

            _mockAdminApiClient.Setup(c => c.GetVirtualKeyUsageStatisticsAsync(null))
                .ReturnsAsync(keyUsageStats);

            // Act
            var result = await _service.GetAllKeysUsageSummaryAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            
            // Verify first key summary
            Assert.Equal(1, result[0].VirtualKeyId);
            Assert.Equal("Key1", result[0].KeyName);
            Assert.Equal(100, result[0].TotalRequests);
            Assert.Equal(10.5m, result[0].TotalCost);
            Assert.Equal(5000, result[0].TotalInputTokens);
            Assert.Equal(2500, result[0].TotalOutputTokens);
            Assert.Equal(keyUsageStats[0].LastUsedAt, result[0].LastUsed);
            
            // Verify second key summary
            Assert.Equal(2, result[1].VirtualKeyId);
            Assert.Equal("Key2", result[1].KeyName);
            Assert.Equal(50, result[1].TotalRequests);
            Assert.Equal(5.25m, result[1].TotalCost);
            Assert.Equal(2500, result[1].TotalInputTokens);
            Assert.Equal(1250, result[1].TotalOutputTokens);
            Assert.Equal(keyUsageStats[1].LastUsedAt, result[1].LastUsed);
            
            _mockAdminApiClient.Verify(c => c.GetVirtualKeyUsageStatisticsAsync(null), Times.Once);
        }

        [Fact]
        public async Task GetDailyUsageStatsAsync_ReturnsDailyStats_WhenApiReturnsData()
        {
            // Arrange
            DateTime startDate = DateTime.UtcNow.AddDays(-7);
            DateTime endDate = DateTime.UtcNow;
            int virtualKeyId = 1;

            var dailyStats = new List<WebUIDTOs.DailyUsageStatsDto>
            {
                new WebUIDTOs.DailyUsageStatsDto
                {
                    Date = startDate.AddDays(1),
                    ModelName = "gpt-4",
                    TotalCost = 2.5m,
                    RequestCount = 25,
                    InputTokens = 1250,
                    OutputTokens = 625
                },
                new WebUIDTOs.DailyUsageStatsDto
                {
                    Date = startDate.AddDays(2),
                    ModelName = "gpt-3.5-turbo",
                    TotalCost = 0.5m,
                    RequestCount = 50,
                    InputTokens = 2500,
                    OutputTokens = 1250
                }
            };

            _mockAdminApiClient.Setup(c => c.GetDailyUsageStatsAsync(startDate, endDate, virtualKeyId))
                .ReturnsAsync(dailyStats);

            // Act
            var result = await _service.GetDailyUsageStatsAsync(virtualKeyId, startDate, endDate);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            
            // Verify first day stats
            Assert.Equal(startDate.AddDays(1).Date, result[0].Date.Date);
            Assert.Equal("gpt-4", result[0].ModelName);
            Assert.Equal(2.5m, result[0].TotalCost);
            Assert.Equal(25, result[0].RequestCount);
            Assert.Equal(1250, result[0].InputTokens);
            Assert.Equal(625, result[0].OutputTokens);
            
            // Verify second day stats
            Assert.Equal(startDate.AddDays(2).Date, result[1].Date.Date);
            Assert.Equal("gpt-3.5-turbo", result[1].ModelName);
            Assert.Equal(0.5m, result[1].TotalCost);
            Assert.Equal(50, result[1].RequestCount);
            Assert.Equal(2500, result[1].InputTokens);
            Assert.Equal(1250, result[1].OutputTokens);
            
            _mockAdminApiClient.Verify(c => c.GetDailyUsageStatsAsync(startDate, endDate, virtualKeyId), Times.Once);
        }

        [Fact]
        public async Task GetDistinctModelsAsync_ReturnsModels_WhenApiReturnsData()
        {
            // Arrange
            var models = new List<string> { "gpt-4", "gpt-3.5-turbo", "gemini-pro" };

            _mockAdminApiClient.Setup(c => c.GetDistinctModelsAsync())
                .ReturnsAsync(models);

            // Act
            var result = await _service.GetDistinctModelsAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.Count);
            Assert.Contains("gpt-4", result);
            Assert.Contains("gpt-3.5-turbo", result);
            Assert.Contains("gemini-pro", result);
            
            _mockAdminApiClient.Verify(c => c.GetDistinctModelsAsync(), Times.Once);
        }

        [Fact]
        public async Task GetKeyUsageSummaryAsync_ReturnsUsageSummary_WhenKeyExists()
        {
            // Arrange
            int virtualKeyId = 1;
            
            var virtualKey = new VirtualKeyDto
            {
                Id = virtualKeyId,
                Name = "Test Key",
                ApiKey = "vk-123456",
                CreatedAt = DateTime.UtcNow.AddDays(-30),
                BudgetAmount = 100,
                CurrentSpend = 50
            };
            
            var keyUsageStats = new List<WebUIDTOs.VirtualKeyCostDataDto>
            {
                new WebUIDTOs.VirtualKeyCostDataDto
                {
                    VirtualKeyId = virtualKeyId,
                    KeyName = "Test Key",
                    Cost = 50m,
                    RequestCount = 200,
                    InputTokens = 10000,
                    OutputTokens = 5000,
                    AverageResponseTimeMs = 1000,
                    LastUsedAt = DateTime.UtcNow.AddDays(-1)
                }
            };
            
            var logsSummary7Days = new LogsSummaryDto
            {
                TotalRequests = 50,
                TotalCost = 15m,
                TotalInputTokens = 2500,
                TotalOutputTokens = 1250,
                AverageResponseTimeMs = 900
            };
            
            var logsSummary1Day = new LogsSummaryDto
            {
                TotalRequests = 10,
                TotalCost = 3m,
                TotalInputTokens = 500,
                TotalOutputTokens = 250,
                AverageResponseTimeMs = 950
            };
            
            var logsSummary30Days = new LogsSummaryDto
            {
                TotalRequests = 150,
                TotalCost = 40m,
                TotalInputTokens = 7500,
                TotalOutputTokens = 3750,
                AverageResponseTimeMs = 980
            };

            _mockAdminApiClient.Setup(c => c.GetVirtualKeyByIdAsync(virtualKeyId))
                .ReturnsAsync(virtualKey);
                
            _mockAdminApiClient.Setup(c => c.GetVirtualKeyUsageStatisticsAsync(virtualKeyId))
                .ReturnsAsync(keyUsageStats);
                
            _mockAdminApiClient.Setup(c => c.GetLogsSummaryAsync(7, virtualKeyId))
                .ReturnsAsync(logsSummary7Days);
                
            _mockAdminApiClient.Setup(c => c.GetLogsSummaryAsync(1, virtualKeyId))
                .ReturnsAsync(logsSummary1Day);
                
            _mockAdminApiClient.Setup(c => c.GetLogsSummaryAsync(30, virtualKeyId))
                .ReturnsAsync(logsSummary30Days);

            // Act
            var result = await _service.GetKeyUsageSummaryAsync(virtualKeyId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(virtualKeyId, result.VirtualKeyId);
            Assert.Equal(virtualKey.Name, result.KeyName);
            Assert.Equal(keyUsageStats[0].RequestCount, result.TotalRequests);
            Assert.Equal(keyUsageStats[0].Cost, result.TotalCost);
            Assert.Equal(keyUsageStats[0].InputTokens, result.TotalInputTokens);
            Assert.Equal(keyUsageStats[0].OutputTokens, result.TotalOutputTokens);
            Assert.Equal(keyUsageStats[0].AverageResponseTimeMs, result.AverageResponseTimeMs);
            Assert.Equal(virtualKey.CreatedAt, result.CreatedAt);
            Assert.Equal(keyUsageStats[0].LastUsedAt, result.LastRequestDate);
            Assert.Equal(10, result.RequestsLast24Hours);
            Assert.Equal(50, result.RequestsLast7Days);
            Assert.Equal(150, result.RequestsLast30Days);
            
            _mockAdminApiClient.Verify(c => c.GetVirtualKeyByIdAsync(virtualKeyId), Times.Once);
            _mockAdminApiClient.Verify(c => c.GetVirtualKeyUsageStatisticsAsync(virtualKeyId), Times.Once);
            _mockAdminApiClient.Verify(c => c.GetLogsSummaryAsync(7, virtualKeyId), Times.Once);
            _mockAdminApiClient.Verify(c => c.GetLogsSummaryAsync(1, virtualKeyId), Times.Once);
            _mockAdminApiClient.Verify(c => c.GetLogsSummaryAsync(30, virtualKeyId), Times.Once);
        }

        [Fact]
        public void StartRequestTimer_ReturnsRunningStopwatch()
        {
            // Act
            var result = _provider.StartRequestTimer();

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsRunning);
            Assert.IsType<Stopwatch>(result);
        }

        [Fact]
        public async Task GetRequestLogsForKeyAsync_ReturnsLogs_WhenApiReturnsData()
        {
            // Arrange
            int virtualKeyId = 1;
            int page = 1;
            int pageSize = 10;
            
            var logs = new List<RequestLogDto>
            {
                new RequestLogDto
                {
                    Id = 1,
                    VirtualKeyId = virtualKeyId,
                    ModelName = "gpt-4",
                    RequestType = "completion",
                    InputTokens = 100,
                    OutputTokens = 50,
                    Cost = 0.25m,
                    ResponseTimeMs = 1500,
                    UserId = "user123",
                    ClientIp = "192.168.1.1",
                    RequestPath = "/api/chat/completions",
                    StatusCode = 200,
                    Timestamp = DateTime.UtcNow.AddDays(-1)
                },
                new RequestLogDto
                {
                    Id = 2,
                    VirtualKeyId = virtualKeyId,
                    ModelName = "gpt-3.5-turbo",
                    RequestType = "completion",
                    InputTokens = 50,
                    OutputTokens = 25,
                    Cost = 0.05m,
                    ResponseTimeMs = 800,
                    UserId = "user123",
                    ClientIp = "192.168.1.1",
                    RequestPath = "/api/chat/completions",
                    StatusCode = 200,
                    Timestamp = DateTime.UtcNow.AddDays(-2)
                }
            };
            
            var pagedResult = new PagedResult<RequestLogDto>
            {
                Items = logs,
                TotalCount = 2,
                Page = page,
                PageSize = pageSize
            };

            _mockAdminApiClient.Setup(c => c.GetRequestLogsAsync(page, pageSize, virtualKeyId, null, null, null))
                .ReturnsAsync(pagedResult);

            // Act
            var (result, totalCount) = await _service.GetRequestLogsForKeyAsync(virtualKeyId, page, pageSize);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.Equal(2, totalCount);
            
            // Verify first log
            Assert.Equal(1, result[0].Id);
            Assert.Equal(virtualKeyId, result[0].VirtualKeyId);
            Assert.Equal("gpt-4", result[0].ModelName);
            Assert.Equal(logs[0].Timestamp, result[0].Timestamp);
            
            // Verify second log
            Assert.Equal(2, result[1].Id);
            Assert.Equal(virtualKeyId, result[1].VirtualKeyId);
            Assert.Equal("gpt-3.5-turbo", result[1].ModelName);
            Assert.Equal(logs[1].Timestamp, result[1].Timestamp);
            
            _mockAdminApiClient.Verify(c => c.GetRequestLogsAsync(page, pageSize, virtualKeyId, null, null, null), Times.Once);
        }

        [Fact]
        public async Task SearchLogsAsync_ReturnsLogs_WhenApiReturnsData()
        {
            // Arrange
            int? virtualKeyId = 1;
            string modelFilter = "gpt-4";
            DateTime startDate = DateTime.UtcNow.AddDays(-7);
            DateTime endDate = DateTime.UtcNow;
            int? statusCode = 200;
            int pageNumber = 1;
            int pageSize = 10;
            
            var logs = new List<RequestLogDto>
            {
                new RequestLogDto
                {
                    Id = 1,
                    VirtualKeyId = virtualKeyId.Value,
                    ModelName = "gpt-4",
                    RequestType = "completion",
                    InputTokens = 100,
                    OutputTokens = 50,
                    Cost = 0.25m,
                    ResponseTimeMs = 1500,
                    UserId = "user123",
                    ClientIp = "192.168.1.1",
                    RequestPath = "/api/chat/completions",
                    StatusCode = 200,
                    Timestamp = DateTime.UtcNow.AddDays(-1)
                },
                new RequestLogDto
                {
                    Id = 2,
                    VirtualKeyId = virtualKeyId.Value,
                    ModelName = "gpt-4",
                    RequestType = "completion",
                    InputTokens = 50,
                    OutputTokens = 25,
                    Cost = 0.05m,
                    ResponseTimeMs = 800,
                    UserId = "user123",
                    ClientIp = "192.168.1.1",
                    RequestPath = "/api/chat/completions",
                    StatusCode = 200,
                    Timestamp = DateTime.UtcNow.AddDays(-2)
                }
            };
            
            var pagedResult = new PagedResult<RequestLogDto>
            {
                Items = logs,
                TotalCount = 2,
                Page = pageNumber,
                PageSize = pageSize
            };

            _mockAdminApiClient.Setup(c => c.GetRequestLogsAsync(
                    pageNumber, pageSize, virtualKeyId, modelFilter, startDate, endDate))
                .ReturnsAsync(pagedResult);

            // Act
            var (result, totalCount) = await _service.SearchLogsAsync(
                virtualKeyId, modelFilter, startDate, endDate, statusCode, pageNumber, pageSize);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.Equal(2, totalCount);
            
            // Verify both logs match the expected model
            Assert.All(result, log => Assert.Equal("gpt-4", log.ModelName));
            Assert.All(result, log => Assert.Equal(200, log.StatusCode));
            
            _mockAdminApiClient.Verify(c => c.GetRequestLogsAsync(
                pageNumber, pageSize, virtualKeyId, modelFilter, startDate, endDate), Times.Once);
        }

        [Fact]
        public async Task GetLogsSummaryAsync_ReturnsSummary_WhenApiReturnsData()
        {
            // Arrange
            DateTime startDate = DateTime.UtcNow.AddDays(-7);
            DateTime endDate = DateTime.UtcNow;
            int days = 8; // startDate to endDate inclusive
            
            var configSummary = new LogsSummaryDto
            {
                TotalRequests = 100,
                TotalCost = 25m,
                TotalInputTokens = 5000,
                TotalOutputTokens = 2500,
                AverageResponseTimeMs = 1200,
                DailyStats = new List<Configuration.Services.Dtos.DailyStatsDto>
                {
                    new Configuration.Services.Dtos.DailyStatsDto
                    {
                        Date = startDate.AddDays(1),
                        RequestCount = 20,
                        Cost = 5m,
                        InputTokens = 1000,
                        OutputTokens = 500
                    },
                    new Configuration.Services.Dtos.DailyStatsDto
                    {
                        Date = startDate.AddDays(2),
                        RequestCount = 30,
                        Cost = 7.5m,
                        InputTokens = 1500,
                        OutputTokens = 750
                    }
                },
                RequestsByModel = new Dictionary<string, int>
                {
                    { "gpt-4", 70 },
                    { "gpt-3.5-turbo", 30 }
                },
                CostByModel = new Dictionary<string, decimal>
                {
                    { "gpt-4", 20m },
                    { "gpt-3.5-turbo", 5m }
                }
            };

            _mockAdminApiClient.Setup(c => c.GetLogsSummaryAsync(days))
                .ReturnsAsync(configSummary);

            // Act
            var result = await _service.GetLogsSummaryAsync(startDate, endDate);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(configSummary.TotalRequests, result.TotalRequests);
            Assert.Equal(configSummary.TotalCost, result.TotalCost);
            Assert.Equal(configSummary.TotalInputTokens, result.TotalInputTokens);
            Assert.Equal(configSummary.TotalOutputTokens, result.TotalOutputTokens);
            Assert.Equal(configSummary.AverageResponseTimeMs, result.AverageResponseTimeMs);
            Assert.Equal(startDate, result.StartDate);
            Assert.Equal(endDate, result.EndDate);
            Assert.Equal(days, result.TotalDays);
            
            // Verify daily breakdown
            Assert.Equal(2, result.DailyBreakdown.Count);
            Assert.Equal(startDate.AddDays(1).Date, result.DailyBreakdown[0].Date.Date);
            Assert.Equal(20, result.DailyBreakdown[0].RequestCount);
            Assert.Equal(5m, result.DailyBreakdown[0].Cost);
            
            // Verify model breakdown
            Assert.Equal(2, result.ModelBreakdown.Count);
            Assert.Contains(result.ModelBreakdown, m => m.ModelName == "gpt-4" && m.RequestCount == 70 && m.Cost == 20m);
            Assert.Contains(result.ModelBreakdown, m => m.ModelName == "gpt-3.5-turbo" && m.RequestCount == 30 && m.Cost == 5m);
            
            // Verify top model
            Assert.Equal("gpt-4", result.TopModel);
            
            _mockAdminApiClient.Verify(c => c.GetLogsSummaryAsync(days), Times.Once);
        }
    }
}