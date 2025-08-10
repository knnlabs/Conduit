using System;
using System.Threading.Tasks;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.DTOs.Audio;
using ConduitLLM.Configuration.Entities;
using FluentAssertions;
using Moq;
using Xunit;

namespace ConduitLLM.Admin.Tests.Services
{
    public partial class AdminAudioUsageServiceTests
    {
        #region Export Tests

        [Fact]
        public async Task ExportUsageDataAsync_AsCsv_ShouldReturnCsvData()
        {
            // Arrange
            var query = new AudioUsageQueryDto { Page = 1, PageSize = 10 };
            var logs = CreateSampleAudioUsageLogs(3);
            var pagedResult = new PagedResult<AudioUsageLog>
            {
                Items = logs,
                TotalCount = 3,
                Page = 1,
                PageSize = int.MaxValue,
                TotalPages = 1
            };

            _mockRepository.Setup(x => x.GetPagedAsync(It.IsAny<AudioUsageQueryDto>()))
                .ReturnsAsync(pagedResult);

            // Act
            var result = await _service.ExportUsageDataAsync(query, "csv");

            // Assert
            result.Should().NotBeNullOrEmpty();
            result.Should().Contain("Timestamp");
            result.Should().Contain("VirtualKey");
            result.Should().Contain("ProviderId");
            result.Should().Contain("1"); // Provider ID 1 in CSV
        }

        [Fact]
        public async Task ExportUsageDataAsync_AsJson_ShouldReturnJsonData()
        {
            // Arrange
            var query = new AudioUsageQueryDto { Page = 1, PageSize = 10 };
            var logs = CreateSampleAudioUsageLogs(2);
            var pagedResult = new PagedResult<AudioUsageLog>
            {
                Items = logs,
                TotalCount = 2,
                Page = 1,
                PageSize = int.MaxValue,
                TotalPages = 1
            };

            _mockRepository.Setup(x => x.GetPagedAsync(It.IsAny<AudioUsageQueryDto>()))
                .ReturnsAsync(pagedResult);

            // Act
            var result = await _service.ExportUsageDataAsync(query, "json");

            // Assert
            result.Should().NotBeNullOrEmpty();
            result.Should().Contain("\"virtualKey\"");
            result.Should().Contain("\"providerId\"");
            result.Should().Contain("\"providerId\": 1"); // Provider ID 1 in JSON (with space)
            
            // Should be valid JSON
            var json = System.Text.Json.JsonDocument.Parse(result);
            json.RootElement.ValueKind.Should().Be(System.Text.Json.JsonValueKind.Array);
        }

        [Fact]
        public async Task ExportUsageDataAsync_WithUnsupportedFormat_ShouldThrowException()
        {
            // Arrange
            var query = new AudioUsageQueryDto { Page = 1, PageSize = 10 };
            var logs = CreateSampleAudioUsageLogs(3);
            var pagedResult = new PagedResult<AudioUsageLog>
            {
                Items = logs,
                TotalCount = 3,
                Page = 1,
                PageSize = int.MaxValue,
                TotalPages = 1
            };

            _mockRepository.Setup(x => x.GetPagedAsync(It.IsAny<AudioUsageQueryDto>()))
                .ReturnsAsync(pagedResult);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _service.ExportUsageDataAsync(query, "xml"));
        }

        #endregion
    }
}