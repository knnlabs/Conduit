using ConduitLLM.Configuration.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using ConduitLLM.Configuration.Interfaces;
namespace ConduitLLM.Configuration.Repositories
{
    /// <summary>
    /// Repository implementation for notifications using Entity Framework Core
    /// </summary>
    public class NotificationRepository : INotificationRepository
    {
        private readonly IDbContextFactory<ConduitDbContext> _dbContextFactory;
        private readonly ILogger<NotificationRepository> _logger;

        /// <summary>
        /// Creates a new instance of the repository
        /// </summary>
        /// <param name="dbContextFactory">The database context factory</param>
        /// <param name="logger">The logger</param>
        public NotificationRepository(
            IDbContextFactory<ConduitDbContext> dbContextFactory,
            ILogger<NotificationRepository> logger)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public async Task<Notification?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                return await dbContext.Notifications
                    .AsNoTracking()
                    .FirstOrDefaultAsync(n => n.Id == id, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting notification with ID {NotificationId}", id);
                throw;
            }
        }

        /// <inheritdoc/>
        [Obsolete("Use GetPaginatedAsync instead. This method loads all records into memory and will be removed in a future version.")]
        public async Task<List<Notification>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                return await dbContext.Notifications
                    .AsNoTracking()
                    .OrderByDescending(n => n.CreatedAt)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all notifications");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<(List<Notification> Items, int TotalCount)> GetPaginatedAsync(
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            if (pageNumber < 1)
            {
                throw new ArgumentException("Page number must be greater than or equal to 1", nameof(pageNumber));
            }

            if (pageSize < 1)
            {
                throw new ArgumentException("Page size must be greater than or equal to 1", nameof(pageSize));
            }

            const int maxPageSize = 100;
            if (pageSize > maxPageSize)
            {
                _logger.LogWarning("Requested page size {RequestedPageSize} exceeds maximum allowed {MaxPageSize}, limiting to maximum",
                    pageSize, maxPageSize);
                pageSize = maxPageSize;
            }

            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

                var query = dbContext.Notifications.AsNoTracking();
                var totalCount = await query.CountAsync(cancellationToken);

                var items = await query
                    .OrderByDescending(n => n.CreatedAt)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync(cancellationToken);

                return (items, totalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting paginated notifications for page {PageNumber}, size {PageSize}",
                    pageNumber, pageSize);
                throw;
            }
        }

        /// <inheritdoc/>
        [Obsolete("Use GetUnreadPaginatedAsync instead. This method loads all records into memory and will be removed in a future version.")]
        public async Task<List<Notification>> GetUnreadAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                return await dbContext.Notifications
                    .AsNoTracking()
                    .Where(n => !n.IsRead)
                    .OrderByDescending(n => n.CreatedAt)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting unread notifications");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<(List<Notification> Items, int TotalCount)> GetUnreadPaginatedAsync(
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            if (pageNumber < 1)
            {
                throw new ArgumentException("Page number must be greater than or equal to 1", nameof(pageNumber));
            }

            if (pageSize < 1)
            {
                throw new ArgumentException("Page size must be greater than or equal to 1", nameof(pageSize));
            }

            const int maxPageSize = 100;
            if (pageSize > maxPageSize)
            {
                _logger.LogWarning("Requested page size {RequestedPageSize} exceeds maximum allowed {MaxPageSize}, limiting to maximum",
                    pageSize, maxPageSize);
                pageSize = maxPageSize;
            }

            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

                var query = dbContext.Notifications
                    .AsNoTracking()
                    .Where(n => !n.IsRead);

                var totalCount = await query.CountAsync(cancellationToken);

                var items = await query
                    .OrderByDescending(n => n.CreatedAt)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync(cancellationToken);

                return (items, totalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting paginated unread notifications for page {PageNumber}, size {PageSize}",
                    pageNumber, pageSize);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<List<Notification>> GetUnreadByVirtualKeyIdAsync(
            int virtualKeyId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                return await dbContext.Notifications
                    .AsNoTracking()
                    .Where(n => !n.IsRead && n.VirtualKeyId == virtualKeyId)
                    .OrderByDescending(n => n.CreatedAt)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting unread notifications for virtual key {VirtualKeyId}", virtualKeyId);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<List<Notification>> GetUnreadByVirtualKeyAndTypeAsync(
            int virtualKeyId,
            NotificationType notificationType,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                return await dbContext.Notifications
                    .AsNoTracking()
                    .Where(n => !n.IsRead && n.VirtualKeyId == virtualKeyId && n.Type == notificationType)
                    .OrderByDescending(n => n.CreatedAt)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting unread notifications for virtual key {VirtualKeyId} and type {NotificationType}",
                    virtualKeyId, notificationType);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<int> CreateAsync(Notification notification, CancellationToken cancellationToken = default)
        {
            if (notification == null)
            {
                throw new ArgumentNullException(nameof(notification));
            }

            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

                // Set created timestamp
                notification.CreatedAt = DateTime.UtcNow;

                dbContext.Notifications.Add(notification);
                await dbContext.SaveChangesAsync(cancellationToken);
                return notification.Id;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error creating notification '{NotificationType}'", notification.Type);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating notification '{NotificationType}'", notification.Type);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> UpdateAsync(Notification notification, CancellationToken cancellationToken = default)
        {
            if (notification == null)
            {
                throw new ArgumentNullException(nameof(notification));
            }

            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

                // Ensure the entity is tracked
                dbContext.Notifications.Update(notification);

                int rowsAffected = await dbContext.SaveChangesAsync(cancellationToken);
                return rowsAffected > 0;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError(ex, "Concurrency error updating notification with ID {NotificationId}", notification.Id);

                // Handle concurrency issues by reloading and reapplying changes if needed
                try
                {
                    using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                    var existingEntity = await dbContext.Notifications.FindAsync(new object[] { notification.Id }, cancellationToken);

                    if (existingEntity == null)
                    {
                        return false;
                    }

                    // Update properties
                    dbContext.Entry(existingEntity).CurrentValues.SetValues(notification);

                    int rowsAffected = await dbContext.SaveChangesAsync(cancellationToken);
                    return rowsAffected > 0;
                }
                catch (Exception retryEx)
                {
                    _logger.LogError(retryEx, "Error during retry of notification update with ID {NotificationId}", notification.Id);
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating notification with ID {NotificationId}", notification.Id);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> MarkAsReadAsync(int id, CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                var notification = await dbContext.Notifications.FindAsync(new object[] { id }, cancellationToken);

                if (notification == null)
                {
                    return false;
                }

                notification.IsRead = true;

                int rowsAffected = await dbContext.SaveChangesAsync(cancellationToken);
                return rowsAffected > 0;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError(ex, "Concurrency error marking notification with ID {NotificationId} as read", id);

                // Handle concurrency issues by retrying
                try
                {
                    using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                    var notification = await dbContext.Notifications.FindAsync(new object[] { id }, cancellationToken);

                    if (notification == null)
                    {
                        return false;
                    }

                    notification.IsRead = true;

                    int rowsAffected = await dbContext.SaveChangesAsync(cancellationToken);
                    return rowsAffected > 0;
                }
                catch (Exception retryEx)
                {
                    _logger.LogError(retryEx, "Error during retry of marking notification with ID {NotificationId} as read", id);
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking notification with ID {NotificationId} as read", id);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                var notification = await dbContext.Notifications.FindAsync(new object[] { id }, cancellationToken);

                if (notification == null)
                {
                    return false;
                }

                dbContext.Notifications.Remove(notification);
                int rowsAffected = await dbContext.SaveChangesAsync(cancellationToken);
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting notification with ID {NotificationId}", id);
                throw;
            }
        }
    }
}
