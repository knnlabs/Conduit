using System;
using System.Threading.Tasks;

using ConduitLLM.Admin.Controllers;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.DTOs.Audio;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using Moq;

using Xunit;

namespace ConduitLLM.Admin.Tests.Controllers
{
    /// <summary>
    /// Tests for the AudioConfigurationController export functionality.
    /// </summary>
    public class AudioConfigurationControllerExportTests
    {
        private readonly Mock<IAdminAudioProviderService> _mockProviderService;
        private readonly Mock<IAdminAudioCostService> _mockCostService;
        private readonly Mock<IAdminAudioUsageService> _mockUsageService;
        private readonly Mock<ILogger<AudioConfigurationController>> _mockLogger;
        private readonly AudioConfigurationController _controller;

        public AudioConfigurationControllerExportTests()
        {
            _mockProviderService = new Mock<IAdminAudioProviderService>();
            _mockCostService = new Mock<IAdminAudioCostService>();
            _mockUsageService = new Mock<IAdminAudioUsageService>();
            _mockLogger = new Mock<ILogger<AudioConfigurationController>>();

            _controller = new AudioConfigurationController(
                _mockProviderService.Object,
                _mockCostService.Object,
                _mockUsageService.Object,
                _mockLogger.Object);
        }

        [Fact]
        public async Task ExportUsageData_ReturnsFileResult_WhenFormatIsCSV()
        {
            // Arrange
            var query = new AudioUsageQueryDto
            {
                Page = 1,
                PageSize = int.MaxValue,
                StartDate = DateTime.UtcNow.AddDays(-7),
                EndDate = DateTime.UtcNow
            };
            var expectedCsvData = "Timestamp,VirtualKey,Provider,Operation,Model,Duration,Cost,Status,Language,Voice\n2024-01-01 10:00:00,key1,openai,transcription,whisper-1,120.5,0.0025,200,en,\n";

            _mockUsageService
                .Setup(s => s.ExportUsageDataAsync(It.IsAny<AudioUsageQueryDto>(), "csv"))
                .ReturnsAsync(expectedCsvData);

            // Act
            var result = await _controller.ExportUsageData(query, "csv");

            // Assert
            var fileResult = Assert.IsType<FileResult>(result);
            Assert.Equal("text/csv", fileResult.ContentType);
            Assert.Contains("audio_usage_", fileResult.FileDownloadName);
            Assert.EndsWith(".csv", fileResult.FileDownloadName);
        }

        [Fact]
        public async Task ExportUsageData_ReturnsFileResult_WhenFormatIsJSON()
        {
            // Arrange
            var query = new AudioUsageQueryDto
            {
                Page = 1,
                PageSize = int.MaxValue,
                StartDate = DateTime.UtcNow.AddDays(-7),
                EndDate = DateTime.UtcNow
            };
            var expectedJsonData = "[{\"timestamp\":\"2024-01-01T10:00:00Z\",\"virtualKey\":\"key1\",\"provider\":\"openai\"}]";

            _mockUsageService
                .Setup(s => s.ExportUsageDataAsync(It.IsAny<AudioUsageQueryDto>(), "json"))
                .ReturnsAsync(expectedJsonData);

            // Act
            var result = await _controller.ExportUsageData(query, "json");

            // Assert
            var fileResult = Assert.IsType<FileResult>(result);
            Assert.Equal("application/json", fileResult.ContentType);
            Assert.Contains("audio_usage_", fileResult.FileDownloadName);
            Assert.EndsWith(".json", fileResult.FileDownloadName);
        }

        [Fact]
        public async Task ExportUsageData_ReturnsBadRequest_WhenFormatIsUnsupported()
        {
            // Arrange
            var query = new AudioUsageQueryDto();

            _mockUsageService
                .Setup(s => s.ExportUsageDataAsync(It.IsAny<AudioUsageQueryDto>(), "xml"))
                .ThrowsAsync(new ArgumentException("Unsupported export format"));

            // Act
            var result = await _controller.ExportUsageData(query, "xml");

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequestResult.Value);
        }

        [Fact]
        public async Task ExportUsageData_DefaultsToCSV_WhenNoFormatSpecified()
        {
            // Arrange
            var query = new AudioUsageQueryDto();
            var expectedCsvData = "test,csv,data";

            _mockUsageService
                .Setup(s => s.ExportUsageDataAsync(It.IsAny<AudioUsageQueryDto>(), "csv"))
                .ReturnsAsync(expectedCsvData);

            // Act
            var result = await _controller.ExportUsageData(query);

            // Assert
            var fileResult = Assert.IsType<FileResult>(result);
            Assert.Equal("text/csv", fileResult.ContentType);
            Assert.EndsWith(".csv", fileResult.FileDownloadName);
            
            // Verify that CSV format was requested from the service
            _mockUsageService.Verify(s => s.ExportUsageDataAsync(It.IsAny<AudioUsageQueryDto>(), "csv"), Times.Once);
        }
    }
}