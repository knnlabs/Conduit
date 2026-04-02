using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Configuration.Interfaces;

/// <summary>
/// Repository interface for managing notifications.
/// Inherits standard CRUD operations from IRepositoryBase.
/// </summary>
public interface INotificationRepository : IRepositoryBase<Notification, int>
{
    /// <summary>
    /// Gets unread notifications with pagination.
    /// </summary>
    /// <param name="pageNumber">The page number (1-based)</param>
    /// <param name="pageSize">The number of items per page</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A tuple with the list of unread notifications and the total count</returns>
    Task<(List<Notification> Items, int TotalCount)> GetUnreadPaginatedAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets unread notifications for a specific virtual key.
    /// </summary>
    /// <param name="virtualKeyId">The virtual key ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A list of unread notifications for the specified virtual key</returns>
    Task<List<Notification>> GetUnreadByVirtualKeyIdAsync(int virtualKeyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets unread notifications for a specific virtual key and notification type.
    /// </summary>
    /// <param name="virtualKeyId">The virtual key ID</param>
    /// <param name="notificationType">The notification type</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A list of unread notifications matching the criteria</returns>
    Task<List<Notification>> GetUnreadByVirtualKeyAndTypeAsync(
        int virtualKeyId,
        NotificationType notificationType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a notification as read.
    /// </summary>
    /// <param name="id">The ID of the notification to mark as read</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if successful, false otherwise</returns>
    Task<bool> MarkAsReadAsync(int id, CancellationToken cancellationToken = default);
}
