using System;
using System.Linq;
using System.Threading.Tasks;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.DTOs.Audio;
using ConduitLLM.Configuration.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ConduitLLM.Tests.Configuration.Repositories
{
    public partial class AudioUsageLogRepositoryTests
    {
        #region GetPagedAsync Tests

        [Fact]
        public async Task GetPagedAsync_WithNoFilters_ShouldReturnAllLogs()
        {
            // Arrange
            await SeedTestDataAsync(15);
            var query = new AudioUsageQueryDto
            {
                Page = 1,
                PageSize = 10
            };

            // Act
            var result = await _repository.GetPagedAsync(query);

            // Assert
            result.Should().NotBeNull();
            result.Items.Should().HaveCount(10);
            result.TotalCount.Should().Be(15);
            result.TotalPages.Should().Be(2);
            result.Page.Should().Be(1);
            result.PageSize.Should().Be(10);
        }

        [Fact]
        public async Task GetPagedAsync_WithVirtualKeyFilter_ShouldReturnFilteredResults()
        {
            // Arrange
            await SeedTestDataAsync(10);
            var query = new AudioUsageQueryDto
            {
                Page = 1,
                PageSize = 10,
                VirtualKey = "key-1"
            };

            // Act
            var result = await _repository.GetPagedAsync(query);

            // Assert
            result.Items.Should().OnlyContain(log => log.VirtualKey == "key-1");
            result.TotalCount.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task GetPagedAsync_WithProviderFilter_ShouldReturnFilteredResults()
        {
            // Arrange
            await SeedTestDataAsync(10);
            
            // Get one of the providers created by SeedTestDataAsync
            var provider = await _context.Providers.FirstAsync();
            
            var query = new AudioUsageQueryDto
            {
                Page = 1,
                PageSize = 10,
                ProviderId = provider.Id
            };

            // Act
            var result = await _repository.GetPagedAsync(query);

            // Assert
            result.Items.Should().NotBeEmpty();
            result.Items.Should().OnlyContain(log => log.ProviderId == provider.Id);
        }

        [Fact]
        public async Task GetPagedAsync_WithDateRange_ShouldReturnFilteredResults()
        {
            // Arrange
            await SeedTestDataAsync(10, maxDaysAgo: 10); // Spread data across 10 days to ensure some fall in the date range
            var startDate = DateTime.UtcNow.AddDays(-7);
            var endDate = DateTime.UtcNow.AddDays(-3);
            
            var query = new AudioUsageQueryDto
            {
                Page = 1,
                PageSize = 10,
                StartDate = startDate,
                EndDate = endDate
            };

            // Act
            var result = await _repository.GetPagedAsync(query);

            // Assert
            result.Items.Should().OnlyContain(log => 
                log.Timestamp >= startDate && log.Timestamp <= endDate);
        }

        [Fact]
        public async Task GetPagedAsync_WithOnlyErrors_ShouldReturnErrorLogs()
        {
            // Arrange
            await SeedTestDataAsync(10);
            var query = new AudioUsageQueryDto
            {
                Page = 1,
                PageSize = 10,
                OnlyErrors = true
            };

            // Act
            var result = await _repository.GetPagedAsync(query);

            // Assert
            result.Items.Should().OnlyContain(log => 
                log.StatusCode == null || log.StatusCode >= 400);
        }

        [Fact]
        public async Task GetPagedAsync_WithMultipleFilters_ShouldApplyAllFilters()
        {
            // Arrange
            await SeedTestDataAsync(20);
            
            // Get one of the providers created by SeedTestDataAsync
            var provider = await _context.Providers.FirstAsync();
            
            var query = new AudioUsageQueryDto
            {
                Page = 1,
                PageSize = 10,
                ProviderId = provider.Id,
                OperationType = "transcription",
                StartDate = DateTime.UtcNow.AddDays(-5)
            };

            // Act
            var result = await _repository.GetPagedAsync(query);

            // Assert
            // Some logs should match these criteria (not all, since we're filtering)
            if (result.Items.Count() > 0)
            {
                result.Items.Should().OnlyContain(log => 
                    log.ProviderId == provider.Id &&
                    log.OperationType.ToLower() == "transcription" &&
                    log.Timestamp >= query.StartDate);
            }
        }

        [Fact]
        public async Task GetPagedAsync_WithPageSizeExceedingMax_ShouldCapPageSize()
        {
            // Arrange
            await SeedTestDataAsync(2000);
            var query = new AudioUsageQueryDto
            {
                Page = 1,
                PageSize = 2000 // Exceeds max of 1000
            };

            // Act
            var result = await _repository.GetPagedAsync(query);

            // Assert
            result.Items.Should().HaveCount(1000); // Should be capped at max
            result.PageSize.Should().Be(1000);
        }

        [Fact]
        public async Task GetPagedAsync_ShouldOrderByTimestampDescending()
        {
            // Arrange
            await SeedTestDataAsync(5);
            var query = new AudioUsageQueryDto
            {
                Page = 1,
                PageSize = 10
            };

            // Act
            var result = await _repository.GetPagedAsync(query);

            // Assert
            result.Items.Should().BeInDescendingOrder(log => log.Timestamp);
        }

        #endregion
    }
}