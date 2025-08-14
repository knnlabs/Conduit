using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Data;
using ConduitLLM.Configuration.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using ConduitLLM.Configuration.Interfaces;
namespace ConduitLLM.Configuration.Repositories
{
    /// <summary>
    /// Repository implementation for media record operations.
    /// </summary>
    public class MediaRecordRepository : IMediaRecordRepository
    {
        private readonly IDbContextFactory<ConduitDbContext> _contextFactory;
        private readonly ILogger<MediaRecordRepository> _logger;

        /// <summary>
        /// Initializes a new instance of the MediaRecordRepository class.
        /// </summary>
        /// <param name="contextFactory">The database context factory.</param>
        /// <param name="logger">The logger instance.</param>
        public MediaRecordRepository(
            IDbContextFactory<ConduitDbContext> contextFactory,
            ILogger<MediaRecordRepository> logger)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public async Task<MediaRecord> CreateAsync(MediaRecord mediaRecord)
        {
            ArgumentNullException.ThrowIfNull(mediaRecord);

            using var context = await _contextFactory.CreateDbContextAsync();
            
            context.MediaRecords.Add(mediaRecord);
            await context.SaveChangesAsync();
            
            _logger.LogInformation("Created media record {Id} for virtual key {VirtualKeyId}", 
                mediaRecord.Id, mediaRecord.VirtualKeyId);
            
            return mediaRecord;
        }

        /// <inheritdoc/>
        public async Task<MediaRecord?> GetByIdAsync(Guid id)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            
            return await context.MediaRecords
                .Include(m => m.VirtualKey)
                .FirstOrDefaultAsync(m => m.Id == id);
        }

        /// <inheritdoc/>
        public async Task<MediaRecord?> GetByStorageKeyAsync(string storageKey)
        {
            if (string.IsNullOrWhiteSpace(storageKey))
                return null;

            using var context = await _contextFactory.CreateDbContextAsync();
            
            return await context.MediaRecords
                .Include(m => m.VirtualKey)
                .FirstOrDefaultAsync(m => m.StorageKey == storageKey);
        }

        /// <inheritdoc/>
        public async Task<List<MediaRecord>> GetByVirtualKeyIdAsync(int virtualKeyId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            
            return await context.MediaRecords
                .Where(m => m.VirtualKeyId == virtualKeyId)
                .OrderByDescending(m => m.CreatedAt)
                .ToListAsync();
        }

        /// <inheritdoc/>
        public async Task<List<MediaRecord>> GetExpiredMediaAsync(DateTime currentTime)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            
            return await context.MediaRecords
                .Where(m => m.ExpiresAt != null && m.ExpiresAt <= currentTime)
                .ToListAsync();
        }

        /// <inheritdoc/>
        public async Task<List<MediaRecord>> GetMediaOlderThanAsync(DateTime cutoffDate)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            
            return await context.MediaRecords
                .Where(m => m.CreatedAt < cutoffDate)
                .ToListAsync();
        }

        /// <inheritdoc/>
        public async Task<List<MediaRecord>> GetOrphanedMediaAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            
            // Find media records where the virtual key no longer exists
            var orphanedMedia = await context.MediaRecords
                .Where(m => !context.VirtualKeys.Any(vk => vk.Id == m.VirtualKeyId))
                .ToListAsync();
            
            if (orphanedMedia.Count() > 0)
            {
                _logger.LogWarning("Found {Count} orphaned media records", orphanedMedia.Count);
            }
            
            return orphanedMedia;
        }

        /// <inheritdoc/>
        public async Task<bool> UpdateAccessStatsAsync(Guid id)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            
            var mediaRecord = await context.MediaRecords.FindAsync(id);
            if (mediaRecord == null)
                return false;
            
            mediaRecord.AccessCount++;
            mediaRecord.LastAccessedAt = DateTime.UtcNow;
            
            await context.SaveChangesAsync();
            return true;
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteAsync(Guid id)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            
            var mediaRecord = await context.MediaRecords.FindAsync(id);
            if (mediaRecord == null)
                return false;
            
            context.MediaRecords.Remove(mediaRecord);
            await context.SaveChangesAsync();
            
            _logger.LogInformation("Deleted media record {Id}", id);
            return true;
        }

        /// <inheritdoc/>
        public async Task<int> DeleteManyAsync(IEnumerable<Guid> ids)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            
            var idList = ids.ToList();
            var mediaRecords = await context.MediaRecords
                .Where(m => idList.Contains(m.Id))
                .ToListAsync();
            
            if (mediaRecords.Count() > 0)
            {
                context.MediaRecords.RemoveRange(mediaRecords);
                await context.SaveChangesAsync();
                
                _logger.LogInformation("Deleted {Count} media records", mediaRecords.Count);
            }
            
            return mediaRecords.Count;
        }

        /// <inheritdoc/>
        public async Task<long> GetTotalStorageSizeByVirtualKeyAsync(int virtualKeyId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            
            return await context.MediaRecords
                .Where(m => m.VirtualKeyId == virtualKeyId && m.SizeBytes.HasValue)
                .SumAsync(m => m.SizeBytes ?? 0);
        }

        /// <inheritdoc/>
        public async Task<Dictionary<string, long>> GetStorageStatsByProviderAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            
            var stats = await context.MediaRecords
                .Where(m => m.Provider != null && m.SizeBytes.HasValue)
                .GroupBy(m => m.Provider!)
                .Select(g => new { Provider = g.Key, TotalSize = g.Sum(m => m.SizeBytes ?? 0) })
                .ToDictionaryAsync(x => x.Provider, x => x.TotalSize);
            
            return stats;
        }

        /// <inheritdoc/>
        public async Task<Dictionary<string, long>> GetStorageStatsByMediaTypeAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            
            var stats = await context.MediaRecords
                .Where(m => m.SizeBytes.HasValue)
                .GroupBy(m => m.MediaType)
                .Select(g => new { MediaType = g.Key, TotalSize = g.Sum(m => m.SizeBytes ?? 0) })
                .ToDictionaryAsync(x => x.MediaType, x => x.TotalSize);
            
            return stats;
        }

        /// <inheritdoc/>
        public async Task<int> GetCountByVirtualKeyAsync(int virtualKeyId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            
            return await context.MediaRecords
                .CountAsync(m => m.VirtualKeyId == virtualKeyId);
        }
    }
}