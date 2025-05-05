using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Services.Dtos;
using ConduitLLM.WebUI.Controllers;
using ConduitLLM.WebUI.DTOs;
using ConduitLLM.WebUI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Controllers
{
    public class LogsControllerNewTests
    {
        private readonly Mock<IRequestLogServiceNew> _mockRequestLogService;
        private readonly Mock<IVirtualKeyServiceNew> _mockVirtualKeyService;
        private readonly Mock<ILogger<LogsControllerNew>> _mockLogger;
        private readonly LogsControllerNew _controller;

        public LogsControllerNewTests()
        {
            _mockRequestLogService = new Mock<IRequestLogServiceNew>();
            _mockVirtualKeyService = new Mock<IVirtualKeyServiceNew>();
            _mockLogger = new Mock<ILogger<LogsControllerNew>>();
            
            _controller = new LogsControllerNew(
                _mockRequestLogService.Object,
                _mockVirtualKeyService.Object,
                _mockLogger.Object);
        }

        [Fact]
        public async Task SearchLogs_ShouldReturnOkWithPagedResult()
        {
            // Arrange
            int? virtualKeyId = 1;
            string modelFilter = "gpt-4";
            DateTime startDate = DateTime.UtcNow.AddDays(-1);
            DateTime endDate = DateTime.UtcNow;
            int? statusCode = 200;
            int page = 1;
            int pageSize = 20;

            var logs = new List<RequestLog>
            {
                new RequestLog
                {
                    Id = 1,
                    VirtualKeyId = 1,
                    ModelName = "gpt-4",
                    RequestType = "chat",
                    InputTokens = 100,
                    OutputTokens = 50,
                    Cost = 0.02m,
                    ResponseTimeMs = 1500,
                    Timestamp = DateTime.UtcNow.AddHours(-1),
                    StatusCode = 200
                }
            };

            _mockRequestLogService.Setup(s => s.SearchLogsAsync(
                virtualKeyId, modelFilter, startDate, endDate, statusCode, page, pageSize, default))
                .ReturnsAsync((logs, 1));

            // Act
            var result = await _controller.SearchLogs(
                virtualKeyId, modelFilter, startDate, endDate, statusCode, page, pageSize);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var pagedResult = Assert.IsType<PagedResult<RequestLogDto>>(okResult.Value);
            
            Assert.Equal(1, pagedResult.TotalCount);
            Assert.Single(pagedResult.Items);
            Assert.Equal(1, pagedResult.Items[0].Id);
            Assert.Equal("gpt-4", pagedResult.Items[0].ModelName);
        }

        [Fact]
        public async Task GetLogsSummary_ShouldReturnOkWithSummary()
        {
            // Arrange
            DateTime startDate = DateTime.UtcNow.AddDays(-7);
            DateTime endDate = DateTime.UtcNow;

            var summary = new LogsSummaryDto
            {
                TotalRequests = 100,
                TotalInputTokens = 10000,
                TotalOutputTokens = 5000,
                TotalCost = 1.25m,
                AverageResponseTimeMs = 1200,
                StartDate = startDate,
                EndDate = endDate
            };

            _mockRequestLogService.Setup(s => s.GetLogsSummaryAsync(
                startDate, endDate, default))
                .ReturnsAsync(summary);

            // Act
            var result = await _controller.GetLogsSummary(startDate, endDate);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedSummary = Assert.IsType<LogsSummaryDto>(okResult.Value);
            
            Assert.Equal(100, returnedSummary.TotalRequests);
            Assert.Equal(10000, returnedSummary.TotalInputTokens);
            Assert.Equal(5000, returnedSummary.TotalOutputTokens);
            Assert.Equal(1.25m, returnedSummary.TotalCost);
        }

        [Fact]
        public async Task GetVirtualKeys_ShouldReturnOkWithKeysList()
        {
            // Arrange
            var keys = new List<ConduitLLM.Configuration.DTOs.VirtualKey.VirtualKeyDto>
            {
                new ConduitLLM.Configuration.DTOs.VirtualKey.VirtualKeyDto
                {
                    Id = 1,
                    KeyName = "Test Key 1"
                },
                new ConduitLLM.Configuration.DTOs.VirtualKey.VirtualKeyDto
                {
                    Id = 2,
                    KeyName = "Test Key 2"
                }
            };

            _mockVirtualKeyService.Setup(s => s.ListVirtualKeysAsync())
                .ReturnsAsync(keys);

            // Act
            var result = await _controller.GetVirtualKeys();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedKeys = Assert.IsAssignableFrom<IEnumerable<object>>(okResult.Value);
            
            Assert.NotEmpty(returnedKeys);
        }

        [Fact]
        public async Task GetDistinctModels_ShouldReturnOkWithModelsList()
        {
            // Arrange
            var models = new List<string> { "gpt-4", "claude-v1", "gpt-3.5-turbo" };

            _mockRequestLogService.Setup(s => s.GetDistinctModelsAsync(default))
                .ReturnsAsync(models);

            // Act
            var result = await _controller.GetDistinctModels();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedModels = Assert.IsAssignableFrom<List<string>>(okResult.Value);
            
            Assert.Equal(3, returnedModels.Count);
            Assert.Contains("gpt-4", returnedModels);
            Assert.Contains("claude-v1", returnedModels);
            Assert.Contains("gpt-3.5-turbo", returnedModels);
        }
    }
}