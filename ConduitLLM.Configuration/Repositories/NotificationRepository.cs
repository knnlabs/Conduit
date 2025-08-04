using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Data;
using ConduitLLM.Configuration.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static ConduitLLM.Configuration.Utilities.LogSanitizer;

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
