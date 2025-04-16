using ConduitLLM.Configuration.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ConduitLLM.Configuration.Services
{
    /// <summary>
    /// Service for managing notifications related to virtual keys
    /// </summary>
    public class NotificationService
    {
        private readonly VirtualKeyDbContext _context;
        
        // Budget warning thresholds
        private const decimal WarningThreshold = 0.75m; // 75%
        private const decimal CriticalThreshold = 0.90m; // 90%
        
        // Expiration warning thresholds in days
        private const int ExpirationWarningDays = 7;
        private const int ExpirationCriticalDays = 1;
        
        /// <summary>
        /// Initializes a new instance of the NotificationService
        /// </summary>
        /// <param name="context">Database context</param>
        public NotificationService(VirtualKeyDbContext context)
        {
            _context = context;
        }
        
        /// <summary>
        /// Checks all virtual keys for budget limits and creates notifications as needed
        /// </summary>
        public async Task CheckBudgetLimitsAsync()
        {
            var keys = await _context.VirtualKeys
                .Where(k => k.IsEnabled && k.MaxBudget.HasValue && k.MaxBudget > 0)
                .ToListAsync();
                
            foreach (var key in keys)
            {
                // Safely access MaxBudget since we've filtered for non-null values above
                decimal usagePercentage = key.CurrentSpend / key.MaxBudget!.Value;
                
                // Check if we should notify based on threshold
                if (usagePercentage >= CriticalThreshold)
                {
                    await CreateBudgetNotificationAsync(key, usagePercentage, NotificationSeverity.Error);
                }
                else if (usagePercentage >= WarningThreshold)
                {
                    await CreateBudgetNotificationAsync(key, usagePercentage, NotificationSeverity.Warning);
                }
            }
        }
        
        /// <summary>
        /// Checks all virtual keys for approaching expiration and creates notifications as needed
        /// </summary>
        public async Task CheckKeyExpirationAsync()
        {
            var now = DateTime.UtcNow;
            var warningDate = now.AddDays(ExpirationWarningDays);
            var criticalDate = now.AddDays(ExpirationCriticalDays);
            
            var keys = await _context.VirtualKeys
                .Where(k => k.IsEnabled && k.ExpiresAt.HasValue)
                .Where(k => k.ExpiresAt.HasValue && k.ExpiresAt <= warningDate)
                .ToListAsync();
                
            foreach (var key in keys)
            {
                // ExpiresAt is guaranteed to have a value based on the query above
                DateTime expiryDate = key.ExpiresAt!.Value;
                
                if (expiryDate <= now)
                {
                    // Already expired
                    await CreateExpirationNotificationAsync(key, 0, NotificationSeverity.Error);
                }
                else if (expiryDate <= criticalDate)
                {
                    // Expires within a day
                    var daysLeft = (expiryDate - now).TotalDays;
                    await CreateExpirationNotificationAsync(key, daysLeft, NotificationSeverity.Error);
                }
                else
                {
                    // Expires within warning window (more than a day but less than warning threshold)
                    var daysLeft = (expiryDate - now).TotalDays;
                    await CreateExpirationNotificationAsync(key, daysLeft, NotificationSeverity.Warning);
                }
            }
        }
        
        /// <summary>
        /// Creates a budget warning notification for a virtual key
        /// </summary>
        private async Task CreateBudgetNotificationAsync(VirtualKey key, decimal percentage, NotificationSeverity severity)
        {
            // Check if we've already created a notification for this
            var existingNotification = await _context.Notifications
                .Where(n => n.VirtualKeyId == key.Id)
                .Where(n => n.Type == NotificationType.BudgetWarning)
                .Where(n => !n.IsRead)
                .FirstOrDefaultAsync();
                
            string message = $"Virtual key '{key.KeyName}' has reached {percentage:P0} of its budget ({key.CurrentSpend:C2} / {key.MaxBudget:C2})";
                
            if (existingNotification != null)
            {
                // Update existing notification
                existingNotification.Message = message;
                existingNotification.Severity = severity;
                existingNotification.CreatedAt = DateTime.UtcNow;
                
                _context.Notifications.Update(existingNotification);
            }
            else
            {
                // Create new notification
                var notification = new Notification
                {
                    VirtualKeyId = key.Id,
                    Type = NotificationType.BudgetWarning,
                    Message = message,
                    Severity = severity,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                };
                
                _context.Notifications.Add(notification);
            }
            
            await _context.SaveChangesAsync();
        }
        
        /// <summary>
        /// Creates an expiration warning notification for a virtual key
        /// </summary>
        private async Task CreateExpirationNotificationAsync(VirtualKey key, double daysLeft, NotificationSeverity severity)
        {
            // Check if we've already created a notification for this
            var existingNotification = await _context.Notifications
                .Where(n => n.VirtualKeyId == key.Id)
                .Where(n => n.Type == NotificationType.ExpirationWarning)
                .Where(n => !n.IsRead)
                .FirstOrDefaultAsync();
                
            string message;
            if (daysLeft <= 0)
            {
                message = $"Virtual key '{key.KeyName}' has expired";
            }
            else if (daysLeft < 1)
            {
                message = $"Virtual key '{key.KeyName}' will expire in less than 1 day";
            }
            else
            {
                message = $"Virtual key '{key.KeyName}' will expire in {(int)daysLeft} day{((int)daysLeft != 1 ? "s" : "")}";
            }
                
            if (existingNotification != null)
            {
                // Update existing notification
                existingNotification.Message = message;
                existingNotification.Severity = severity;
                existingNotification.CreatedAt = DateTime.UtcNow;
                
                _context.Notifications.Update(existingNotification);
            }
            else
            {
                // Create new notification
                var notification = new Notification
                {
                    VirtualKeyId = key.Id,
                    Type = NotificationType.ExpirationWarning,
                    Message = message,
                    Severity = severity,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                };
                
                _context.Notifications.Add(notification);
            }
            
            await _context.SaveChangesAsync();
        }
        
        /// <summary>
        /// Marks a notification as read
        /// </summary>
        /// <param name="id">The notification ID</param>
        public async Task MarkAsReadAsync(int id)
        {
            var notification = await _context.Notifications.FindAsync(id);
            if (notification != null)
            {
                notification.IsRead = true;
                _context.Notifications.Update(notification);
                await _context.SaveChangesAsync();
            }
        }
        
        /// <summary>
        /// Marks all notifications for a virtual key as read
        /// </summary>
        /// <param name="virtualKeyId">The virtual key ID</param>
        public async Task MarkAllAsReadForKeyAsync(int virtualKeyId)
        {
            var notifications = await _context.Notifications
                .Where(n => n.VirtualKeyId == virtualKeyId && !n.IsRead)
                .ToListAsync();
                
            foreach (var notification in notifications)
            {
                notification.IsRead = true;
                _context.Notifications.Update(notification);
            }
            
            await _context.SaveChangesAsync();
        }
        
        /// <summary>
        /// Gets all unread notifications for a virtual key
        /// </summary>
        /// <param name="virtualKeyId">The virtual key ID</param>
        public async Task<List<Notification>> GetUnreadNotificationsAsync(int virtualKeyId)
        {
            return await _context.Notifications
                .Where(n => n.VirtualKeyId == virtualKeyId && !n.IsRead)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();
        }
    }
}
