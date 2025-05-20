using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.WebUI.Services.Adapters;
using Microsoft.Extensions.Logging;
using Moq;

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
        public async Task GetRequestLogsAsync_DelegatesToAdminApiClient()
        {
            // Arrange
            var expectedLogs = new ConfigDTOs.PagedResult<ConfigDTOs.RequestLogDto>
            {
                Items = new List<ConfigDTOs.RequestLogDto>
                {
                    new ConfigDTOs.RequestLogDto { Id = 1, ModelId = "gpt-4" },
                    new ConfigDTOs.RequestLogDto { Id = 2, ModelId = "gpt-3.5-turbo" }
                },
                TotalCount = 2,
                PageSize = 20,
                CurrentPage = 1,
                TotalPages = 1
            };

            _adminApiClientMock.Setup(c => c.GetRequestLogsAsync(
                1, 20, 1, "gpt-4", It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync(expectedLogs);

            // Act
            var result = await _adapter.GetRequestLogsAsync(
                1, 20, 1, "gpt-4", null, null);

            // Assert
            Assert.Same(expectedLogs, result);
            _adminApiClientMock.Verify(c => c.GetRequestLogsAsync(
                1, 20, 1, "gpt-4", null, null), Times.Once);
        }

        [Fact]
        public async Task GetRequestLogsAsync_ReturnsEmptyResult_WhenApiReturnsNull()
        {
            // Arrange
            _adminApiClientMock.Setup(c => c.GetRequestLogsAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<string>(),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync((ConfigDTOs.PagedResult<ConfigDTOs.RequestLogDto>)null);

            // Act
            var result = await _adapter.GetRequestLogsAsync(1, 20);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result.Items);
            Assert.Equal(0, result.TotalCount);
            Assert.Equal(20, result.PageSize);
            Assert.Equal(1, result.CurrentPage);
            Assert.Equal(0, result.TotalPages);
            _adminApiClientMock.Verify(c => c.GetRequestLogsAsync(
                1, 20, null, null, null, null), Times.Once);
        }

        [Fact]
        public async Task GetLogsSummaryAsync_DelegatesToAdminApiClient()
        {
            // Arrange
            var expectedSummary = new ConfigDTOs.LogsSummaryDto
            {
                TotalRequests = 100,
                TotalInputTokens = 50000,
                TotalOutputTokens = 25000,
                TotalCost = 10.5m
            };

            _adminApiClientMock.Setup(c => c.GetLogsSummaryAsync(7, 1))
                .ReturnsAsync(expectedSummary);

            // Act
            var result = await _adapter.GetLogsSummaryAsync(7, 1);

            // Assert
            Assert.Same(expectedSummary, result);
            _adminApiClientMock.Verify(c => c.GetLogsSummaryAsync(7, 1), Times.Once);
        }

        [Fact]
        public async Task GetLogsSummaryAsync_UsesDefaultDays_WhenNotSpecified()
        {
            // Arrange
            var expectedSummary = new ConfigDTOs.LogsSummaryDto
            {
                TotalRequests = 100,
                TotalInputTokens = 50000,
                TotalOutputTokens = 25000,
                TotalCost = 10.5m
            };

            _adminApiClientMock.Setup(c => c.GetLogsSummaryAsync(7, null))
                .ReturnsAsync(expectedSummary);

            // Act
            var result = await _adapter.GetLogsSummaryAsync();

            // Assert
            Assert.Same(expectedSummary, result);
            _adminApiClientMock.Verify(c => c.GetLogsSummaryAsync(7, null), Times.Once);
        }

        [Fact]
        public async Task GetLogsSummaryAsync_ReturnsNull_WhenApiReturnsNull()
        {
            // Arrange
            _adminApiClientMock.Setup(c => c.GetLogsSummaryAsync(
                It.IsAny<int>(), It.IsAny<int?>()))
                .ReturnsAsync((ConfigDTOs.LogsSummaryDto)null);

            // Act
            var result = await _adapter.GetLogsSummaryAsync(30, 2);

            // Assert
            Assert.Null(result);
            _adminApiClientMock.Verify(c => c.GetLogsSummaryAsync(30, 2), Times.Once);
        }
    }
}