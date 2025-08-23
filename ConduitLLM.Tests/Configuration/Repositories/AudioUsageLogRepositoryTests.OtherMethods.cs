using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

namespace ConduitLLM.Tests.Configuration.Repositories
{
    public partial class AudioUsageLogRepositoryTests
    {
        #region Other Repository Methods Tests

        [Fact]
        public async Task GetByVirtualKeyAsync_ShouldReturnLogsForKey()
        {
            // Arrange
            await SeedTestDataAsync(10);

            // Act
            var result = await _repository.GetByVirtualKeyAsync("key-1");

            // Assert
            result.Should().NotBeEmpty();
            result.Should().OnlyContain(log => log.VirtualKey == "key-1");
        }

        [Fact]
        public async Task GetByProviderAsync_ShouldReturnLogsForProvider()
        {
            // Arrange
            // SeedTestDataAsync creates its own providers, so we need to get one of those
            await SeedTestDataAsync(10);
            
            // Get the first provider that was created by SeedTestDataAsync
            var provider = await _context.Providers.FirstAsync();

            // Act
            var result = await _repository.GetByProviderAsync(provider.Id);

            // Assert
            result.Should().NotBeEmpty();
            result.Should().OnlyContain(log => log.ProviderId == provider.Id);
        }

        [Fact]
        public async Task GetOperationBreakdownAsync_ShouldReturnCorrectCounts()
        {
            // Arrange
            await SeedTestDataAsync(30, maxDaysAgo: 6); // Keep all data within 6 days for 7-day window query
            var startDate = DateTime.UtcNow.AddDays(-7);
            var endDate = DateTime.UtcNow;

            // Act
            var result = await _repository.GetOperationBreakdownAsync(startDate, endDate);

            // Assert
            result.Should().NotBeEmpty();
            result.Should().Contain(b => b.OperationType == "transcription");
            result.Should().Contain(b => b.OperationType == "tts");
            result.Should().Contain(b => b.OperationType == "realtime");
            result.Sum(b => b.Count).Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task GetProviderBreakdownAsync_ShouldReturnCorrectCounts()
        {
            // Arrange
            await SeedTestDataAsync(30, maxDaysAgo: 6); // Keep all data within 6 days for 7-day window query
            var startDate = DateTime.UtcNow.AddDays(-7);
            var endDate = DateTime.UtcNow;

            // Act
            var result = await _repository.GetProviderBreakdownAsync(startDate, endDate);

            // Assert
            result.Should().NotBeEmpty();
            result.Should().Contain(b => b.ProviderName.ToLower().Contains("openai"));
            result.Should().Contain(b => b.ProviderName.ToLower().Contains("azure"));
            result.Sum(b => b.Count).Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task DeleteOldLogsAsync_ShouldDeleteLogsBeforeCutoff()
        {
            // Arrange
            var provider = new Provider { ProviderType = ProviderType.OpenAI, ProviderName = "OpenAI" };
            _context.Providers.Add(provider);
            await _context.SaveChangesAsync();

            // Add some old logs
            var oldLogs = new List<AudioUsageLog>();
            for (int i = 0; i < 10; i++)
            {
                oldLogs.Add(new AudioUsageLog
                {
                    VirtualKey = "old-key",
                    ProviderId = provider.Id,
                    OperationType = "transcription",
                    Model = "whisper-1",
                    Timestamp = DateTime.UtcNow.AddDays(-60),
                    Cost = 0.1m
                });
            }
            _context.AudioUsageLogs.AddRange(oldLogs);
            
            // Add some recent logs
            var recentLogs = new List<AudioUsageLog>();
            for (int i = 0; i < 5; i++)
            {
                recentLogs.Add(new AudioUsageLog
                {
                    VirtualKey = "recent-key",
                    ProviderId = provider.Id,
                    OperationType = "transcription",
                    Model = "whisper-1",
                    Timestamp = DateTime.UtcNow.AddDays(-5),
                    Cost = 0.1m
                });
            }
            _context.AudioUsageLogs.AddRange(recentLogs);
            await _context.SaveChangesAsync();

            var cutoffDate = DateTime.UtcNow.AddDays(-30);

            // Act
            var deletedCount = await _repository.DeleteOldLogsAsync(cutoffDate);

            // Assert
            deletedCount.Should().Be(10);
            var remainingLogs = await _context.AudioUsageLogs.ToListAsync();
            remainingLogs.Should().HaveCount(5);
            remainingLogs.Should().OnlyContain(log => log.Timestamp > cutoffDate);
        }

        #endregion
    }
}