using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using FluentAssertions;
using Xunit;

namespace ConduitLLM.Tests.Configuration.Repositories
{
    public partial class AudioUsageLogRepositoryTests
    {
        #region GetUsageSummaryAsync Tests

        [Fact]
        public async Task GetUsageSummaryAsync_WithNoFilters_ShouldReturnFullSummary()
        {
            // Arrange
            await SeedTestDataAsync(50);
            var startDate = DateTime.UtcNow.AddDays(-30);
            var endDate = DateTime.UtcNow;

            // Act
            var result = await _repository.GetUsageSummaryAsync(startDate, endDate);

            // Assert
            result.Should().NotBeNull();
            result.TotalOperations.Should().BeGreaterThan(0);
            result.TotalCost.Should().BeGreaterThan(0);
            result.SuccessfulOperations.Should().BeGreaterThan(0);
            result.OperationBreakdown.Should().NotBeEmpty();
            result.ProviderBreakdown.Should().NotBeEmpty();
            result.VirtualKeyBreakdown.Should().NotBeEmpty();
        }

        [Fact]
        public async Task GetUsageSummaryAsync_WithVirtualKeyFilter_ShouldReturnFilteredSummary()
        {
            // Arrange
            await SeedTestDataAsync(20, maxDaysAgo: 6); // Keep all data within 6 days for 7-day window query
            var startDate = DateTime.UtcNow.AddDays(-7);
            var endDate = DateTime.UtcNow;

            // Act
            var result = await _repository.GetUsageSummaryAsync(startDate, endDate, "key-1");

            // Assert
            result.TotalOperations.Should().BeGreaterThan(0);
            // Should only count operations for key-1
            var key1Logs = await _context.AudioUsageLogs
                .Where(l => l.VirtualKey == "key-1" && l.Timestamp >= startDate && l.Timestamp <= endDate)
                .ToListAsync();
            result.TotalOperations.Should().Be(key1Logs.Count);
        }

        #endregion
    }
}