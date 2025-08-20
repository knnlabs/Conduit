using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Entities;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Admin.Services
{
    /// <summary>
    /// Export operation tests for AnalyticsServiceTests
    /// </summary>
    public partial class AnalyticsServiceTests
    {
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
    }
}