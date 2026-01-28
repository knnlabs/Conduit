using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Configuration.Interfaces
{
    /// <summary>
    /// Repository interface for managing notifications
    /// </summary>
    public interface INotificationRepository
    {
        /// <summary>
        /// Gets a notification by ID
        /// </summary>
        /// <param name="id">The notification ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The notification entity or null if not found</returns>
        Task<Notification?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all notifications
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A list of all notifications</returns>
        /// <remarks>This method is obsolete. Use GetPaginatedAsync instead for better performance.</remarks>
        [Obsolete("Use GetPaginatedAsync instead. This method loads all records into memory and will be removed in a future version.")]
        Task<List<Notification>> GetAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets notifications with pagination
        /// </summary>
        /// <param name="pageNumber">The page number (1-based)</param>
        /// <param name="pageSize">The number of items per page</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A tuple with the list of notifications and the total count</returns>
        Task<(List<Notification> Items, int TotalCount)> GetPaginatedAsync(
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets unread notifications
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A list of unread notifications</returns>
        /// <remarks>This method is obsolete. Use GetUnreadPaginatedAsync instead for better performance.</remarks>
        [Obsolete("Use GetUnreadPaginatedAsync instead. This method loads all records into memory and will be removed in a future version.")]
        Task<List<Notification>> GetUnreadAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets unread notifications with pagination
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
        /// Gets unread notifications for a specific virtual key
        /// </summary>
        /// <param name="virtualKeyId">The virtual key ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A list of unread notifications for the specified virtual key</returns>
        Task<List<Notification>> GetUnreadByVirtualKeyIdAsync(int virtualKeyId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets unread notifications for a specific virtual key and notification type
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
        /// Creates a new notification
        /// </summary>
        /// <param name="notification">The notification to create</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The ID of the created notification</returns>
        Task<int> CreateAsync(Notification notification, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates a notification
        /// </summary>
        /// <param name="notification">The notification to update</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the update was successful, false otherwise</returns>
        Task<bool> UpdateAsync(Notification notification, CancellationToken cancellationToken = default);

        /// <summary>
        /// Marks a notification as read
        /// </summary>
        /// <param name="id">The ID of the notification to mark as read</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if successful, false otherwise</returns>
        Task<bool> MarkAsReadAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a notification
        /// </summary>
        /// <param name="id">The ID of the notification to delete</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the deletion was successful, false otherwise</returns>
        Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
    }
}
