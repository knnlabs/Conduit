using ConduitLLM.Configuration.DTOs;

namespace ConduitLLM.Admin.Interfaces
{
    /// <summary>
    /// Service interface for managing notifications through the Admin API
    /// </summary>
    public interface IAdminNotificationService
    {
        /// <summary>
        /// Gets all notifications
        /// </summary>
        /// <returns>A list of all notifications</returns>
        Task<IEnumerable<NotificationDto>> GetAllNotificationsAsync();

        /// <summary>
        /// Gets only unread notifications
        /// </summary>
        /// <returns>A list of unread notifications</returns>
        Task<IEnumerable<NotificationDto>> GetUnreadNotificationsAsync();

        /// <summary>
        /// Gets a notification by ID
        /// </summary>
        /// <param name="id">The notification ID</param>
        /// <returns>The notification, or null if not found</returns>
        Task<NotificationDto?> GetNotificationByIdAsync(int id);

        /// <summary>
        /// Creates a new notification
        /// </summary>
        /// <param name="notification">The notification to create</param>
        /// <returns>The created notification</returns>
        Task<NotificationDto> CreateNotificationAsync(CreateNotificationDto notification);

        /// <summary>
        /// Updates a notification
        /// </summary>
        /// <param name="notification">The updated notification data</param>
        /// <returns>True if the update was successful, false otherwise</returns>
        Task<bool> UpdateNotificationAsync(UpdateNotificationDto notification);

        /// <summary>
        /// Marks a notification as read
        /// </summary>
        /// <param name="id">The ID of the notification to mark as read</param>
        /// <returns>True if successful, false otherwise</returns>
        Task<bool> MarkNotificationAsReadAsync(int id);

        /// <summary>
        /// Marks all notifications as read
        /// </summary>
        /// <returns>The number of notifications marked as read</returns>
        Task<int> MarkAllNotificationsAsReadAsync();

        /// <summary>
        /// Deletes a notification
        /// </summary>
        /// <param name="id">The ID of the notification to delete</param>
        /// <returns>True if the deletion was successful, false otherwise</returns>
        Task<bool> DeleteNotificationAsync(int id);
    }
}
