using System;
using System.Threading.Tasks;

using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Services;

using Microsoft.EntityFrameworkCore;

using Xunit;

namespace ConduitLLM.Tests.Services
{
    public class VirtualKeyMaintenanceServiceTests
    {
        private DbContextOptions<VirtualKeyDbContext> GetDbOptions()
        {
            return new DbContextOptionsBuilder<VirtualKeyDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
        }
        
        [Fact]
        public async Task ProcessDailyBudgetResets_ShouldReset_WhenDayHasPassed()
        {
            // Arrange
            var options = GetDbOptions();
            var yesterday = DateTime.UtcNow.AddDays(-1).Date;
            
            using (var context = new VirtualKeyDbContext(options))
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
            using (var context = new VirtualKeyDbContext(options))
            {
                var maintenanceService = new VirtualKeyMaintenanceService(context);
                await maintenanceService.ProcessBudgetResetsAsync();
            }
            
            // Assert
            using (var context = new VirtualKeyDbContext(options))
            {
                var key = await context.VirtualKeys.FindAsync(1);
                
                Assert.NotNull(key);
                Assert.Equal(0m, key.CurrentSpend); // Should be reset to 0
                Assert.Equal(DateTime.UtcNow.Date, key.BudgetStartDate?.Date); // Should be updated to today
                
                // Verify we created a spend history entry
                var history = await context.VirtualKeySpendHistory.FirstOrDefaultAsync();
                Assert.NotNull(history);
                Assert.Equal(1, history.VirtualKeyId);
                Assert.Equal(3.0m, history.Amount);
                Assert.Equal(yesterday, history.Date.Date);
            }
        }
        
        [Fact]
        public async Task ProcessMonthlyBudgetResets_ShouldReset_WhenMonthHasPassed()
        {
            // Arrange
            var options = GetDbOptions();
            var lastMonth = DateTime.UtcNow.AddMonths(-1);
            
            using (var context = new VirtualKeyDbContext(options))
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
            using (var context = new VirtualKeyDbContext(options))
            {
                var maintenanceService = new VirtualKeyMaintenanceService(context);
                await maintenanceService.ProcessBudgetResetsAsync();
            }
            
            // Assert
            using (var context = new VirtualKeyDbContext(options))
            {
                var key = await context.VirtualKeys.FindAsync(1);
                
                Assert.NotNull(key);
                Assert.Equal(0m, key.CurrentSpend); // Should be reset to 0
                Assert.Equal(DateTime.UtcNow.Month, key.BudgetStartDate?.Month); // Should be updated to current month
                Assert.Equal(DateTime.UtcNow.Year, key.BudgetStartDate?.Year);
                
                // Verify we created a spend history entry
                var history = await context.VirtualKeySpendHistory.FirstOrDefaultAsync();
                Assert.NotNull(history);
                Assert.Equal(1, history.VirtualKeyId);
                Assert.Equal(75.0m, history.Amount);
                Assert.Equal(lastMonth.Month, history.Date.Month);
                Assert.Equal(lastMonth.Year, history.Date.Year);
            }
        }
        
        [Fact]
        public async Task DisableExpiredKeys_ShouldDeactivate_WhenExpirationDatePassed()
        {
            // Arrange
            var options = GetDbOptions();
            var yesterday = DateTime.UtcNow.AddDays(-1);
            
            using (var context = new VirtualKeyDbContext(options))
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
            using (var context = new VirtualKeyDbContext(options))
            {
                var maintenanceService = new VirtualKeyMaintenanceService(context);
                await maintenanceService.DisableExpiredKeysAsync();
            }
            
            // Assert
            using (var context = new VirtualKeyDbContext(options))
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
