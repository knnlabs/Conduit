using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Configuration.Repositories
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
        Task<List<Notification>> GetAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets unread notifications
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A list of unread notifications</returns>
        Task<List<Notification>> GetUnreadAsync(CancellationToken cancellationToken = default);

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
