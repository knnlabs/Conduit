using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.WebUI.Services.Adapters;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

// Using global namespace aliases from DTONamespaceAliases.cs

namespace ConduitLLM.Tests.WebUI.Adapters
{
    public class RequestLogServiceAdapterTests
    {
        private readonly Mock<IAdminApiClient> _adminApiClientMock;
        private readonly Mock<ILogger<RequestLogServiceAdapter>> _loggerMock;
        private readonly RequestLogServiceAdapter _adapter;

        public RequestLogServiceAdapterTests()
        {
            _adminApiClientMock = new Mock<IAdminApiClient>();
            _loggerMock = new Mock<ILogger<RequestLogServiceAdapter>>();
            _adapter = new RequestLogServiceAdapter(_adminApiClientMock.Object, _loggerMock.Object);
        }

        [Fact]
        public async Task CreateRequestLogAsync_DelegatesToAdminApiClient()
        {
            // Arrange
            var responseDto = new ConfigDTOs.RequestLogDto { Id = 1 };
            _adminApiClientMock.Setup(c => c.CreateRequestLogAsync(It.IsAny<ConfigDTOs.RequestLogDto>()))
                .ReturnsAsync(responseDto);

            // Act
            var result = await ((ConduitLLM.WebUI.Interfaces.IRequestLogService)_adapter).CreateRequestLogAsync(
                1, "gpt-4", "chat", 100, 200, 0.5m, 500, "user1", "127.0.0.1", "/api/chat", 200, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1, result.Id);
            _adminApiClientMock.Verify(c => c.CreateRequestLogAsync(It.IsAny<ConfigDTOs.RequestLogDto>()), Times.Once);
        }

        [Fact]
        public async Task GetRequestLogsForKeyAsync_DelegatesToAdminApiClient()
        {
            // Arrange
            var logs = new ConfigDTOs.PagedResult<ConfigDTOs.RequestLogDto>
            {
                Items = new List<ConfigDTOs.RequestLogDto>
                {
                    new ConfigDTOs.RequestLogDto { Id = 1, VirtualKeyId = 1, ModelName = "gpt-4" },
                    new ConfigDTOs.RequestLogDto { Id = 2, VirtualKeyId = 1, ModelName = "gpt-3.5-turbo" }
                },
                TotalCount = 2,
                Page = 1,
                PageSize = 20,
                TotalPages = 1
            };

            _adminApiClientMock.Setup(c => c.GetRequestLogsAsync(
                1, 20, 1, null, null, null))
                .ReturnsAsync(logs);

            // Act
            var result = await ((ConduitLLM.WebUI.Interfaces.IRequestLogService)_adapter).GetRequestLogsForKeyAsync(
                1, 1, 20, CancellationToken.None);

            // Assert
            Assert.NotNull(result.Logs);
            Assert.Equal(2, result.Logs.Count);
            Assert.Equal(2, result.TotalCount);
            _adminApiClientMock.Verify(c => c.GetRequestLogsAsync(1, 20, 1, null, null, null), Times.Once);
        }

        [Fact]
        public async Task GetKeyUsageSummaryAsync_DelegatesToAdminApiClient()
        {
            // Arrange
            var keyStats = new List<WebUIDTOs.VirtualKeyCostDataDto>
            {
                new WebUIDTOs.VirtualKeyCostDataDto
                {
                    VirtualKeyId = 1,
                    KeyName = "TestKey",
                    TotalCost = 10.5m,
                    RequestCount = 100
                }
            };

            _adminApiClientMock.Setup(c => c.GetVirtualKeyUsageStatisticsAsync(null))
                .Returns(Task.FromResult<IEnumerable<WebUIDTOs.VirtualKeyCostDataDto>>(keyStats));

            // Act
            var result = await ((ConduitLLM.WebUI.Interfaces.IRequestLogService)_adapter).GetKeyUsageSummaryAsync(1, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1, result.VirtualKeyId);
            Assert.Equal("TestKey", result.KeyName);
            Assert.Equal(100, result.TotalRequests);
            Assert.Equal(10.5m, result.TotalCost);
            _adminApiClientMock.Verify(c => c.GetVirtualKeyUsageStatisticsAsync(null), Times.Once);
        }

        [Fact]
        public async Task GetLogsSummaryAsync_ConvertsToWebUIDto()
        {
            // Arrange
            var today = DateTime.UtcNow;
            var sevenDaysAgo = today.AddDays(-7);
            
            var adminSummary = new ConfigDTOs.LogsSummaryDto
            {
                TotalRequests = 100,
                InputTokens = 50000,
                OutputTokens = 25000,
                EstimatedCost = 10.5m,
                AverageResponseTime = 200,
                LastRequestDate = today,
                SuccessfulRequests = 95,
                FailedRequests = 5,
                // Add required properties that might be used in conversion
                RequestsByModel = new Dictionary<string, int>(),
                CostByModel = new Dictionary<string, decimal>(),
                RequestsByStatus = new Dictionary<int, int>(),
                DailyStats = new List<ConfigDTOs.DailyUsageStatsDto>()
            };

            _adminApiClientMock.Setup(c => c.GetLogsSummaryAsync(7, null))
                .ReturnsAsync(adminSummary);

            // Act
            var result = await ((ConduitLLM.WebUI.Interfaces.IRequestLogService)_adapter).GetLogsSummaryAsync(
                sevenDaysAgo, today, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(100, result.TotalRequests);
            Assert.Equal(50000, result.InputTokens);
            Assert.Equal(25000, result.OutputTokens);
            Assert.Equal(10.5m, result.EstimatedCost);
            Assert.Equal(200, result.AverageResponseTime);
            Assert.Equal(sevenDaysAgo, result.StartDate);
            Assert.Equal(today, result.EndDate);
            _adminApiClientMock.Verify(c => c.GetLogsSummaryAsync(7, null), Times.Once);
        }
    }
}