using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Configuration.Services
{
    /// <summary>
    /// Service for managing notifications related to virtual keys
    /// </summary>
    public class NotificationService
    {
        private readonly INotificationRepository _notificationRepository;
        private readonly IVirtualKeyRepository _virtualKeyRepository;
        private readonly ILogger<NotificationService> _logger;
        
        // Budget warning thresholds
        private const decimal WarningThreshold = 0.75m; // 75%
        private const decimal CriticalThreshold = 0.90m; // 90%
        
        // Expiration warning thresholds in days
        private const int ExpirationWarningDays = 7;
        private const int ExpirationCriticalDays = 1;
        
        /// <summary>
        /// Initializes a new instance of the NotificationService
        /// </summary>
        /// <param name="notificationRepository">The notification repository</param>
        /// <param name="virtualKeyRepository">The virtual key repository</param>
        /// <param name="logger">The logger</param>
        public NotificationService(
            INotificationRepository notificationRepository,
            IVirtualKeyRepository virtualKeyRepository,
            ILogger<NotificationService> logger)
        {
            _notificationRepository = notificationRepository ?? throw new ArgumentNullException(nameof(notificationRepository));
            _virtualKeyRepository = virtualKeyRepository ?? throw new ArgumentNullException(nameof(virtualKeyRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        /// <summary>
        /// Checks all virtual keys for budget limits and creates notifications as needed
        /// </summary>
        public async Task CheckBudgetLimitsAsync()
        {
            try
            {
                var keys = (await _virtualKeyRepository.GetAllAsync())
                    .Where(k => k.IsEnabled && k.MaxBudget.HasValue && k.MaxBudget > 0)
                    .ToList();
                    
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking budget limits for notifications");
                throw;
            }
        }
        
        /// <summary>
        /// Checks all virtual keys for approaching expiration and creates notifications as needed
        /// </summary>
        public async Task CheckKeyExpirationAsync()
        {
            try
            {
                var now = DateTime.UtcNow;
                var warningDate = now.AddDays(ExpirationWarningDays);
                
                var keys = (await _virtualKeyRepository.GetAllAsync())
                    .Where(k => k.IsEnabled && k.ExpiresAt.HasValue)
                    .Where(k => k.ExpiresAt.HasValue && k.ExpiresAt <= warningDate)
                    .ToList();
                    
                foreach (var key in keys)
                {
                    // ExpiresAt is guaranteed to have a value based on the query above
                    DateTime expiryDate = key.ExpiresAt!.Value;
                    
                    if (expiryDate <= now)
                    {
                        // Already expired
                        await CreateExpirationNotificationAsync(key, 0, NotificationSeverity.Error);
                    }
                    else if (expiryDate <= now.AddDays(ExpirationCriticalDays))
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking key expiration for notifications");
                throw;
            }
        }
        
        /// <summary>
        /// Creates a budget warning notification for a virtual key
        /// </summary>
        private async Task CreateBudgetNotificationAsync(VirtualKey key, decimal percentage, NotificationSeverity severity)
        {
            try
            {
                // Get existing notifications for this key
                var notifications = await _notificationRepository.GetAllAsync();
                var existingNotification = notifications
                    .Where(n => n.VirtualKeyId == key.Id)
                    .Where(n => n.Type == NotificationType.BudgetWarning)
                    .Where(n => !n.IsRead)
                    .FirstOrDefault();
                    
                string message = $"Virtual key '{key.KeyName}' has reached {percentage:P0} of its budget ({key.CurrentSpend:C2} / {key.MaxBudget:C2})";
                    
                if (existingNotification != null)
                {
                    // Update existing notification
                    existingNotification.Message = message;
                    existingNotification.Severity = severity;
                    existingNotification.CreatedAt = DateTime.UtcNow;
                    
                    await _notificationRepository.UpdateAsync(existingNotification);
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
                    
                    await _notificationRepository.CreateAsync(notification);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating budget notification for key {KeyId}", key.Id);
                throw;
            }
        }
        
        /// <summary>
        /// Creates an expiration warning notification for a virtual key
        /// </summary>
        private async Task CreateExpirationNotificationAsync(VirtualKey key, double daysLeft, NotificationSeverity severity)
        {
            try
            {
                // Get existing notifications for this key
                var notifications = await _notificationRepository.GetAllAsync();
                var existingNotification = notifications
                    .Where(n => n.VirtualKeyId == key.Id)
                    .Where(n => n.Type == NotificationType.ExpirationWarning)
                    .Where(n => !n.IsRead)
                    .FirstOrDefault();
                    
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
                    
                    await _notificationRepository.UpdateAsync(existingNotification);
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
                    
                    await _notificationRepository.CreateAsync(notification);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating expiration notification for key {KeyId}", key.Id);
                throw;
            }
        }
        
        /// <summary>
        /// Marks a notification as read
        /// </summary>
        /// <param name="id">The notification ID</param>
        public async Task MarkAsReadAsync(int id)
        {
            try
            {
                await _notificationRepository.MarkAsReadAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking notification {NotificationId} as read", id);
                throw;
            }
        }
        
        /// <summary>
        /// Marks all notifications for a virtual key as read
        /// </summary>
        /// <param name="virtualKeyId">The virtual key ID</param>
        public async Task MarkAllAsReadForKeyAsync(int virtualKeyId)
        {
            try
            {
                var notifications = (await _notificationRepository.GetAllAsync())
                    .Where(n => n.VirtualKeyId == virtualKeyId && !n.IsRead)
                    .ToList();
                    
                foreach (var notification in notifications)
                {
                    notification.IsRead = true;
                    await _notificationRepository.UpdateAsync(notification);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking all notifications as read for key {KeyId}", virtualKeyId);
                throw;
            }
        }
        
        /// <summary>
        /// Gets all unread notifications for a virtual key
        /// </summary>
        /// <param name="virtualKeyId">The virtual key ID</param>
        public async Task<List<Notification>> GetUnreadNotificationsAsync(int virtualKeyId)
        {
            try
            {
                var notifications = await _notificationRepository.GetAllAsync();
                return notifications
                    .Where(n => n.VirtualKeyId == virtualKeyId && !n.IsRead)
                    .OrderByDescending(n => n.CreatedAt)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting unread notifications for key {KeyId}", virtualKeyId);
                throw;
            }
        }
    }
}