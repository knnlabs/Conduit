using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Services;

using Microsoft.EntityFrameworkCore;

using Xunit;

namespace ConduitLLM.Tests.Services
{
    public class NotificationServiceTests
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
        public async Task CheckBudgetLimits_ShouldCreateNotification_WhenKeyApproachesLimit()
        {
            // Arrange
            var options = GetDbOptions();
            
            using (var context = CreateTestContext(options))
            {
                context.VirtualKeys.Add(new VirtualKey 
                { 
                    Id = 1, 
                    KeyName = "Budget Test Key", 
                    KeyHash = "vk_budget_test", 
                    IsEnabled = true,
                    MaxBudget = 5.0m,
                    CurrentSpend = 4.0m, // 80% of budget used
                    BudgetDuration = "monthly",
                    BudgetStartDate = DateTime.UtcNow.AddDays(-15)
                });
                await context.SaveChangesAsync();
            }
            
            // Act
            using (var context = CreateTestContext(options))
            {
                var service = new ConduitLLM.Configuration.Services.NotificationService(context);
                await service.CheckBudgetLimitsAsync();
            }
            
            // Assert
            using (var context = CreateTestContext(options))
            {
                var notification = await context.Notifications.FirstOrDefaultAsync();
                
                Assert.NotNull(notification);
                Assert.Equal(1, notification.VirtualKeyId);
                Assert.Equal(ConduitLLM.Configuration.Entities.NotificationType.BudgetWarning, notification.Type);
                Assert.False(notification.IsRead);
                
                // The actual message format may vary, so just check for key parts
                Assert.Contains("Virtual key", notification.Message);
                Assert.Contains("Budget Test Key", notification.Message);
            }
        }
        
        [Fact]
        public async Task CheckKeyExpiration_ShouldCreateNotification_WhenKeyApproachesExpiration()
        {
            // Arrange
            var options = GetDbOptions();
            var expirationDate = DateTime.UtcNow.AddDays(5); // Expires in 5 days
            
            using (var context = CreateTestContext(options))
            {
                context.VirtualKeys.Add(new VirtualKey 
                { 
                    Id = 1, 
                    KeyName = "Expiring Key", 
                    KeyHash = "vk_expiring", 
                    IsEnabled = true,
                    ExpiresAt = expirationDate
                });
                await context.SaveChangesAsync();
            }
            
            // Act
            using (var context = CreateTestContext(options))
            {
                var service = new ConduitLLM.Configuration.Services.NotificationService(context);
                await service.CheckKeyExpirationAsync();
            }
            
            // Assert
            using (var context = CreateTestContext(options))
            {
                var notification = await context.Notifications.FirstOrDefaultAsync();
                
                Assert.NotNull(notification);
                Assert.Equal(1, notification.VirtualKeyId);
                Assert.Equal(ConduitLLM.Configuration.Entities.NotificationType.ExpirationWarning, notification.Type);
                Assert.False(notification.IsRead);
                
                // The actual message format may vary, so just check for key parts
                Assert.Contains("Virtual key", notification.Message);
                Assert.Contains("Expiring Key", notification.Message);
                Assert.Contains("expire", notification.Message);
            }
        }
        
        [Fact]
        public async Task MarkNotificationAsRead_ShouldUpdateReadStatus()
        {
            // Arrange
            var options = GetDbOptions();
            
            using (var context = CreateTestContext(options))
            {
                context.VirtualKeys.Add(new VirtualKey 
                { 
                    Id = 1, 
                    KeyName = "Test Key", 
                    KeyHash = "vk_test", 
                    IsEnabled = true
                });
                
                context.Notifications.Add(new Notification
                {
                    Id = 1,
                    VirtualKeyId = 1,
                    Type = ConduitLLM.Configuration.Entities.NotificationType.BudgetWarning,
                    Message = "Test notification",
                    CreatedAt = DateTime.UtcNow,
                    IsRead = false
                });
                
                await context.SaveChangesAsync();
            }
            
            // Act
            using (var context = CreateTestContext(options))
            {
                var service = new ConduitLLM.Configuration.Services.NotificationService(context);
                await service.MarkAsReadAsync(1);
            }
            
            // Assert
            using (var context = CreateTestContext(options))
            {
                var notification = await context.Notifications.FindAsync(1);
                
                Assert.NotNull(notification);
                Assert.True(notification.IsRead);
            }
        }
    }
}
