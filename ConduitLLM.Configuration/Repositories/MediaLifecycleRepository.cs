using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ConduitLLM.Configuration.Data;
using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Configuration.Repositories
{
    /// <summary>
    /// Repository implementation for media lifecycle records
    /// </summary>
    public class MediaLifecycleRepository : IMediaLifecycleRepository
    {
        private readonly IDbContextFactory<ConduitDbContext> _contextFactory;

        public MediaLifecycleRepository(IDbContextFactory<ConduitDbContext> contextFactory)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        }

        /// <inheritdoc />
        public async Task<MediaLifecycleRecord> AddAsync(MediaLifecycleRecord record)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            
            record.CreatedAt = DateTime.UtcNow;
            record.UpdatedAt = DateTime.UtcNow;
            
            context.MediaLifecycleRecords.Add(record);
            await context.SaveChangesAsync();
            
            return record;
        }

        /// <inheritdoc />
        public async Task<IList<MediaLifecycleRecord>> GetByVirtualKeyIdAsync(int virtualKeyId)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            
            return await context.MediaLifecycleRecords
                .Where(m => m.VirtualKeyId == virtualKeyId && !m.IsDeleted)
                .OrderByDescending(m => m.GeneratedAt)
                .ToListAsync();
        }

        /// <inheritdoc />
        public async Task<IList<MediaLifecycleRecord>> GetExpiredMediaAsync(DateTime cutoffDate)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            
            return await context.MediaLifecycleRecords
                .Where(m => m.ExpiresAt.HasValue && m.ExpiresAt.Value <= cutoffDate && !m.IsDeleted)
                .OrderBy(m => m.ExpiresAt)
                .ToListAsync();
        }

        /// <inheritdoc />
        public async Task<bool> MarkAsDeletedAsync(int recordId)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            
            var record = await context.MediaLifecycleRecords.FindAsync(recordId);
            if (record == null)
                return false;

            record.IsDeleted = true;
            record.DeletedAt = DateTime.UtcNow;
            record.UpdatedAt = DateTime.UtcNow;

            await context.SaveChangesAsync();
            return true;
        }

        /// <inheritdoc />
        public async Task<int> DeleteByVirtualKeyIdAsync(int virtualKeyId)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            
            var records = await context.MediaLifecycleRecords
                .Where(m => m.VirtualKeyId == virtualKeyId && !m.IsDeleted)
                .ToListAsync();

            var now = DateTime.UtcNow;
            foreach (var record in records)
            {
                record.IsDeleted = true;
                record.DeletedAt = now;
                record.UpdatedAt = now;
            }

            await context.SaveChangesAsync();
            return records.Count;
        }

        /// <inheritdoc />
        public async Task<long> GetTotalStorageByVirtualKeyIdAsync(int virtualKeyId)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            
            return await context.MediaLifecycleRecords
                .Where(m => m.VirtualKeyId == virtualKeyId && !m.IsDeleted)
                .SumAsync(m => m.FileSizeBytes);
        }

        /// <inheritdoc />
        public async Task<MediaLifecycleRecord?> GetByStorageKeyAsync(string storageKey)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            
            return await context.MediaLifecycleRecords
                .FirstOrDefaultAsync(m => m.StorageKey == storageKey && !m.IsDeleted);
        }
    }
}