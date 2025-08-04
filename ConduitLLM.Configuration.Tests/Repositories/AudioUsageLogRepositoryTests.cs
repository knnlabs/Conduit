using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Data;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.DTOs.Audio;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;

namespace ConduitLLM.Configuration.Tests.Repositories
{
    /// <summary>
    /// Unit tests for the AudioUsageLogRepository class.
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Component", "Repository")]
    public class AudioUsageLogRepositoryTests : IDisposable
    {
        private readonly ConfigurationDbContext _context;
        private readonly AudioUsageLogRepository _repository;
        private readonly ITestOutputHelper _output;

        public AudioUsageLogRepositoryTests(ITestOutputHelper output)
        {
            _output = output;
            
            var options = new DbContextOptionsBuilder<ConfigurationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(warnings => warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.CoreEventId.TransactionIgnoredWarning))
                .Options;

            _context = new ConfigurationDbContext(options);
            _context.IsTestEnvironment = true;
            _repository = new AudioUsageLogRepository(_context);
        }

        #region CreateAsync Tests

        [Fact]
        public async Task CreateAsync_WithValidLog_ShouldPersistLog()
        {
            // Arrange
            var provider = new Provider { ProviderType = ProviderType.OpenAI, ProviderName = "OpenAI" };
            _context.Providers.Add(provider);
            await _context.SaveChangesAsync();

            var log = new AudioUsageLog
            {
                VirtualKey = "test-key-hash",
                ProviderId = provider.Id,
                OperationType = "transcription",
                Model = "whisper-1",
                RequestId = Guid.NewGuid().ToString(),
                DurationSeconds = 15.5,
                CharacterCount = 1000,
                Cost = 0.15m,
                Language = "en",
                StatusCode = 200
            };

            // Act
            var result = await _repository.CreateAsync(log);

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().BeGreaterThan(0);
            result.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

            var savedLog = await _context.AudioUsageLogs.FindAsync(result.Id);
            savedLog.Should().NotBeNull();
            savedLog!.VirtualKey.Should().Be("test-key-hash");
            savedLog.ProviderId.Should().Be(provider.Id);
        }

        [Fact]
        public async Task CreateAsync_WithErrorLog_ShouldPersistWithError()
        {
            // Arrange
            var provider = new Provider { ProviderType = ProviderType.AzureOpenAI, ProviderName = "Azure OpenAI" };
            _context.Providers.Add(provider);
            await _context.SaveChangesAsync();

            var log = new AudioUsageLog
            {
                VirtualKey = "test-key-hash",
                ProviderId = provider.Id,
                OperationType = "tts",
                Model = "tts-1",
                RequestId = Guid.NewGuid().ToString(),
                StatusCode = 500,
                ErrorMessage = "Internal server error",
                Cost = 0m
            };

            // Act
            var result = await _repository.CreateAsync(log);

            // Assert
            result.StatusCode.Should().Be(500);
            result.ErrorMessage.Should().Be("Internal server error");
            result.Cost.Should().Be(0);
        }

        #endregion

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
            var provider = new Provider { ProviderType = ProviderType.OpenAI, ProviderName = "OpenAI" };
            _context.Providers.Add(provider);
            await _context.SaveChangesAsync();
            
            await SeedTestDataAsync(10);
            var query = new AudioUsageQueryDto
            {
                Page = 1,
                PageSize = 10,
                ProviderId = provider.Id
            };

            // Act
            var result = await _repository.GetPagedAsync(query);

            // Assert
            result.Items.Should().OnlyContain(log => log.ProviderId == provider.Id);
        }

        [Fact]
        public async Task GetPagedAsync_WithDateRange_ShouldReturnFilteredResults()
        {
            // Arrange
            await SeedTestDataAsync(10);
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
            var provider = new Provider { ProviderType = ProviderType.OpenAI, ProviderName = "OpenAI" };
            _context.Providers.Add(provider);
            await _context.SaveChangesAsync();
            
            await SeedTestDataAsync(20);
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
            result.Items.Should().OnlyContain(log => 
                log.ProviderId == provider.Id &&
                log.OperationType.ToLower() == "transcription" &&
                log.Timestamp >= query.StartDate);
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
            await SeedTestDataAsync(20);
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
            var provider = new Provider { ProviderType = ProviderType.OpenAI, ProviderName = "OpenAI" };
            _context.Providers.Add(provider);
            await _context.SaveChangesAsync();
            
            await SeedTestDataAsync(10);

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
            await SeedTestDataAsync(30);
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
            await SeedTestDataAsync(30);
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

        #region Helper Methods

        private async Task SeedTestDataAsync(int count)
        {
            // Create test providers
            var openAiProvider = new Provider { ProviderType = ProviderType.OpenAI, ProviderName = "OpenAI" };
            var azureProvider = new Provider { ProviderType = ProviderType.AzureOpenAI, ProviderName = "Azure OpenAI" };
            _context.Providers.AddRange(openAiProvider, azureProvider);
            await _context.SaveChangesAsync();

            var logs = new List<AudioUsageLog>();
            var random = new Random();
            
            for (int i = 0; i < count; i++)
            {
                var operationType = i % 3 == 0 ? "transcription" : i % 3 == 1 ? "tts" : "realtime";
                var provider = i % 2 == 0 ? openAiProvider : azureProvider;
                var statusCode = i % 10 == 0 ? 500 : 200;
                
                logs.Add(new AudioUsageLog
                {
                    VirtualKey = $"key-{i % 3}",
                    ProviderId = provider.Id,
                    OperationType = operationType,
                    Model = provider.ProviderType == ProviderType.OpenAI ? "whisper-1" : "azure-tts",
                    RequestId = Guid.NewGuid().ToString(),
                    SessionId = operationType == "realtime" ? Guid.NewGuid().ToString() : null,
                    DurationSeconds = random.Next(1, 60),
                    CharacterCount = random.Next(100, 5000),
                    Cost = (decimal)(random.NextDouble() * 2),
                    Language = "en",
                    Voice = operationType == "tts" ? "alloy" : null,
                    StatusCode = statusCode,
                    ErrorMessage = statusCode >= 400 ? "Error occurred" : null,
                    IpAddress = $"192.168.1.{i % 255}",
                    UserAgent = "Test/1.0",
                    Timestamp = DateTime.UtcNow.AddDays(-random.Next(0, 30))
                });
            }

            _context.AudioUsageLogs.AddRange(logs);
            await _context.SaveChangesAsync();
        }

        public void Dispose()
        {
            _context?.Dispose();
        }

        #endregion
    }
}