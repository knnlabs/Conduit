using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Data;
using ConduitLLM.Configuration.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Configuration.Repositories
{
    /// <summary>
    /// Repository implementation for virtual key spend history using Entity Framework Core
    /// </summary>
    public class VirtualKeySpendHistoryRepository : IVirtualKeySpendHistoryRepository
    {
        private readonly IDbContextFactory<ConfigurationDbContext> _dbContextFactory;
        private readonly ILogger<VirtualKeySpendHistoryRepository> _logger;

        /// <summary>
        /// Creates a new instance of the repository
        /// </summary>
        /// <param name="dbContextFactory">The database context factory</param>
        /// <param name="logger">The logger</param>
        public VirtualKeySpendHistoryRepository(
            IDbContextFactory<ConfigurationDbContext> dbContextFactory,
            ILogger<VirtualKeySpendHistoryRepository> logger)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public async Task<VirtualKeySpendHistory?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                return await dbContext.VirtualKeySpendHistories
                    .AsNoTracking()
                    .Include(h => h.VirtualKey)
                    .FirstOrDefaultAsync(h => h.Id == id, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting virtual key spend history with ID {HistoryId}", id);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<List<VirtualKeySpendHistory>> GetByVirtualKeyIdAsync(int virtualKeyId, CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                return await dbContext.VirtualKeySpendHistories
                    .AsNoTracking()
                    .Where(h => h.VirtualKeyId == virtualKeyId)
                    .OrderByDescending(h => h.Timestamp)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting spend history for virtual key with ID {VirtualKeyId}", virtualKeyId);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<List<VirtualKeySpendHistory>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                return await dbContext.VirtualKeySpendHistories
                    .AsNoTracking()
                    .Include(h => h.VirtualKey)
                    .Where(h => h.Timestamp >= startDate && h.Timestamp <= endDate)
                    .OrderByDescending(h => h.Timestamp)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting spend history for date range {StartDate} to {EndDate}", startDate, endDate);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<List<VirtualKeySpendHistory>> GetByVirtualKeyAndDateRangeAsync(
            int virtualKeyId, 
            DateTime startDate, 
            DateTime endDate, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                return await dbContext.VirtualKeySpendHistories
                    .AsNoTracking()
                    .Where(h => h.VirtualKeyId == virtualKeyId && h.Timestamp >= startDate && h.Timestamp <= endDate)
                    .OrderByDescending(h => h.Timestamp)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting spend history for virtual key {VirtualKeyId} and date range {StartDate} to {EndDate}", 
                    virtualKeyId, startDate, endDate);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<int> CreateAsync(VirtualKeySpendHistory spendHistory, CancellationToken cancellationToken = default)
        {
            if (spendHistory == null)
            {
                throw new ArgumentNullException(nameof(spendHistory));
            }

            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                
                // Set timestamp if not provided
                if (spendHistory.Timestamp == default)
                {
                    spendHistory.Timestamp = DateTime.UtcNow;
                }
                
                dbContext.VirtualKeySpendHistories.Add(spendHistory);
                await dbContext.SaveChangesAsync(cancellationToken);
                return spendHistory.Id;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error creating spend history for virtual key {VirtualKeyId}", 
                    spendHistory.VirtualKeyId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating spend history for virtual key {VirtualKeyId}", 
                    spendHistory.VirtualKeyId);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> UpdateAsync(VirtualKeySpendHistory spendHistory, CancellationToken cancellationToken = default)
        {
            if (spendHistory == null)
            {
                throw new ArgumentNullException(nameof(spendHistory));
            }

            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                
                dbContext.VirtualKeySpendHistories.Update(spendHistory);
                int rowsAffected = await dbContext.SaveChangesAsync(cancellationToken);
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating spend history with ID {HistoryId}", 
                    spendHistory.Id);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                var spendHistory = await dbContext.VirtualKeySpendHistories.FindAsync(new object[] { id }, cancellationToken);
                
                if (spendHistory == null)
                {
                    return false;
                }
                
                dbContext.VirtualKeySpendHistories.Remove(spendHistory);
                int rowsAffected = await dbContext.SaveChangesAsync(cancellationToken);
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting spend history with ID {HistoryId}", id);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<decimal> GetTotalSpendAsync(int virtualKeyId, CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                return await dbContext.VirtualKeySpendHistories
                    .Where(h => h.VirtualKeyId == virtualKeyId)
                    .SumAsync(h => h.Amount, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting total spend for virtual key {VirtualKeyId}", virtualKeyId);
                throw;
            }
        }
    }
}