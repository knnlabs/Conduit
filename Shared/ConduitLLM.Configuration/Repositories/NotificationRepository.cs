using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Configuration.Repositories;

/// <summary>
/// Repository implementation for notifications using Entity Framework Core.
/// Inherits common CRUD operations from RepositoryBase.
/// </summary>
public class NotificationRepository : RepositoryBase<Notification, int>, INotificationRepository
{
    /// <summary>
    /// Creates a new instance of the repository.
    /// </summary>
    /// <param name="dbContextFactory">The database context factory</param>
    /// <param name="logger">The logger</param>
    public NotificationRepository(
        IDbContextFactory<ConduitDbContext> dbContextFactory,
        ILogger<NotificationRepository> logger)
        : base(dbContextFactory, logger)
    {
    }

    /// <inheritdoc/>
    protected override DbSet<Notification> GetDbSet(ConduitDbContext context) => context.Notifications;

    /// <inheritdoc/>
    protected override IQueryable<Notification> ApplyDefaultOrdering(IQueryable<Notification> query)
    {
        return query.OrderByDescending(n => n.CreatedAt);
    }

    /// <summary>
    /// Override to set CreatedAt for Notification (which only has CreatedAt, not UpdatedAt).
    /// </summary>
    protected override void OnBeforeCreate(Notification entity)
    {
        entity.CreatedAt = DateTime.UtcNow;
    }

    /// <inheritdoc/>
    public async Task<(List<Notification> Items, int TotalCount)> GetUnreadPaginatedAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        return await GetFilteredPaginatedAsync(
            n => !n.IsRead,
            pageNumber,
            pageSize,
            q => q.OrderByDescending(n => n.CreatedAt),
            cancellationToken,
            "getting unread notifications");
    }

    /// <inheritdoc/>
    public async Task<List<Notification>> GetUnreadByVirtualKeyIdAsync(
        int virtualKeyId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await ExecuteAsync(async context =>
            {
                return await GetDbSet(context)
                    .AsNoTracking()
                    .Where(n => !n.IsRead && n.VirtualKeyId == virtualKeyId)
                    .OrderByDescending(n => n.CreatedAt)
                    .ToListAsync(cancellationToken);
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting unread notifications for virtual key {VirtualKeyId}", virtualKeyId);
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
            return await ExecuteAsync(async context =>
            {
                return await GetDbSet(context)
                    .AsNoTracking()
                    .Where(n => !n.IsRead && n.VirtualKeyId == virtualKeyId && n.Type == notificationType)
                    .OrderByDescending(n => n.CreatedAt)
                    .ToListAsync(cancellationToken);
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting unread notifications for virtual key {VirtualKeyId} and type {NotificationType}",
                virtualKeyId, notificationType);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> MarkAsReadAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            return await ExecuteAsync(async context =>
            {
                var notification = await GetDbSet(context).FindAsync(new object[] { id }, cancellationToken);

                if (notification == null)
                {
                    return false;
                }

                notification.IsRead = true;
                int rowsAffected = await context.SaveChangesAsync(cancellationToken);
                return rowsAffected > 0;
            }, cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            Logger.LogError(ex, "Concurrency error marking notification with ID {NotificationId} as read", id);

            // Handle concurrency issues by retrying
            try
            {
                return await ExecuteAsync(async context =>
                {
                    var notification = await GetDbSet(context).FindAsync(new object[] { id }, cancellationToken);

                    if (notification == null)
                    {
                        return false;
                    }

                    notification.IsRead = true;
                    int rowsAffected = await context.SaveChangesAsync(cancellationToken);
                    return rowsAffected > 0;
                }, cancellationToken);
            }
            catch (Exception retryEx)
            {
                Logger.LogError(retryEx, "Error during retry of marking notification with ID {NotificationId} as read", id);
                throw;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error marking notification with ID {NotificationId} as read", id);
            throw;
        }
    }

    /// <summary>
    /// Override UpdateAsync to include concurrency retry logic.
    /// </summary>
    public override async Task<bool> UpdateAsync(Notification entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        try
        {
            return await base.UpdateAsync(entity, cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            Logger.LogError(ex, "Concurrency error updating notification with ID {NotificationId}", entity.Id);

            // Handle concurrency issues by reloading and reapplying changes
            try
            {
                return await ExecuteAsync(async context =>
                {
                    var existingEntity = await GetDbSet(context).FindAsync(new object[] { entity.Id }, cancellationToken);

                    if (existingEntity == null)
                    {
                        return false;
                    }

                    // Update properties
                    context.Entry(existingEntity).CurrentValues.SetValues(entity);

                    int rowsAffected = await context.SaveChangesAsync(cancellationToken);
                    return rowsAffected > 0;
                }, cancellationToken);
            }
            catch (Exception retryEx)
            {
                Logger.LogError(retryEx, "Error during retry of notification update with ID {NotificationId}", entity.Id);
                throw;
            }
        }
    }
}
