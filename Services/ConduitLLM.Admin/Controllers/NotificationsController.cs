using ConduitLLM.Admin.Extensions;
using ConduitLLM.Admin.Filters;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Core.Extensions;

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
    [ServiceFilter(typeof(OperationLoggingFilter))]
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
        public async Task<IActionResult> GetAllNotifications()
        {
            var notifications = await _notificationService.GetAllNotificationsAsync();
            return Ok(notifications);
        }

        /// <summary>
        /// Gets all unread notifications
        /// </summary>
        /// <returns>List of unread notifications</returns>
        [HttpGet("unread")]
        [ProducesResponseType(typeof(IEnumerable<NotificationDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetUnreadNotifications()
        {
            var notifications = await _notificationService.GetUnreadNotificationsAsync();
            return Ok(notifications);
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
        public async Task<IActionResult> GetNotificationById(int id)
        {
            var notification = await _notificationService.GetNotificationByIdAsync(id);
            if (notification == null)
            {
                return this.NotFoundEntity("Notification", id);
            }
            return Ok(notification);
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
        public async Task<IActionResult> CreateNotification([FromBody] CreateNotificationDto notification)
        {
            var result = await _notificationService.CreateNotificationAsync(notification);
            LogAdminAudit("Created", "Notification", result.Id, $"Type: {result.Type}, Message: {LoggingSanitizer.S(result.Message)}");
            return CreatedAtAction(nameof(GetNotificationById), new { id = result.Id }, result);
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
        public async Task<IActionResult> UpdateNotification(int id, [FromBody] UpdateNotificationDto notification)
        {
            // Ensure ID in route matches ID in body
            if (id != notification.Id)
            {
                return BadRequest("ID in route must match ID in body");
            }

            if (!await _notificationService.UpdateNotificationAsync(notification))
                throw new KeyNotFoundException();
            LogAdminAudit("Updated", "Notification", id, notification.Message != null ? $"Message: {LoggingSanitizer.S(notification.Message)}" : null);
            return NoContent();
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
        public async Task<IActionResult> MarkAsRead(int id)
        {
            if (!await _notificationService.MarkNotificationAsReadAsync(id))
                throw new KeyNotFoundException();
            LogAdminAudit("MarkedAsRead", "Notification", id, "IsRead: true");
            return NoContent();
        }

        /// <summary>
        /// Marks all notifications as read
        /// </summary>
        /// <returns>The number of notifications marked as read</returns>
        [HttpPost("mark-all-read")]
        [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var count = await _notificationService.MarkAllNotificationsAsReadAsync();
            LogAdminAudit("MarkedAllAsRead", "Notification", detail: $"Count: {count}");
            return Ok(count);
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
        public async Task<IActionResult> DeleteNotification(int id)
        {
            if (!await _notificationService.DeleteNotificationAsync(id))
                throw new KeyNotFoundException();
            LogAdminAudit("Deleted", "Notification", id, $"Id: {id}");
            return NoContent();
        }
    }
}
