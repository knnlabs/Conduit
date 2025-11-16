using ConduitLLM.Configuration.Entities;

using Moq;

namespace ConduitLLM.Tests.Admin.Services
{
    /// <summary>
    /// Log operation tests for AnalyticsServiceTests
    /// </summary>
    public partial class AnalyticsServiceTests
    {
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
    }
}