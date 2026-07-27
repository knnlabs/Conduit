using ConduitLLM.Admin.Extensions;
using ConduitLLM.Admin.Endpoints;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Extensions;
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
            _logger.LogDebug("Getting all notifications");

            var notifications = await RepositoryPaginationExtensions.GetAllViaPaginationAsync(
                _notificationRepository.GetPaginatedAsync);
            var virtualKeyIds = notifications
                .Where(n => n.VirtualKeyId.HasValue)
                .Select(n => n.VirtualKeyId!.Value)
                .Distinct()
                .ToList();

            // Get virtual key names for the notifications using efficient lookup
            var virtualKeys = virtualKeyIds.Count != 0
                ? await _virtualKeyRepository.GetKeyNamesByIdsAsync(virtualKeyIds)
                : new Dictionary<int, string>();

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

        /// <inheritdoc />
        public async Task<IEnumerable<NotificationDto>> GetUnreadNotificationsAsync()
        {
            _logger.LogDebug("Getting unread notifications");

            var notifications = await RepositoryPaginationExtensions.GetAllViaPaginationAsync(
                _notificationRepository.GetUnreadPaginatedAsync);
            var virtualKeyIds = notifications
                .Where(n => n.VirtualKeyId.HasValue)
                .Select(n => n.VirtualKeyId!.Value)
                .Distinct()
                .ToList();

            // Get virtual key names for the notifications using efficient lookup
            var virtualKeys = virtualKeyIds.Count != 0
                ? await _virtualKeyRepository.GetKeyNamesByIdsAsync(virtualKeyIds)
                : new Dictionary<int, string>();

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

        /// <inheritdoc />
        public async Task<NotificationDto?> GetNotificationByIdAsync(int id)
        {
            _logger.LogDebug("Getting notification with ID: {Id}", id);

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

        /// <inheritdoc />
        public async Task<NotificationDto> CreateNotificationAsync(CreateNotificationDto notification)
        {
            _logger.LogDebug("Creating new notification");

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
            var createdNotification = ConduitLLM.Core.Utilities.ReadBackGuard.RequireCreated(
                await _notificationRepository.GetByIdAsync(id),
                "notification",
                id);

            // Get virtual key name if applicable
            string? keyName = null;
            if (createdNotification.VirtualKeyId.HasValue)
            {
                var key = await _virtualKeyRepository.GetByIdAsync(createdNotification.VirtualKeyId.Value);
                keyName = key?.KeyName;
            }

            _logger.LogInformation("Created notification {NotificationId} of type {Type}",
                createdNotification.Id, createdNotification.Type);

            return createdNotification.ToDto(keyName);
        }

        /// <inheritdoc />
        public async Task<bool> UpdateNotificationAsync(int id, UpdateNotificationDto notification)
        {
            _logger.LogDebug("Updating notification with ID: {Id}", id);

            // Get the existing notification
            var existingNotification = await _notificationRepository.GetByIdAsync(id);
            if (existingNotification == null)
            {
                _logger.LogWarning("Notification with ID {Id} not found", id);
                return false;
            }

            if (notification.TryGetPatchedProperty(
                    nameof(notification.IsRead),
                    existingNotification.IsRead,
                    out bool isRead))
                existingNotification.IsRead = isRead;
            if (notification.TryGetPatchedProperty(
                    nameof(notification.Message),
                    existingNotification.Message,
                    out string? message))
                existingNotification.Message = message
                    ?? throw new InvalidOperationException("message cannot be null.");

            // Save changes
            return await _notificationRepository.UpdateAsync(existingNotification);
        }

        public Task<bool> UpdateNotificationAsync(UpdateNotificationDto notification) =>
            UpdateNotificationAsync(notification.Id, notification);

        /// <inheritdoc />
        public async Task<bool> MarkNotificationAsReadAsync(int id)
        {
            _logger.LogDebug("Marking notification with ID {Id} as read", id);

            return await _notificationRepository.MarkAsReadAsync(id);
        }

        /// <inheritdoc />
        public async Task<int> MarkAllNotificationsAsReadAsync()
        {
            _logger.LogDebug("Marking all notifications as read");

            // Get all unread notifications
            var unreadNotifications = await RepositoryPaginationExtensions.GetAllViaPaginationAsync(
                _notificationRepository.GetUnreadPaginatedAsync);
            if (!unreadNotifications.Any())
            {
                return 0;
            }

            // Mark each as read
            int count = 0;
            int failed = 0;
            foreach (var notification in unreadNotifications)
            {
                var success = await _notificationRepository.MarkAsReadAsync(notification.Id);
                if (success)
                {
                    count++;
                }
                else
                {
                    failed++;
                }
            }

            if (failed > 0)
            {
                _logger.LogWarning("MarkAllNotificationsAsRead: {Marked} marked, {Failed} failed out of {Total}",
                    count, failed, unreadNotifications.Count);
            }
            else if (count > 0)
            {
                _logger.LogInformation("Marked {Count} notifications as read", count);
            }

            return count;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteNotificationAsync(int id)
        {
            _logger.LogDebug("Deleting notification with ID: {Id}", id);

            var result = await _notificationRepository.DeleteAsync(id);
            if (result)
            {
                _logger.LogInformation("Deleted notification {NotificationId}", id);
            }
            return result;
        }
    }
}
