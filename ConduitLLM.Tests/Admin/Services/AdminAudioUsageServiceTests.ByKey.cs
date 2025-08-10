using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration.DTOs.Audio;
using ConduitLLM.Configuration.Entities;
using FluentAssertions;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Admin.Services
{
    public partial class AdminAudioUsageServiceTests
    {
        #region GetUsageByKeyAsync Tests

        [Fact]
        public async Task GetUsageByKeyAsync_WithValidKey_ShouldReturnKeyUsage()
        {
            // Arrange
            var virtualKey = "test-key-hash";
            var logs = CreateSampleAudioUsageLogs(10);
            var key = new VirtualKey
            {
                KeyHash = virtualKey,
                KeyName = "Test API Key"
            };

            _mockRepository.Setup(x => x.GetByVirtualKeyAsync(virtualKey, It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync(logs);
            _mockVirtualKeyRepository.Setup(x => x.GetByKeyHashAsync(virtualKey, It.IsAny<CancellationToken>()))
                .ReturnsAsync(key);
            _mockRepository.Setup(x => x.GetOperationBreakdownAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), virtualKey))
                .ReturnsAsync(new List<OperationTypeBreakdown> 
                { 
                    new() { OperationType = "transcription", Count = 6, TotalCost = 3.0m }, 
                    new() { OperationType = "tts", Count = 4, TotalCost = 2.0m } 
                });
            _mockRepository.Setup(x => x.GetProviderBreakdownAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), virtualKey))
                .ReturnsAsync(new List<ProviderBreakdown> 
                { 
                    new() { ProviderId = 1, ProviderName = "OpenAI Test", Count = 10, TotalCost = 5.0m, SuccessRate = 100 } 
                });

            // Act
            var result = await _service.GetUsageByKeyAsync(virtualKey);

            // Assert
            result.Should().NotBeNull();
            result.VirtualKey.Should().Be(virtualKey);
            result.KeyName.Should().Be("Test API Key");
            result.TotalOperations.Should().Be(10);
            result.TotalCost.Should().Be(logs.Sum(l => l.Cost));
            result.SuccessRate.Should().Be(90); // 9 out of 10 logs are successful (one has status 500)
        }

        [Fact]
        public async Task GetUsageByKeyAsync_WithDateRange_ShouldFilterResults()
        {
            // Arrange
            var virtualKey = "test-key-hash";
            var startDate = DateTime.UtcNow.AddDays(-7);
            var endDate = DateTime.UtcNow;
            var logs = CreateSampleAudioUsageLogs(5);

            _mockRepository.Setup(x => x.GetByVirtualKeyAsync(virtualKey, startDate, endDate))
                .ReturnsAsync(logs);
            _mockVirtualKeyRepository.Setup(x => x.GetByKeyHashAsync(virtualKey, It.IsAny<CancellationToken>()))
                .ReturnsAsync((VirtualKey?)null);

            // Act
            var result = await _service.GetUsageByKeyAsync(virtualKey, startDate, endDate);

            // Assert
            result.TotalOperations.Should().Be(5);
            result.KeyName.Should().BeEmpty();
        }

        #endregion
    }
}