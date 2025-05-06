using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Services;
using ConduitLLM.WebUI.Controllers;
using ConduitLLM.WebUI.DTOs;
using ConduitLLM.WebUI.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Controllers
{
    public class LogsControllerNewTests
    {
        private readonly Mock<ConduitLLM.WebUI.Interfaces.IRequestLogService> _mockRequestLogService;
        private readonly Mock<ConduitLLM.WebUI.Interfaces.IVirtualKeyService> _mockVirtualKeyService;
        private readonly Mock<ILogger<LogsController>> _mockLogger;
        private readonly LogsController _controller;

        public LogsControllerNewTests()
        {
            _mockRequestLogService = new Mock<ConduitLLM.WebUI.Interfaces.IRequestLogService>();
            _mockVirtualKeyService = new Mock<ConduitLLM.WebUI.Interfaces.IVirtualKeyService>();
            _mockLogger = new Mock<ILogger<LogsController>>();
            
            _controller = new LogsController(
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
                It.IsAny<int?>(), It.IsAny<string>(), It.IsAny<DateTime>(), 
                It.IsAny<DateTime>(), It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<int>(), 
                It.IsAny<CancellationToken>()))
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

            // Create a mock with object for the summary to handle type differences
            object summaryObj = new
            {
                TotalRequests = 100,
                TotalInputTokens = 10000,
                TotalOutputTokens = 5000,
                TotalCost = 1.25m,
                AverageResponseTimeMs = 1200,
                StartDate = startDate,
                EndDate = endDate
            };

            // Use mock.Setup with a more generic approach
            _mockRequestLogService.Setup(s => s.GetLogsSummaryAsync(
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult((dynamic)summaryObj));

            // Act
            var result = await _controller.GetLogsSummary(startDate, endDate);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            
            // Make sure the return value is not null
            Assert.NotNull(okResult.Value);
            
            // Use dynamic to avoid type issues, but check for null first
            var returnedSummary = okResult.Value;
            
            // Perform assertions with null checks
            Assert.Equal(100, ((dynamic)returnedSummary).TotalRequests);
            Assert.Equal(10000, ((dynamic)returnedSummary).TotalInputTokens);
            Assert.Equal(5000, ((dynamic)returnedSummary).TotalOutputTokens);
            Assert.Equal(1.25m, ((dynamic)returnedSummary).TotalCost);
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

            _mockRequestLogService.Setup(s => s.GetDistinctModelsAsync(
                It.IsAny<CancellationToken>()))
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