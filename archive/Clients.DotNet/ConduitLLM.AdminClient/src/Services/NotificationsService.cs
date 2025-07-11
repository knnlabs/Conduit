using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using ConduitLLM.AdminClient.Client;
using ConduitLLM.AdminClient.Models;

namespace ConduitLLM.AdminClient.Services;

/// <summary>
/// Service for managing notifications through the Admin API.
/// </summary>
public class NotificationsService : BaseApiClient
{
    private const string BaseEndpoint = "/api/notifications";

    /// <summary>
    /// Initializes a new instance of the NotificationsService class.
    /// </summary>
    /// <param name="httpClient">The HTTP client to use for requests.</param>
    /// <param name="configuration">The client configuration.</param>
    /// <param name="logger">Optional logger instance.</param>
    /// <param name="cache">Optional memory cache instance.</param>
    public NotificationsService(
        HttpClient httpClient,
        ConduitAdminClientConfiguration configuration,
        ILogger<NotificationsService>? logger = null,
        IMemoryCache? cache = null)
        : base(httpClient, configuration, logger, cache)
    {
    }

    #region Notification Management

    /// <summary>
    /// Retrieves all notifications ordered by creation date (descending).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of all notifications.</returns>
    public async Task<IEnumerable<NotificationDto>> GetAllNotificationsAsync(CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("Getting all notifications");

        try
        {
            var notifications = await GetAsync<IEnumerable<NotificationDto>>(
                BaseEndpoint, 
                cancellationToken: cancellationToken);

            _logger?.LogDebug("Retrieved {Count} notifications", notifications?.Count() ?? 0);
            return notifications ?? Enumerable.Empty<NotificationDto>();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get all notifications");
            throw;
        }
    }

    /// <summary>
    /// Retrieves only unread notifications.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of unread notifications.</returns>
    public async Task<IEnumerable<NotificationDto>> GetUnreadNotificationsAsync(CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("Getting unread notifications");

        try
        {
            var notifications = await GetAsync<IEnumerable<NotificationDto>>(
                $"{BaseEndpoint}/unread", 
                cancellationToken: cancellationToken);

            _logger?.LogDebug("Retrieved {Count} unread notifications", notifications?.Count() ?? 0);
            return notifications ?? Enumerable.Empty<NotificationDto>();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get unread notifications");
            throw;
        }
    }

    /// <summary>
    /// Retrieves a specific notification by ID.
    /// </summary>
    /// <param name="notificationId">The ID of the notification to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The notification if found, null otherwise.</returns>
    public async Task<NotificationDto?> GetNotificationByIdAsync(int notificationId, CancellationToken cancellationToken = default)
    {
        if (notificationId <= 0)
            throw new ArgumentException("Notification ID must be greater than 0", nameof(notificationId));

        _logger?.LogDebug("Getting notification {NotificationId}", notificationId);

        try
        {
            var notification = await GetAsync<NotificationDto>(
                $"{BaseEndpoint}/{notificationId}", 
                cancellationToken: cancellationToken);

            _logger?.LogDebug("Retrieved notification {NotificationId}", notificationId);
            return notification;
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("404"))
        {
            _logger?.LogWarning("Notification {NotificationId} not found", notificationId);
            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get notification {NotificationId}", notificationId);
            throw;
        }
    }

    /// <summary>
    /// Creates a new notification.
    /// </summary>
    /// <param name="request">The notification creation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created notification.</returns>
    public async Task<NotificationDto> CreateNotificationAsync(
        CreateNotificationDto request, 
        CancellationToken cancellationToken = default)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        _logger?.LogDebug("Creating notification of type {Type} with severity {Severity}", 
            request.Type, request.Severity);

        try
        {
            var notification = await PostAsync<NotificationDto>(
                BaseEndpoint, 
                request, 
                cancellationToken);

            _logger?.LogInformation("Created notification {NotificationId} of type {Type}", 
                notification.Id, notification.Type);
            return notification;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to create notification");
            throw;
        }
    }

    /// <summary>
    /// Updates an existing notification.
    /// </summary>
    /// <param name="notificationId">The ID of the notification to update.</param>
    /// <param name="request">The notification update request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated notification.</returns>
    public async Task<NotificationDto> UpdateNotificationAsync(
        int notificationId,
        UpdateNotificationDto request, 
        CancellationToken cancellationToken = default)
    {
        if (notificationId <= 0)
            throw new ArgumentException("Notification ID must be greater than 0", nameof(notificationId));
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        _logger?.LogDebug("Updating notification {NotificationId}", notificationId);

        try
        {
            var notification = await PutAsync<NotificationDto>(
                $"{BaseEndpoint}/{notificationId}", 
                request, 
                cancellationToken);

            _logger?.LogInformation("Updated notification {NotificationId}", notificationId);
            return notification;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to update notification {NotificationId}", notificationId);
            throw;
        }
    }

    /// <summary>
    /// Marks a specific notification as read.
    /// </summary>
    /// <param name="notificationId">The ID of the notification to mark as read.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task MarkAsReadAsync(int notificationId, CancellationToken cancellationToken = default)
    {
        if (notificationId <= 0)
            throw new ArgumentException("Notification ID must be greater than 0", nameof(notificationId));

        _logger?.LogDebug("Marking notification {NotificationId} as read", notificationId);

        try
        {
            await PostAsync($"{BaseEndpoint}/{notificationId}/read", cancellationToken: cancellationToken);

            _logger?.LogInformation("Marked notification {NotificationId} as read", notificationId);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to mark notification {NotificationId} as read", notificationId);
            throw;
        }
    }

    /// <summary>
    /// Marks all notifications as read.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of notifications that were marked as read.</returns>
    public async Task<int> MarkAllAsReadAsync(CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("Marking all notifications as read");

        try
        {
            var result = await PostAsync<int>($"{BaseEndpoint}/mark-all-read", cancellationToken: cancellationToken);

            _logger?.LogInformation("Marked {Count} notifications as read", result);
            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to mark all notifications as read");
            throw;
        }
    }

    /// <summary>
    /// Deletes a notification.
    /// </summary>
    /// <param name="notificationId">The ID of the notification to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task DeleteNotificationAsync(int notificationId, CancellationToken cancellationToken = default)
    {
        if (notificationId <= 0)
            throw new ArgumentException("Notification ID must be greater than 0", nameof(notificationId));

        _logger?.LogDebug("Deleting notification {NotificationId}", notificationId);

        try
        {
            await DeleteAsync($"{BaseEndpoint}/{notificationId}", cancellationToken);

            _logger?.LogInformation("Deleted notification {NotificationId}", notificationId);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to delete notification {NotificationId}", notificationId);
            throw;
        }
    }

    #endregion

    #region Filtered Queries

    /// <summary>
    /// Gets notifications by type.
    /// </summary>
    /// <param name="type">The notification type to filter by.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Notifications of the specified type.</returns>
    public async Task<IEnumerable<NotificationDto>> GetNotificationsByTypeAsync(
        NotificationType type, 
        CancellationToken cancellationToken = default)
    {
        var allNotifications = await GetAllNotificationsAsync(cancellationToken);
        return allNotifications.Where(n => n.Type == type);
    }

    /// <summary>
    /// Gets notifications by severity.
    /// </summary>
    /// <param name="severity">The notification severity to filter by.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Notifications of the specified severity.</returns>
    public async Task<IEnumerable<NotificationDto>> GetNotificationsBySeverityAsync(
        NotificationSeverity severity, 
        CancellationToken cancellationToken = default)
    {
        var allNotifications = await GetAllNotificationsAsync(cancellationToken);
        return allNotifications.Where(n => n.Severity == severity);
    }

    /// <summary>
    /// Gets notifications for a specific virtual key.
    /// </summary>
    /// <param name="virtualKeyId">The virtual key ID to filter by.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Notifications associated with the specified virtual key.</returns>
    public async Task<IEnumerable<NotificationDto>> GetNotificationsForVirtualKeyAsync(
        int virtualKeyId, 
        CancellationToken cancellationToken = default)
    {
        if (virtualKeyId <= 0)
            throw new ArgumentException("Virtual key ID must be greater than 0", nameof(virtualKeyId));

        var allNotifications = await GetAllNotificationsAsync(cancellationToken);
        return allNotifications.Where(n => n.VirtualKeyId == virtualKeyId);
    }

    /// <summary>
    /// Gets notifications created within a specific date range.
    /// </summary>
    /// <param name="startDate">The start date (inclusive).</param>
    /// <param name="endDate">The end date (inclusive).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Notifications created within the specified date range.</returns>
    public async Task<IEnumerable<NotificationDto>> GetNotificationsByDateRangeAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        if (startDate > endDate)
            throw new ArgumentException("Start date cannot be greater than end date");

        var allNotifications = await GetAllNotificationsAsync(cancellationToken);
        return allNotifications.Where(n => n.CreatedAt >= startDate && n.CreatedAt <= endDate);
    }

    #endregion

    #region Statistics and Analytics

    /// <summary>
    /// Gets notification statistics including counts by type, severity, and read status.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Notification statistics summary.</returns>
    public async Task<object> GetNotificationStatisticsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var allNotifications = await GetAllNotificationsAsync(cancellationToken);
            var notificationsList = allNotifications.ToList();

            return new
            {
                Total = notificationsList.Count,
                Unread = notificationsList.Count(n => !n.IsRead),
                Read = notificationsList.Count(n => n.IsRead),
                ByType = notificationsList
                    .GroupBy(n => n.Type)
                    .ToDictionary(g => g.Key.ToString(), g => g.Count()),
                BySeverity = notificationsList
                    .GroupBy(n => n.Severity)
                    .ToDictionary(g => g.Key.ToString(), g => g.Count()),
                Recent = new
                {
                    LastHour = notificationsList.Count(n => n.CreatedAt > DateTime.UtcNow.AddHours(-1)),
                    Last24Hours = notificationsList.Count(n => n.CreatedAt > DateTime.UtcNow.AddDays(-1)),
                    LastWeek = notificationsList.Count(n => n.CreatedAt > DateTime.UtcNow.AddDays(-7))
                }
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get notification statistics");
            throw;
        }
    }

    /// <summary>
    /// Gets the count of unread notifications.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of unread notifications.</returns>
    public async Task<int> GetUnreadCountAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var unreadNotifications = await GetUnreadNotificationsAsync(cancellationToken);
            return unreadNotifications.Count();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get unread notification count");
            throw;
        }
    }

    /// <summary>
    /// Checks if there are any unread notifications.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if there are unread notifications, false otherwise.</returns>
    public async Task<bool> HasUnreadNotificationsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var count = await GetUnreadCountAsync(cancellationToken);
            return count > 0;
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region Bulk Operations

    /// <summary>
    /// Marks multiple notifications as read by their IDs.
    /// </summary>
    /// <param name="notificationIds">The IDs of notifications to mark as read.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of notifications successfully marked as read.</returns>
    public async Task<int> MarkMultipleAsReadAsync(
        IEnumerable<int> notificationIds, 
        CancellationToken cancellationToken = default)
    {
        if (notificationIds == null)
            throw new ArgumentNullException(nameof(notificationIds));

        var ids = notificationIds.ToList();
        if (!ids.Any())
            return 0;

        _logger?.LogDebug("Marking {Count} notifications as read", ids.Count);

        var successCount = 0;
        foreach (var id in ids)
        {
            try
            {
                await MarkAsReadAsync(id, cancellationToken);
                successCount++;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to mark notification {NotificationId} as read", id);
            }
        }

        _logger?.LogInformation("Successfully marked {SuccessCount}/{TotalCount} notifications as read", 
            successCount, ids.Count);
        return successCount;
    }

    /// <summary>
    /// Deletes multiple notifications by their IDs.
    /// </summary>
    /// <param name="notificationIds">The IDs of notifications to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of notifications successfully deleted.</returns>
    public async Task<int> DeleteMultipleAsync(
        IEnumerable<int> notificationIds, 
        CancellationToken cancellationToken = default)
    {
        if (notificationIds == null)
            throw new ArgumentNullException(nameof(notificationIds));

        var ids = notificationIds.ToList();
        if (!ids.Any())
            return 0;

        _logger?.LogDebug("Deleting {Count} notifications", ids.Count);

        var successCount = 0;
        foreach (var id in ids)
        {
            try
            {
                await DeleteNotificationAsync(id, cancellationToken);
                successCount++;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to delete notification {NotificationId}", id);
            }
        }

        _logger?.LogInformation("Successfully deleted {SuccessCount}/{TotalCount} notifications", 
            successCount, ids.Count);
        return successCount;
    }

    #endregion
}