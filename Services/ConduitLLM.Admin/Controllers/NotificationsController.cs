using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.DTOs;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConduitLLM.Admin.Controllers
{
    /// <summary>
    /// Controller for managing notifications
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "MasterKeyPolicy")]
    public class NotificationsController : AdminControllerBase
    {
        private readonly IAdminNotificationService _notificationService;

        /// <summary>
        /// Initializes a new instance of the NotificationsController
        /// </summary>
        /// <param name="notificationService">The notification service</param>
        /// <param name="logger">The logger</param>
        public NotificationsController(
            IAdminNotificationService notificationService,
            ILogger<NotificationsController> logger)
            : base(logger)
        {
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        }

        /// <summary>
        /// Gets all notifications
        /// </summary>
        /// <returns>List of all notifications</returns>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<NotificationDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public Task<IActionResult> GetAllNotifications()
        {
            return ExecuteAsync(
                () => _notificationService.GetAllNotificationsAsync(),
                Ok,
                "GetAllNotifications");
        }

        /// <summary>
        /// Gets all unread notifications
        /// </summary>
        /// <returns>List of unread notifications</returns>
        [HttpGet("unread")]
        [ProducesResponseType(typeof(IEnumerable<NotificationDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public Task<IActionResult> GetUnreadNotifications()
        {
            return ExecuteAsync(
                () => _notificationService.GetUnreadNotificationsAsync(),
                Ok,
                "GetUnreadNotifications");
        }

        /// <summary>
        /// Gets a notification by ID
        /// </summary>
        /// <param name="id">The ID of the notification to get</param>
        /// <returns>The notification</returns>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(NotificationDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public Task<IActionResult> GetNotificationById(int id)
        {
            return ExecuteWithNotFoundAsync(
                () => _notificationService.GetNotificationByIdAsync(id),
                Ok,
                "Notification",
                id,
                "GetNotificationById");
        }

        /// <summary>
        /// Creates a new notification
        /// </summary>
        /// <param name="notification">The notification to create</param>
        /// <returns>The created notification</returns>
        [HttpPost]
        [ProducesResponseType(typeof(NotificationDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public Task<IActionResult> CreateNotification([FromBody] CreateNotificationDto notification)
        {
            return ExecuteAsync(
                async () =>
                {
                    var result = await _notificationService.CreateNotificationAsync(notification);
                    LogAdminAudit("Created", "Notification", result.Id);
                    return result;
                },
                createdNotification => CreatedAtAction(nameof(GetNotificationById), new { id = createdNotification.Id }, createdNotification),
                "CreateNotification");
        }

        /// <summary>
        /// Updates an existing notification
        /// </summary>
        /// <param name="id">The ID of the notification to update</param>
        /// <param name="notification">The updated notification data</param>
        /// <returns>No content if successful</returns>
        [HttpPut("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public Task<IActionResult> UpdateNotification(int id, [FromBody] UpdateNotificationDto notification)
        {
            // Ensure ID in route matches ID in body
            if (id != notification.Id)
            {
                return Task.FromResult<IActionResult>(BadRequest("ID in route must match ID in body"));
            }

            return ExecuteAsync(
                async () =>
                {
                    if (!await _notificationService.UpdateNotificationAsync(notification))
                        throw new KeyNotFoundException();
                    LogAdminAudit("Updated", "Notification", id);
                },
                NoContent(),
                "UpdateNotification",
                new { Id = id });
        }

        /// <summary>
        /// Marks a notification as read
        /// </summary>
        /// <param name="id">The ID of the notification to mark as read</param>
        /// <returns>No content if successful</returns>
        [HttpPost("{id}/read")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public Task<IActionResult> MarkAsRead(int id)
        {
            return ExecuteAsync(
                async () =>
                {
                    if (!await _notificationService.MarkNotificationAsReadAsync(id))
                        throw new KeyNotFoundException();
                },
                NoContent(),
                "MarkAsRead",
                new { Id = id });
        }

        /// <summary>
        /// Marks all notifications as read
        /// </summary>
        /// <returns>The number of notifications marked as read</returns>
        [HttpPost("mark-all-read")]
        [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public Task<IActionResult> MarkAllAsRead()
        {
            return ExecuteAsync(
                () => _notificationService.MarkAllNotificationsAsReadAsync(),
                result => Ok(result),
                "MarkAllAsRead");
        }

        /// <summary>
        /// Deletes a notification
        /// </summary>
        /// <param name="id">The ID of the notification to delete</param>
        /// <returns>No content if successful</returns>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public Task<IActionResult> DeleteNotification(int id)
        {
            return ExecuteAsync(
                async () =>
                {
                    if (!await _notificationService.DeleteNotificationAsync(id))
                        throw new KeyNotFoundException();
                    LogAdminAudit("Deleted", "Notification", id);
                },
                NoContent(),
                "DeleteNotification",
                new { Id = id });
        }
    }
}
