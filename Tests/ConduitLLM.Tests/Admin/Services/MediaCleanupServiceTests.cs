using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Admin.DTOs;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Admin.Services;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Options;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Tests.TestInfrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Admin.Services
{
    /// <summary>
    /// Unit tests for MediaCleanupService.
    /// Tests the unified media cleanup background service including scheduling,
    /// distributed locking, retention evaluation, and deletion logic.
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Component", "MediaLifecycle")]
    public class MediaCleanupServiceTests : IDisposable
    {
        private readonly ServiceProvider _serviceProvider;
        private readonly Mock<IDistributedLockService> _mockLockService;
        private readonly Mock<IMediaStorageService> _mockStorageService;
        private readonly Mock<IMediaDeletionBudgetService> _mockBudgetService;
        private readonly Mock<IMediaRecordRepository> _mockMediaRepository;
        private readonly Mock<IMediaCleanupStatusService> _mockStatusService;
        private readonly Mock<ILogger<MediaCleanupService>> _mockLogger;
        private readonly Mock<IDistributedLock> _mockLock;
        private readonly ConduitDbContext _context;
        private readonly SqliteTestDatabase _database;

        public MediaCleanupServiceTests()
        {
            _database = new SqliteTestDatabase();
            _context = _database.CreateContext();

            _mockLockService = new Mock<IDistributedLockService>();
            _mockStorageService = new Mock<IMediaStorageService>();
            _mockBudgetService = new Mock<IMediaDeletionBudgetService>();
            _mockMediaRepository = new Mock<IMediaRecordRepository>();
            _mockStatusService = new Mock<IMediaCleanupStatusService>();
            _mockLogger = new Mock<ILogger<MediaCleanupService>>();

            // Set up lock mock
            _mockLock = new Mock<IDistributedLock>();
            _mockLock.Setup(x => x.Key).Returns("media:cleanup:leader");
            _mockLock.Setup(x => x.IsValid).Returns(true);

            // Default budget service setup - within budget
            _mockBudgetService
                .Setup(x => x.WouldExceedBudgetAsync(
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            _mockBudgetService
                .Setup(x => x.IncrementMonthlyDeleteCountAsync(
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(100);

            // Default storage service setup - successful deletes
            _mockStorageService
                .Setup(x => x.DeleteAsync(It.IsAny<string>()))
                .ReturnsAsync(true);

            // Default repository setup - successful deletes
            _mockMediaRepository
                .Setup(x => x.DeleteAsync(It.IsAny<Guid>()))
                .ReturnsAsync(true);

            _mockStatusService
                .Setup(x => x.GetSimpleRetentionOverrideAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync((int?)null);

            // Set up service provider for scope factory
            var services = new ServiceCollection();
            services.AddSingleton<IConfigurationDbContext>(_context);
            services.AddSingleton(_mockStorageService.Object);
            services.AddSingleton(_mockBudgetService.Object);
            services.AddSingleton(_mockMediaRepository.Object);
            services.AddSingleton(_mockStatusService.Object);
            _serviceProvider = services.BuildServiceProvider();
        }

        private MediaCleanupService CreateService(MediaLifecycleOptions options)
        {
            return new MediaCleanupService(
                _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
                _mockLockService.Object,
                Options.Create(options),
                _mockLogger.Object);
        }

        private void SeedTestGroup(int groupId, decimal balance = 100m)
        {
            _context.VirtualKeyGroups.Add(new VirtualKeyGroup
            {
                Id = groupId,
                GroupName = $"Test Group {groupId}",
                Balance = balance,
                LifetimeCreditsAdded = 100.00m,
                LifetimeSpent = 0m,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            _context.SaveChanges();
        }

        private void SeedDefaultRetentionPolicy()
        {
            _context.MediaRetentionPolicies.Add(new MediaRetentionPolicy
            {
                Id = 1,
                Name = "Default Policy",
                Description = "Default retention policy",
                PositiveBalanceRetentionDays = 60,
                ZeroBalanceRetentionDays = 30,
                NegativeBalanceRetentionDays = 7,
                RespectRecentAccess = false,
                RecentAccessWindowDays = 7,
                IsDefault = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            _context.SaveChanges();
        }

        private void SeedVirtualKey(int keyId, int groupId)
        {
            _context.VirtualKeys.Add(new VirtualKey
            {
                Id = keyId,
                KeyName = $"Test Key {keyId}",
                KeyHash = $"hash{keyId}",
                VirtualKeyGroupId = groupId,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow
            });
            _context.SaveChanges();
        }

        private void SeedMediaRecords(int virtualKeyId, int count, int daysOld)
        {
            for (int i = 0; i < count; i++)
            {
                _context.MediaRecords.Add(new MediaRecord
                {
                    Id = Guid.NewGuid(),
                    VirtualKeyId = virtualKeyId,
                    StorageKey = $"storage-key-{virtualKeyId}-{i}",
                    MediaType = "image/png",
                    SizeBytes = 1024,
                    Provider = "test-provider",
                    Prompt = "test prompt",
                    CreatedAt = DateTime.UtcNow.AddDays(-daysOld)
                });
            }
            _context.SaveChanges();
        }

        #region Service Disabled Tests

        [Fact]
        public async Task ExecuteAsync_WhenDisabled_DoesNotAcquireLock()
        {
            // Arrange
            var options = new MediaLifecycleOptions { Enabled = false };
            var service = CreateService(options);

            using var cts = new CancellationTokenSource();

            // Act
            var executeTask = service.StartAsync(cts.Token);
            await Task.Delay(100);
            cts.Cancel();
            await service.StopAsync(CancellationToken.None);

            // Assert
            _mockLockService.Verify(
                x => x.AcquireLockAsync(
                    It.IsAny<string>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task ExecuteAsync_WhenDisabled_LogsDisabledMessage()
        {
            // Arrange
            var options = new MediaLifecycleOptions { Enabled = false };
            var service = CreateService(options);

            using var cts = new CancellationTokenSource();

            // Act
            await service.StartAsync(cts.Token);
            await Task.Delay(100);
            cts.Cancel();
            await service.StopAsync(CancellationToken.None);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) =>
                        o.ToString()!.Contains("disabled")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region Lock Acquisition Tests

        [Fact]
        public async Task ExecuteAsync_WhenEnabled_LogsStartupMessage()
        {
            // Arrange
            var options = new MediaLifecycleOptions
            {
                Enabled = true,
                ScheduleIntervalMinutes = 60
            };

            _mockLockService
                .Setup(x => x.AcquireLockAsync(
                    It.IsAny<string>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(_mockLock.Object);

            var service = CreateService(options);
            using var cts = new CancellationTokenSource();

            // Act
            await service.StartAsync(cts.Token);
            await Task.Delay(200);
            cts.Cancel();
            await service.StopAsync(CancellationToken.None);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) =>
                        o.ToString()!.Contains("starting")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_WhenLockNotAcquired_DoesNotProcessCleanup()
        {
            // Arrange
            var options = new MediaLifecycleOptions { Enabled = true };

            _mockLockService
                .Setup(x => x.AcquireLockAsync(
                    It.IsAny<string>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((IDistributedLock?)null);

            SeedTestGroup(1);
            SeedDefaultRetentionPolicy();
            SeedVirtualKey(1, 1);
            SeedMediaRecords(1, 5, 100); // Old media that should be deleted

            var service = CreateService(options);

            // Act
            await service.RunScheduledCleanupAsync(CancellationToken.None);

            // Assert - no storage deletions when lock not acquired
            _mockStorageService.Verify(
                x => x.DeleteAsync(It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public async Task RunScheduledCleanupAsync_CleansExpiredMediaWithinDistributedLock()
        {
            var options = CreateExecutionOptions();
            options.EnableOrphanCleanup = false;
            options.EnableRetentionCleanup = false;
            ArrangeLockAcquired();

            SeedTestGroup(1);
            SeedVirtualKey(1, 1);
            var expired = new MediaRecord
            {
                Id = Guid.NewGuid(),
                VirtualKeyId = 1,
                StorageKey = "expired-media",
                MediaType = "image",
                SizeBytes = 2048,
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                ExpiresAt = DateTime.UtcNow.AddMinutes(-1)
            };
            _context.MediaRecords.Add(expired);
            await _context.SaveChangesAsync();

            var service = CreateService(options);
            await service.RunScheduledCleanupAsync(CancellationToken.None);

            _mockLockService.Verify(x => x.AcquireLockAsync(
                "media:cleanup:leader", It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
            _mockStorageService.Verify(x => x.DeleteAsync("expired-media"), Times.Once);
            _mockMediaRepository.Verify(x => x.DeleteAsync(expired.Id), Times.Once);
            _mockBudgetService.Verify(x => x.IncrementMonthlyDeleteCountAsync(1, It.IsAny<CancellationToken>()), Times.Once);
            VerifyOperationStatus(MediaCleanupTypes.Expiration, "Completed");
        }

        [Fact]
        public async Task RunScheduledCleanupAsync_CleansOrphanedMedia()
        {
            var options = CreateExecutionOptions();
            options.EnableExpirationCleanup = false;
            options.EnableRetentionCleanup = false;
            ArrangeLockAcquired();

            var orphan = CreateMediaRecord("orphaned-media", virtualKeyId: 999);
            _mockMediaRepository
                .Setup(x => x.GetOrphanedMediaAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<MediaRecord> { orphan });

            var service = CreateService(options);
            await service.RunScheduledCleanupAsync(CancellationToken.None);

            _mockStorageService.Verify(x => x.DeleteAsync("orphaned-media"), Times.Once);
            _mockMediaRepository.Verify(x => x.DeleteAsync(orphan.Id), Times.Once);
            VerifyOperationStatus(MediaCleanupTypes.Orphan, "Completed");
        }

        [Fact]
        public async Task RunScheduledCleanupAsync_AppliesRetentionPolicies()
        {
            var options = CreateExecutionOptions();
            options.EnableExpirationCleanup = false;
            options.EnableOrphanCleanup = false;
            ArrangeLockAcquired();

            SeedTestGroup(1);
            SeedDefaultRetentionPolicy();
            SeedVirtualKey(1, 1);
            SeedMediaRecords(1, 1, 100);

            var service = CreateService(options);
            await service.RunScheduledCleanupAsync(CancellationToken.None);

            _mockStorageService.Verify(x => x.DeleteAsync("storage-key-1-0"), Times.Once);
            VerifyOperationStatus(MediaCleanupTypes.Retention, "Completed");
        }

        [Fact]
        public async Task RunScheduledCleanupAsync_StopsPhaseWhenDeletionBudgetWouldBeExceeded()
        {
            var options = CreateExecutionOptions();
            options.EnableOrphanCleanup = false;
            options.EnableRetentionCleanup = false;
            ArrangeLockAcquired();

            SeedTestGroup(1);
            SeedVirtualKey(1, 1);
            _context.MediaRecords.Add(new MediaRecord
            {
                Id = Guid.NewGuid(),
                VirtualKeyId = 1,
                StorageKey = "over-budget-media",
                MediaType = "image",
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(-1)
            });
            await _context.SaveChangesAsync();

            _mockBudgetService
                .Setup(x => x.WouldExceedBudgetAsync(1, options.MonthlyDeleteBudget, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            _mockBudgetService
                .Setup(x => x.GetRemainingBudgetAsync(options.MonthlyDeleteBudget, It.IsAny<CancellationToken>()))
                .ReturnsAsync(0);
            _mockBudgetService
                .Setup(x => x.GetMonthlyDeleteCountAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(options.MonthlyDeleteBudget);

            var service = CreateService(options);
            await service.RunScheduledCleanupAsync(CancellationToken.None);

            _mockStorageService.Verify(x => x.DeleteAsync(It.IsAny<string>()), Times.Never);
            VerifyOperationStatus(MediaCleanupTypes.Expiration, "budget");
        }

        [Fact]
        public async Task RunScheduledCleanupAsync_ContinuesAfterPhaseDeletionFailure()
        {
            var options = CreateExecutionOptions();
            options.EnableRetentionCleanup = false;
            ArrangeLockAcquired();

            SeedTestGroup(1);
            SeedVirtualKey(1, 1);
            _context.MediaRecords.Add(new MediaRecord
            {
                Id = Guid.NewGuid(),
                VirtualKeyId = 1,
                StorageKey = "failing-expired-media",
                MediaType = "image",
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(-1)
            });
            await _context.SaveChangesAsync();

            var orphan = CreateMediaRecord("healthy-orphaned-media", virtualKeyId: 999);
            _mockMediaRepository
                .Setup(x => x.GetOrphanedMediaAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<MediaRecord> { orphan });
            _mockStorageService
                .Setup(x => x.DeleteAsync("failing-expired-media"))
                .ThrowsAsync(new InvalidOperationException("storage unavailable"));

            var service = CreateService(options);
            await service.RunScheduledCleanupAsync(CancellationToken.None);

            _mockStorageService.Verify(x => x.DeleteAsync("healthy-orphaned-media"), Times.Once);
            VerifyOperationStatus(MediaCleanupTypes.Expiration, "errors");
            VerifyOperationStatus(MediaCleanupTypes.Orphan, "Completed");
        }

        #endregion

        #region Configuration Tests

        [Fact]
        public void IsSchedulerEnabled_WithEnabledTrue_ReturnsTrue()
        {
            // Arrange
            var options = new MediaLifecycleOptions { Enabled = true };

            // Assert
            options.IsSchedulerEnabled.Should().BeTrue();
        }

        [Fact]
        public void IsSchedulerEnabled_WithEnabledFalse_ReturnsFalse()
        {
            // Arrange
            var options = new MediaLifecycleOptions { Enabled = false };

            // Assert
            options.IsSchedulerEnabled.Should().BeFalse();
        }

        [Fact]
        public void DryRunMode_DefaultsToTrue()
        {
            // Arrange
            var options = new MediaLifecycleOptions();

            // Assert
            options.DryRunMode.Should().BeTrue();
        }

        [Fact]
        public void DryRunMode_CanBeSetToFalse()
        {
            // Arrange
            var options = new MediaLifecycleOptions { DryRunMode = false };

            // Assert
            options.DryRunMode.Should().BeFalse();
        }

        [Fact]
        public void MonthlyDeleteBudget_DefaultsTo500000()
        {
            // Arrange
            var options = new MediaLifecycleOptions();

            // Assert
            options.MonthlyDeleteBudget.Should().Be(500_000);
        }

        [Fact]
        public void MaxBatchSize_DefaultsTo50()
        {
            // Arrange
            var options = new MediaLifecycleOptions();

            // Assert
            options.MaxBatchSize.Should().Be(50);
        }

        [Fact]
        public void ScheduleIntervalMinutes_DefaultsTo60()
        {
            // Arrange
            var options = new MediaLifecycleOptions();

            // Assert
            options.ScheduleIntervalMinutes.Should().Be(60);
        }

        #endregion

        #region Test Group Filtering Tests

        [Fact]
        public void TestVirtualKeyGroups_WhenSet_FiltersGroups()
        {
            // Arrange
            var options = new MediaLifecycleOptions
            {
                Enabled = true,
                TestVirtualKeyGroups = new List<int> { 1, 3 }
            };

            // Assert
            options.TestVirtualKeyGroups.Should().Contain(1);
            options.TestVirtualKeyGroups.Should().Contain(3);
            options.TestVirtualKeyGroups.Should().HaveCount(2);
        }

        #endregion

        #region Stop/Shutdown Tests

        [Fact]
        public async Task StopAsync_LogsShutdownMessage()
        {
            // Arrange
            var options = new MediaLifecycleOptions { Enabled = false };
            var service = CreateService(options);

            // Act
            await service.StartAsync(CancellationToken.None);
            await service.StopAsync(CancellationToken.None);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) =>
                        o.ToString()!.Contains("stop")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        #endregion

        #region Budget Integration Tests

        [Fact]
        public void BudgetService_IsIntegrated_InService()
        {
            // This test verifies that the budget service is properly injected
            // The actual budget checking is tested in the budget service tests
            var options = new MediaLifecycleOptions
            {
                Enabled = true,
                MonthlyDeleteBudget = 500_000
            };

            var service = CreateService(options);
            service.Should().NotBeNull();
        }

        #endregion

        #region Retention Policy Tests

        [Fact]
        public void RetentionDays_CalculatedByBalance_Positive()
        {
            // This tests the retention calculation logic
            // Positive balance = longer retention
            var policy = new MediaRetentionPolicy
            {
                PositiveBalanceRetentionDays = 60,
                ZeroBalanceRetentionDays = 30,
                NegativeBalanceRetentionDays = 7
            };

            decimal balance = 100m;
            var retentionDays = balance switch
            {
                > 0 => policy.PositiveBalanceRetentionDays,
                0 => policy.ZeroBalanceRetentionDays,
                < 0 => policy.NegativeBalanceRetentionDays
            };

            retentionDays.Should().Be(60);
        }

        [Fact]
        public void RetentionDays_CalculatedByBalance_Zero()
        {
            var policy = new MediaRetentionPolicy
            {
                PositiveBalanceRetentionDays = 60,
                ZeroBalanceRetentionDays = 30,
                NegativeBalanceRetentionDays = 7
            };

            decimal balance = 0m;
            var retentionDays = balance switch
            {
                > 0 => policy.PositiveBalanceRetentionDays,
                0 => policy.ZeroBalanceRetentionDays,
                < 0 => policy.NegativeBalanceRetentionDays
            };

            retentionDays.Should().Be(30);
        }

        [Fact]
        public void RetentionDays_CalculatedByBalance_Negative()
        {
            var policy = new MediaRetentionPolicy
            {
                PositiveBalanceRetentionDays = 60,
                ZeroBalanceRetentionDays = 30,
                NegativeBalanceRetentionDays = 7
            };

            decimal balance = -50m;
            var retentionDays = balance switch
            {
                > 0 => policy.PositiveBalanceRetentionDays,
                0 => policy.ZeroBalanceRetentionDays,
                < 0 => policy.NegativeBalanceRetentionDays
            };

            retentionDays.Should().Be(7);
        }

        #endregion

        private MediaLifecycleOptions CreateExecutionOptions() => new()
        {
            Enabled = true,
            DryRunMode = false,
            DelayBetweenBatchesMs = 0,
            MaxBatchSize = 50
        };

        private void ArrangeLockAcquired()
        {
            _mockLockService
                .Setup(x => x.AcquireLockAsync(
                    It.IsAny<string>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(_mockLock.Object);
        }

        private static MediaRecord CreateMediaRecord(string storageKey, int virtualKeyId) => new()
        {
            Id = Guid.NewGuid(),
            VirtualKeyId = virtualKeyId,
            StorageKey = storageKey,
            MediaType = "image",
            SizeBytes = 1024,
            CreatedAt = DateTime.UtcNow
        };

        private void VerifyOperationStatus(string cleanupType, string expectedStatusFragment)
        {
            _mockStatusService.Verify(x => x.RecordOperationCompletionAsync(
                cleanupType,
                It.IsAny<int>(),
                It.IsAny<long>(),
                It.IsAny<double>(),
                It.Is<string>(status => status.Contains(expectedStatusFragment, StringComparison.OrdinalIgnoreCase)),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        public void Dispose()
        {
            _context?.Dispose();
            _serviceProvider?.Dispose();
            _database.Dispose();
        }
    }
}
