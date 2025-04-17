using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Services;
using ConduitLLM.WebUI.Services;

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
        
        [Fact]
        public async Task CheckBudgetLimits_ShouldCreateNotification_WhenKeyApproachesLimit()
        {
            // Arrange
            var options = GetDbOptions();
            
            // Setup test data
            using (var context = new ConfigurationDbContext(options))
            {
                context.VirtualKeys.Add(new VirtualKey 
                { 
                    Id = 1, 
                    KeyName = "Test Key", 
                    KeyHash = "vk_test123", 
                    IsEnabled = true,
                    MaxBudget = 10.0m,
                    CurrentSpend = 8.5m, // 85% of limit
                    BudgetDuration = "monthly",
                    BudgetStartDate = DateTime.UtcNow.AddDays(-15)
                });
                await context.SaveChangesAsync();
            }
            
            // Act
            using (var context = new ConfigurationDbContext(options))
            {
                var notificationService = new Configuration.Services.NotificationService(context);
                await notificationService.CheckBudgetLimitsAsync();
            }
            
            // Assert
            using (var context = new ConfigurationDbContext(options))
            {
                var notification = await context.Notifications.FirstOrDefaultAsync();
                
                Assert.NotNull(notification);
                Assert.Equal(NotificationType.BudgetWarning, notification.Type);
                Assert.Equal(1, notification.VirtualKeyId);
                Assert.Contains("85 %", notification.Message); // Matches the P0 formatting
                Assert.False(notification.IsRead);
                Assert.Equal(NotificationSeverity.Warning, notification.Severity);
            }
        }
        
        [Fact]
        public async Task CheckKeyExpiration_ShouldCreateNotification_WhenKeyApproachesExpiration()
        {
            // Arrange
            var options = GetDbOptions();
            // Setting expiration to 5 days from now to get Warning severity instead of Error (which is ≤ 1 day)
            var fiveDaysLater = DateTime.UtcNow.AddDays(5);
            
            // Setup test data
            using (var context = new ConfigurationDbContext(options))
            {
                context.VirtualKeys.Add(new VirtualKey 
                { 
                    Id = 1, 
                    KeyName = "Expiring Key", 
                    KeyHash = "vk_expiring", 
                    IsEnabled = true,
                    MaxBudget = 10.0m,
                    CurrentSpend = 5.0m,
                    ExpiresAt = fiveDaysLater
                });
                await context.SaveChangesAsync();
            }
            
            // Act
            using (var context = new ConfigurationDbContext(options))
            {
                var notificationService = new Configuration.Services.NotificationService(context);
                await notificationService.CheckKeyExpirationAsync();
            }
            
            // Assert
            using (var context = new ConfigurationDbContext(options))
            {
                var notification = await context.Notifications.FirstOrDefaultAsync();
                
                Assert.NotNull(notification);
                Assert.Equal(NotificationType.ExpirationWarning, notification.Type);
                Assert.Equal(1, notification.VirtualKeyId);
                Assert.Contains("expire", notification.Message.ToLower());
                
                // Use regex to check for days pattern since exact day count might vary due to time calculations
                var dayPattern = new Regex(@"expire in \d+ days?");
                Assert.Matches(dayPattern, notification.Message.ToLower());
                
                Assert.False(notification.IsRead);
                Assert.Equal(NotificationSeverity.Warning, notification.Severity);
            }
        }
        
        [Fact]
        public async Task MarkNotificationAsRead_ShouldUpdateReadStatus()
        {
            // Arrange
            var options = GetDbOptions();
            
            // Setup test data
            using (var context = new ConfigurationDbContext(options))
            {
                context.Notifications.Add(new Notification
                {
                    Id = 1,
                    VirtualKeyId = 1,
                    Type = NotificationType.BudgetWarning,
                    Message = "Test notification",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                });
                await context.SaveChangesAsync();
            }
            
            // Act
            using (var context = new ConfigurationDbContext(options))
            {
                var notificationService = new Configuration.Services.NotificationService(context);
                await notificationService.MarkAsReadAsync(1);
            }
            
            // Assert
            using (var context = new ConfigurationDbContext(options))
            {
                var notification = await context.Notifications.FindAsync(1);
                
                Assert.NotNull(notification);
                Assert.True(notification.IsRead);
            }
        }
    }
}
