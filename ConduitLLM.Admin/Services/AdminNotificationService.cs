using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ConduitLLM.Admin.Extensions;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;

using Microsoft.Extensions.Logging;

using ConduitLLM.Configuration.Interfaces;
namespace ConduitLLM.Admin.Services
{
    /// <summary>
    /// Service for managing notifications through the Admin API
    /// </summary>
    public class AdminNotificationService : IAdminNotificationService
    {
        private readonly INotificationRepository _notificationRepository;
        private readonly IVirtualKeyRepository _virtualKeyRepository;
        private readonly ILogger<AdminNotificationService> _logger;

        /// <summary>
        /// Initializes a new instance of the AdminNotificationService
        /// </summary>
        /// <param name="notificationRepository">The notification repository</param>
        /// <param name="virtualKeyRepository">The virtual key repository</param>
        /// <param name="logger">The logger</param>
        public AdminNotificationService(
            INotificationRepository notificationRepository,
            IVirtualKeyRepository virtualKeyRepository,
            ILogger<AdminNotificationService> logger)
        {
            _notificationRepository = notificationRepository ?? throw new ArgumentNullException(nameof(notificationRepository));
            _virtualKeyRepository = virtualKeyRepository ?? throw new ArgumentNullException(nameof(virtualKeyRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<IEnumerable<NotificationDto>> GetAllNotificationsAsync()
        {
            try
            {
                _logger.LogInformation("Getting all notifications");

                var notifications = await _notificationRepository.GetAllAsync();
                var virtualKeyIds = notifications
                    .Where(n => n.VirtualKeyId.HasValue)
                    .Select(n => n.VirtualKeyId!.Value)
                    .Distinct()
                    .ToList();

                // Get virtual key names for the notifications
                var virtualKeys = new Dictionary<int, string>();
                if (virtualKeyIds.Any())
                {
                    var keys = await _virtualKeyRepository.GetAllAsync();
                    virtualKeys = keys
                        .Where(k => virtualKeyIds.Contains(k.Id))
                        .ToDictionary(k => k.Id, k => k.KeyName);
                }

                // Map to DTOs with virtual key names
                var result = notifications
                    .OrderByDescending(n => n.CreatedAt)
                    .Select(n =>
                    {
                        string? keyName = null;
                        if (n.VirtualKeyId.HasValue && virtualKeys.TryGetValue(n.VirtualKeyId.Value, out var name))
                        {
                            keyName = name;
                        }

                        return n.ToDto(keyName);
                    })
                    .ToList();

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all notifications");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<IEnumerable<NotificationDto>> GetUnreadNotificationsAsync()
        {
            try
            {
                _logger.LogInformation("Getting unread notifications");

                var notifications = await _notificationRepository.GetUnreadAsync();
                var virtualKeyIds = notifications
                    .Where(n => n.VirtualKeyId.HasValue)
                    .Select(n => n.VirtualKeyId!.Value)
                    .Distinct()
                    .ToList();

                // Get virtual key names for the notifications
                var virtualKeys = new Dictionary<int, string>();
                if (virtualKeyIds.Any())
                {
                    var keys = await _virtualKeyRepository.GetAllAsync();
                    virtualKeys = keys
                        .Where(k => virtualKeyIds.Contains(k.Id))
                        .ToDictionary(k => k.Id, k => k.KeyName);
                }

                // Map to DTOs with virtual key names
                var result = notifications
                    .OrderByDescending(n => n.CreatedAt)
                    .Select(n =>
                    {
                        string? keyName = null;
                        if (n.VirtualKeyId.HasValue && virtualKeys.TryGetValue(n.VirtualKeyId.Value, out var name))
                        {
                            keyName = name;
                        }

                        return n.ToDto(keyName);
                    })
                    .ToList();

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting unread notifications");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<NotificationDto?> GetNotificationByIdAsync(int id)
        {
            try
            {
                _logger.LogInformation("Getting notification with ID: {Id}", id);

                var notification = await _notificationRepository.GetByIdAsync(id);
                if (notification == null)
                {
                    _logger.LogWarning("Notification with ID {Id} not found", id);
                    return null;
                }

                // Get virtual key name if applicable
                string? keyName = null;
                if (notification.VirtualKeyId.HasValue)
                {
                    var key = await _virtualKeyRepository.GetByIdAsync(notification.VirtualKeyId.Value);
                    keyName = key?.KeyName;
                }

                return notification.ToDto(keyName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting notification with ID {Id}", id);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<NotificationDto> CreateNotificationAsync(CreateNotificationDto notification)
        {
            try
            {
                _logger.LogInformation("Creating new notification");

                // Validate virtual key ID if provided
                if (notification.VirtualKeyId.HasValue)
                {
                    var key = await _virtualKeyRepository.GetByIdAsync(notification.VirtualKeyId.Value);
                    if (key == null)
                    {
                        throw new ArgumentException($"Virtual key with ID {notification.VirtualKeyId.Value} not found");
                    }
                }

                // Convert to entity
                var entity = notification.ToEntity();

                // Save to database
                var id = await _notificationRepository.CreateAsync(entity);

                // Get the created notification
                var createdNotification = await _notificationRepository.GetByIdAsync(id);
                if (createdNotification == null)
                {
                    throw new InvalidOperationException($"Failed to retrieve newly created notification with ID {id}");
                }

                // Get virtual key name if applicable
                string? keyName = null;
                if (createdNotification.VirtualKeyId.HasValue)
                {
                    var key = await _virtualKeyRepository.GetByIdAsync(createdNotification.VirtualKeyId.Value);
                    keyName = key?.KeyName;
                }

                return createdNotification.ToDto(keyName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating notification");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<bool> UpdateNotificationAsync(UpdateNotificationDto notification)
        {
            try
            {
                _logger.LogInformation("Updating notification with ID: {Id}", notification.Id);

                // Get the existing notification
                var existingNotification = await _notificationRepository.GetByIdAsync(notification.Id);
                if (existingNotification == null)
                {
                    _logger.LogWarning("Notification with ID {Id} not found", notification.Id);
                    return false;
                }

                // Update properties
                existingNotification.IsRead = notification.IsRead;
                if (!string.IsNullOrEmpty(notification.Message))
                {
                    existingNotification.Message = notification.Message;
                }

                // Save changes
                return await _notificationRepository.UpdateAsync(existingNotification);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating notification with ID {Id}", notification.Id);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<bool> MarkNotificationAsReadAsync(int id)
        {
            try
            {
                _logger.LogInformation("Marking notification with ID {Id} as read", id);

                return await _notificationRepository.MarkAsReadAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking notification with ID {Id} as read", id);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<int> MarkAllNotificationsAsReadAsync()
        {
            try
            {
                _logger.LogInformation("Marking all notifications as read");

                // Get all unread notifications
                var unreadNotifications = await _notificationRepository.GetUnreadAsync();
                if (!unreadNotifications.Any())
                {
                    return 0;
                }

                // Mark each as read
                int count = 0;
                foreach (var notification in unreadNotifications)
                {
                    var success = await _notificationRepository.MarkAsReadAsync(notification.Id);
                    if (success)
                    {
                        count++;
                    }
                }

                return count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking all notifications as read");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<bool> DeleteNotificationAsync(int id)
        {
            try
            {
                _logger.LogInformation("Deleting notification with ID: {Id}", id);

                return await _notificationRepository.DeleteAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting notification with ID {Id}", id);
                throw;
            }
        }
    }
}
