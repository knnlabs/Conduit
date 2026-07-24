using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Configuration.Repositories;

/// <summary>
/// Repository implementation for media record operations.
/// Extends RepositoryBase for standard CRUD operations and implements domain-specific methods.
/// </summary>
public class MediaRecordRepository : RepositoryBase<MediaRecord, Guid>, IMediaRecordRepository
{
    /// <summary>
    /// Creates a new instance of the repository.
    /// </summary>
    /// <param name="dbContextFactory">The database context factory.</param>
    /// <param name="logger">The logger instance.</param>
    public MediaRecordRepository(
        IDbContextFactory<ConduitDbContext> dbContextFactory,
        ILogger<MediaRecordRepository> logger)
        : base(dbContextFactory, logger)
    {
    }

    /// <inheritdoc/>
    protected override DbSet<MediaRecord> GetDbSet(ConduitDbContext context)
        => context.MediaRecords;

    /// <inheritdoc/>
    protected override IQueryable<MediaRecord> ApplyDefaultIncludes(IQueryable<MediaRecord> query)
    {
        return query.Include(m => m.VirtualKey);
    }

    /// <inheritdoc/>
    protected override IQueryable<MediaRecord> ApplyDefaultOrdering(IQueryable<MediaRecord> query)
    {
        return query.OrderByDescending(m => m.CreatedAt);
    }

    /// <inheritdoc/>
    protected override void OnBeforeCreate(MediaRecord entity)
    {
        base.OnBeforeCreate(entity);

        // Set CreatedAt if not provided
        if (entity.CreatedAt == default)
        {
            entity.CreatedAt = DateTime.UtcNow;
        }
    }

    /// <inheritdoc/>
    public async Task<MediaRecord?> GetByStorageKeyAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
        {
            return null;
        }

        return await ExecuteAsync(async context =>
            await ApplyDefaultIncludes(GetDbSet(context).AsNoTracking())
                .FirstOrDefaultAsync(m => m.StorageKey == storageKey, cancellationToken),
            cancellationToken, $"getting by storage key {storageKey}");
    }

    /// <inheritdoc/>
    public async Task<List<MediaRecord>> GetByVirtualKeyIdAsync(int virtualKeyId, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async context =>
            await GetDbSet(context)
                .AsNoTracking()
                .Where(m => m.VirtualKeyId == virtualKeyId)
                .OrderByDescending(m => m.CreatedAt)
                .ToListAsync(cancellationToken),
            cancellationToken, $"getting by virtual key ID {virtualKeyId}");
    }

    /// <inheritdoc/>
    public async Task<List<MediaRecord>> GetExpiredMediaAsync(DateTime currentTime, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async context =>
            await GetDbSet(context)
                .AsNoTracking()
                .Where(m => m.ExpiresAt != null && m.ExpiresAt <= currentTime)
                .ToListAsync(cancellationToken),
            cancellationToken, "getting expired media");
    }

    /// <inheritdoc/>
    public async Task<List<MediaRecord>> GetMediaOlderThanAsync(DateTime cutoffDate, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async context =>
            await GetDbSet(context)
                .AsNoTracking()
                .Where(m => m.CreatedAt < cutoffDate)
                .ToListAsync(cancellationToken),
            cancellationToken, $"getting media older than {cutoffDate:d}");
    }

    /// <inheritdoc/>
    public async Task<bool> UpdateAccessStatsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async context =>
        {
            var mediaRecord = await GetDbSet(context).FindAsync(new object[] { id }, cancellationToken);
            if (mediaRecord == null)
            {
                return false;
            }

            mediaRecord.AccessCount++;
            mediaRecord.LastAccessedAt = DateTime.UtcNow;

            await context.SaveChangesAsync(cancellationToken);
            return true;
        }, cancellationToken, $"updating access stats for ID {id}");
    }

    /// <inheritdoc/>
    public async Task<int> DeleteManyAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async context =>
        {
            var idList = ids.ToList();
            var mediaRecords = await GetDbSet(context)
                .Where(m => idList.Contains(m.Id))
                .ToListAsync(cancellationToken);

            if (mediaRecords.Count > 0)
            {
                GetDbSet(context).RemoveRange(mediaRecords);
                await context.SaveChangesAsync(cancellationToken);

                Logger.LogInformation("Deleted {Count} media records", mediaRecords.Count);
            }

            return mediaRecords.Count;
        }, cancellationToken, "deleting multiple");
    }

    /// <inheritdoc/>
    public async Task<long> GetTotalStorageSizeByVirtualKeyAsync(int virtualKeyId, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async context =>
            await GetDbSet(context)
                .Where(m => m.VirtualKeyId == virtualKeyId && m.SizeBytes.HasValue)
                .SumAsync(m => m.SizeBytes ?? 0, cancellationToken),
            cancellationToken, $"getting total storage size for virtual key {virtualKeyId}");
    }

    /// <inheritdoc/>
    public async Task<Dictionary<string, long>> GetStorageStatsByProviderAsync(CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async context =>
            await GetDbSet(context)
                .Where(m => m.Provider != null && m.SizeBytes.HasValue)
                .GroupBy(m => m.Provider!)
                .Select(g => new { Provider = g.Key, TotalSize = g.Sum(m => m.SizeBytes ?? 0) })
                .ToDictionaryAsync(x => x.Provider, x => x.TotalSize, cancellationToken),
            cancellationToken, "getting storage stats by provider");
    }

    /// <inheritdoc/>
    public async Task<Dictionary<string, long>> GetStorageStatsByMediaTypeAsync(CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async context =>
            await GetDbSet(context)
                .Where(m => m.SizeBytes.HasValue)
                .GroupBy(m => m.MediaType)
                .Select(g => new { MediaType = g.Key, TotalSize = g.Sum(m => m.SizeBytes ?? 0) })
                .ToDictionaryAsync(x => x.MediaType, x => x.TotalSize, cancellationToken),
            cancellationToken, "getting storage stats by media type");
    }

    /// <inheritdoc/>
    public async Task<int> GetCountByVirtualKeyAsync(int virtualKeyId, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async context =>
            await GetDbSet(context)
                .CountAsync(m => m.VirtualKeyId == virtualKeyId, cancellationToken),
            cancellationToken, $"getting count for virtual key {virtualKeyId}");
    }

    /// <inheritdoc/>
    public async Task<List<MediaRecord>> SearchByStorageKeyPatternAsync(string storageKeyPattern, int maxResults = 100, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storageKeyPattern))
        {
            return new List<MediaRecord>();
        }

        // Ensure maxResults is within reasonable bounds
        if (maxResults <= 0)
        {
            maxResults = 100;
        }
        else if (maxResults > 1000)
        {
            maxResults = 1000;
        }

        // Escape special characters in the pattern for LIKE/ILIKE
        var escapedPattern = storageKeyPattern
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_");

        // Use ILIKE for case-insensitive pattern matching in PostgreSQL
        var likePattern = $"%{escapedPattern}%";

        return await ExecuteAsync(async context =>
            await GetDbSet(context)
                .AsNoTracking()
                .Where(m => EF.Functions.ILike(m.StorageKey, likePattern))
                .OrderByDescending(m => m.CreatedAt)
                .Take(maxResults)
                .ToListAsync(cancellationToken),
            cancellationToken, "searching by storage key pattern");
    }
}
