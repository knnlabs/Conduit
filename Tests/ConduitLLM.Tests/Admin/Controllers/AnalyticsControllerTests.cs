using ConduitLLM.Admin.Controllers;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.DTOs.Costs;

using FluentAssertions;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using Moq;

using Xunit.Abstractions;

namespace ConduitLLM.Tests.Admin.Controllers
{
    [Trait("Category", "Unit")]
    [Trait("Component", "AdminController")]
    public class AnalyticsControllerTests
    {
        private readonly Mock<IAnalyticsService> _mockAnalyticsService;
        private readonly Mock<IAnalyticsMetrics> _mockAnalyticsMetrics;
        private readonly Mock<ILogger<AnalyticsController>> _mockLogger;
        private readonly AnalyticsController _controller;
        private readonly ITestOutputHelper _output;

        public AnalyticsControllerTests(ITestOutputHelper output)
        {
            _output = output;
            _mockAnalyticsService = new Mock<IAnalyticsService>();
            _mockAnalyticsMetrics = new Mock<IAnalyticsMetrics>();
            _mockLogger = new Mock<ILogger<AnalyticsController>>();
            _controller = new AnalyticsController(
                _mockAnalyticsService.Object,
                _mockLogger.Object,
                _mockAnalyticsMetrics.Object);
        }

        #region GetLogs Tests

        [Fact]
        public async Task GetLogs_ValidParams_ReturnsOk()
        {
            // Arrange
            var pagedResult = new PagedResult<LogRequestDto>
            {
                Items = new List<LogRequestDto>(),
                TotalCount = 0,
                Page = 1,
                PageSize = 50
            };
            _mockAnalyticsService.Setup(s => s.GetLogsAsync(1, 50, null, null, null, null, null))
                .ReturnsAsync(pagedResult);

            // Act
            var result = await _controller.GetLogs();

            // Assert
            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public async Task GetLogs_InvalidPage_ReturnsBadRequest()
        {
            // Act
            var result = await _controller.GetLogs(page: 0);

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task GetLogs_PageSizeTooLarge_ReturnsBadRequest()
        {
            // Act
            var result = await _controller.GetLogs(pageSize: 101);

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
        }

        #endregion

        #region ExportAnalytics Tests

        [Fact]
        public async Task ExportAnalytics_ValidCsvExport_ReturnsFileAndLogsAudit()
        {
            // Arrange
            var csvData = System.Text.Encoding.UTF8.GetBytes("header1,header2\nval1,val2");
            _mockAnalyticsService.Setup(s => s.ExportAnalyticsAsync("csv", null, null, null, null))
                .ReturnsAsync(csvData);

            // Act
            var result = await _controller.ExportAnalytics(format: "csv");

            // Assert
            result.Should().BeOfType<FileContentResult>();
            var fileResult = (FileContentResult)result;
            fileResult.ContentType.Should().Be("text/csv");

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Admin Audit") && o.ToString()!.Contains("Exported")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task ExportAnalytics_InvalidFormat_ReturnsBadRequest()
        {
            // Act
            var result = await _controller.ExportAnalytics(format: "xml");

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
        }

        #endregion

        #region InvalidateCache Tests

        [Fact]
        public async Task InvalidateCache_LogsAudit()
        {
            // Act
            var result = await _controller.InvalidateCache("test reason");

            // Assert
            result.Should().BeOfType<OkObjectResult>();

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Admin Audit") && o.ToString()!.Contains("Invalidated")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        #endregion

        #region GetCostSummary Tests

        [Fact]
        public async Task GetCostSummary_InvalidTimeframe_ReturnsBadRequest()
        {
            // Act
            var result = await _controller.GetCostSummary(timeframe: "yearly");

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task GetCostSummary_ValidTimeframe_ReturnsOk()
        {
            // Arrange
            _mockAnalyticsService.Setup(s => s.GetCostSummaryAsync("daily", null, null))
                .ReturnsAsync(new CostDashboardDto());

            // Act
            var result = await _controller.GetCostSummary(timeframe: "daily");

            // Assert
            result.Should().BeOfType<OkObjectResult>();
        }

        #endregion

        #region GetCacheMetrics Tests

        [Fact]
        public void GetCacheMetrics_MetricsEnabled_ReturnsOk()
        {
            // Arrange
            _mockAnalyticsMetrics.Setup(m => m.GetCacheStatistics())
                .Returns(new Dictionary<string, object> { ["hitRate"] = 0.95 });

            // Act
            var result = _controller.GetCacheMetrics();

            // Assert
            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public void GetCacheMetrics_MetricsDisabled_ReturnsNotFound()
        {
            // Arrange - controller with null metrics
            var controller = new AnalyticsController(
                _mockAnalyticsService.Object,
                _mockLogger.Object,
                analyticsMetrics: null);

            // Act
            var result = controller.GetCacheMetrics();

            // Assert
            result.Should().BeOfType<NotFoundObjectResult>();
        }

        #endregion
    }
}
