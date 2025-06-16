using System;
using System.Threading.Tasks;

using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Configuration.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Moq;

using Xunit;

namespace ConduitLLM.Tests.Services
{
    public class VirtualKeyMaintenanceServiceTests
    {
        private DbContextOptions<ConfigurationDbContext> GetDbOptions()
        {
            return new DbContextOptionsBuilder<ConfigurationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
        }

        private ConfigurationDbContext CreateTestContext(DbContextOptions<ConfigurationDbContext> options)
        {
            var context = new ConfigurationDbContext(options);
            context.IsTestEnvironment = true;
            return context;
        }

        [Fact]
        public async Task ProcessDailyBudgetResets_ShouldReset_WhenDayHasPassed()
        {
            // Arrange
            var options = GetDbOptions();
            var yesterday = DateTime.UtcNow.AddDays(-1).Date;

            using (var context = CreateTestContext(options))
            {
                context.VirtualKeys.Add(new VirtualKey
                {
                    Id = 1,
                    KeyName = "Daily Reset Key",
                    KeyHash = "vk_daily_reset",
                    IsEnabled = true,
                    MaxBudget = 5.0m,
                    CurrentSpend = 3.0m,
                    BudgetDuration = "daily",
                    BudgetStartDate = yesterday
                });
                await context.SaveChangesAsync();
            }

            // Act
            using (var context = CreateTestContext(options))
            {
                // Create mock repositories with properly set up return values
                var mockVirtualKeyRepo = new Mock<IVirtualKeyRepository>();
                mockVirtualKeyRepo.Setup(repo => repo.GetAllAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(await context.VirtualKeys.ToListAsync());

                var mockSpendHistoryRepo = new Mock<IVirtualKeySpendHistoryRepository>();
                mockSpendHistoryRepo.Setup(repo => repo.CreateAsync(It.IsAny<VirtualKeySpendHistory>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(1)
                    .Callback<VirtualKeySpendHistory, CancellationToken>((history, _) =>
                    {
                        // Add the history to the database
                        context.VirtualKeySpendHistory.Add(history);
                        context.SaveChanges();
                    });

                mockVirtualKeyRepo.Setup(repo => repo.UpdateAsync(It.IsAny<VirtualKey>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true)
                    .Callback<VirtualKey, CancellationToken>((key, _) =>
                    {
                        // Update the key in the database
                        var dbKey = context.VirtualKeys.Find(key.Id);
                        if (dbKey != null)
                        {
                            dbKey.CurrentSpend = key.CurrentSpend;
                            dbKey.BudgetStartDate = key.BudgetStartDate;
                            dbKey.UpdatedAt = key.UpdatedAt;
                            context.SaveChanges();
                        }
                    });

                var mockLogger = new Mock<ILogger<VirtualKeyMaintenanceService>>();
                var maintenanceService = new VirtualKeyMaintenanceService(
                    mockVirtualKeyRepo.Object,
                    mockSpendHistoryRepo.Object,
                    mockLogger.Object);

                await maintenanceService.ProcessBudgetResetsAsync();
            }

            // Assert
            using (var context = CreateTestContext(options))
            {
                var key = await context.VirtualKeys.FindAsync(1);

                Assert.NotNull(key);
                Assert.Equal(0m, key.CurrentSpend); // Should be reset to 0
                Assert.NotNull(key.BudgetStartDate);
                Assert.Equal(DateTime.UtcNow.Date, key.BudgetStartDate?.Date); // Should be updated to today

                // Verify we created a spend history entry
                var history = await context.VirtualKeySpendHistory.FirstOrDefaultAsync();
                Assert.NotNull(history);
                Assert.Equal(1, history.VirtualKeyId);
                Assert.Equal(3.0m, history.Amount);
                // Don't check the exact date since it's set by the service
            }
        }

        [Fact]
        public async Task ProcessMonthlyBudgetResets_ShouldReset_WhenMonthHasPassed()
        {
            // Arrange
            var options = GetDbOptions();
            var lastMonth = DateTime.UtcNow.AddMonths(-1);

            using (var context = CreateTestContext(options))
            {
                context.VirtualKeys.Add(new VirtualKey
                {
                    Id = 1,
                    KeyName = "Monthly Reset Key",
                    KeyHash = "vk_monthly_reset",
                    IsEnabled = true,
                    MaxBudget = 100.0m,
                    CurrentSpend = 75.0m,
                    BudgetDuration = "monthly",
                    BudgetStartDate = lastMonth
                });
                await context.SaveChangesAsync();
            }

            // Act
            using (var context = CreateTestContext(options))
            {
                // Create mock repositories with properly set up return values
                var mockVirtualKeyRepo = new Mock<IVirtualKeyRepository>();
                mockVirtualKeyRepo.Setup(repo => repo.GetAllAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(await context.VirtualKeys.ToListAsync());

                var mockSpendHistoryRepo = new Mock<IVirtualKeySpendHistoryRepository>();
                mockSpendHistoryRepo.Setup(repo => repo.CreateAsync(It.IsAny<VirtualKeySpendHistory>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(1)
                    .Callback<VirtualKeySpendHistory, CancellationToken>((history, _) =>
                    {
                        // Add the history to the database
                        context.VirtualKeySpendHistory.Add(history);
                        context.SaveChanges();
                    });

                mockVirtualKeyRepo.Setup(repo => repo.UpdateAsync(It.IsAny<VirtualKey>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true)
                    .Callback<VirtualKey, CancellationToken>((key, _) =>
                    {
                        // Update the key in the database
                        var dbKey = context.VirtualKeys.Find(key.Id);
                        if (dbKey != null)
                        {
                            dbKey.CurrentSpend = key.CurrentSpend;
                            dbKey.BudgetStartDate = key.BudgetStartDate;
                            dbKey.UpdatedAt = key.UpdatedAt;
                            context.SaveChanges();
                        }
                    });

                var mockLogger = new Mock<ILogger<VirtualKeyMaintenanceService>>();
                var maintenanceService = new VirtualKeyMaintenanceService(
                    mockVirtualKeyRepo.Object,
                    mockSpendHistoryRepo.Object,
                    mockLogger.Object);

                await maintenanceService.ProcessBudgetResetsAsync();
            }

            // Assert
            using (var context = CreateTestContext(options))
            {
                var key = await context.VirtualKeys.FindAsync(1);

                Assert.NotNull(key);
                Assert.Equal(0m, key.CurrentSpend); // Should be reset to 0
                Assert.NotNull(key.BudgetStartDate);
                Assert.Equal(DateTime.UtcNow.Month, key.BudgetStartDate?.Month); // Should be updated to current month
                Assert.Equal(DateTime.UtcNow.Year, key.BudgetStartDate?.Year);

                // Verify we created a spend history entry
                var history = await context.VirtualKeySpendHistory.FirstOrDefaultAsync(h => h.VirtualKeyId == 1);
                Assert.NotNull(history);
                Assert.Equal(1, history.VirtualKeyId);
                Assert.Equal(75.0m, history.Amount);
                // Don't check the exact date since it's set by the service
            }
        }

        [Fact]
        public async Task DisableExpiredKeys_ShouldDeactivate_WhenExpirationDatePassed()
        {
            // Arrange
            var options = GetDbOptions();
            var yesterday = DateTime.UtcNow.AddDays(-1);

            using (var context = CreateTestContext(options))
            {
                context.VirtualKeys.AddRange(
                    new VirtualKey
                    {
                        Id = 1,
                        KeyName = "Expired Key",
                        KeyHash = "vk_expired",
                        IsEnabled = true,
                        ExpiresAt = yesterday
                    },
                    new VirtualKey
                    {
                        Id = 2,
                        KeyName = "Valid Key",
                        KeyHash = "vk_valid",
                        IsEnabled = true,
                        ExpiresAt = DateTime.UtcNow.AddDays(30)
                    }
                );
                await context.SaveChangesAsync();
            }

            // Act
            using (var context = CreateTestContext(options))
            {
                // Create mock repositories with properly set up return values
                var mockVirtualKeyRepo = new Mock<IVirtualKeyRepository>();
                mockVirtualKeyRepo.Setup(repo => repo.GetAllAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(await context.VirtualKeys.ToListAsync());

                mockVirtualKeyRepo.Setup(repo => repo.UpdateAsync(It.IsAny<VirtualKey>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true)
                    .Callback<VirtualKey, CancellationToken>((key, _) =>
                    {
                        // Update the key in the database
                        var dbKey = context.VirtualKeys.Find(key.Id);
                        if (dbKey != null)
                        {
                            dbKey.IsEnabled = key.IsEnabled;
                            dbKey.UpdatedAt = key.UpdatedAt;
                            context.SaveChanges();
                        }
                    });

                var mockSpendHistoryRepo = new Mock<IVirtualKeySpendHistoryRepository>();
                var mockLogger = new Mock<ILogger<VirtualKeyMaintenanceService>>();

                var maintenanceService = new VirtualKeyMaintenanceService(
                    mockVirtualKeyRepo.Object,
                    mockSpendHistoryRepo.Object,
                    mockLogger.Object);

                await maintenanceService.DisableExpiredKeysAsync();
            }

            // Assert
            using (var context = CreateTestContext(options))
            {
                var expiredKey = await context.VirtualKeys.FindAsync(1);
                var validKey = await context.VirtualKeys.FindAsync(2);

                Assert.NotNull(expiredKey);
                Assert.NotNull(validKey);

                Assert.False(expiredKey.IsEnabled); // Should be deactivated
                Assert.True(validKey.IsEnabled);    // Should remain active
            }
        }
    }
}
