using ConduitLLM.Configuration.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ConduitLLM.Configuration.Services
{
    /// <summary>
    /// Service for maintaining virtual keys including budget resets and expiration
    /// </summary>
    public class VirtualKeyMaintenanceService
    {
        private readonly VirtualKeyDbContext _context;
        
        /// <summary>
        /// Initializes a new instance of the VirtualKeyMaintenanceService
        /// </summary>
        /// <param name="context">Database context</param>
        public VirtualKeyMaintenanceService(VirtualKeyDbContext context)
        {
            _context = context;
        }
        
        /// <summary>
        /// Processes budget resets for all virtual keys
        /// </summary>
        public async Task ProcessBudgetResetsAsync()
        {
            var now = DateTime.UtcNow;
            
            // Use projection to retrieve only necessary fields for active keys needing potential resets
            var keysToCheck = await _context.VirtualKeys
                .AsNoTracking()
                .Where(k => k.IsEnabled)
                .Where(k => k.BudgetDuration != null)
                .Select(k => new
                {
                    k.Id,
                    k.BudgetDuration,
                    k.BudgetStartDate,
                    k.CurrentSpend
                })
                .ToListAsync();
                
            if (!keysToCheck.Any())
            {
                return;
            }
            
            var keysToReset = new List<int>();
            var spendHistory = new List<VirtualKeySpendHistory>();
            
            // Determine which keys need resetting
            foreach (var key in keysToCheck)
            {
                bool shouldReset = false;
                
                switch (key.BudgetDuration?.ToLower())
                {
                    case "daily":
                        // Reset if the day has changed
                        shouldReset = key.BudgetStartDate?.Date < now.Date;
                        break;
                        
                    case "monthly":
                        // Reset if the month or year has changed
                        shouldReset = key.BudgetStartDate?.Month != now.Month || 
                                     key.BudgetStartDate?.Year != now.Year;
                        break;
                    
                    // Add more budget periods as needed
                }
                
                if (shouldReset)
                {
                    keysToReset.Add(key.Id);
                    
                    // Create history record
                    spendHistory.Add(new VirtualKeySpendHistory
                    {
                        VirtualKeyId = key.Id,
                        Amount = key.CurrentSpend,
                        Date = key.BudgetStartDate ?? now
                    });
                }
            }
            
            if (!keysToReset.Any())
            {
                return;
            }
            
            // Check if the database provider supports transactions
            bool supportsTransactions = !(_context.Database.ProviderName?.Contains("InMemory") ?? false);
            
            // Create the transaction only if supported
            Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null;
            if (supportsTransactions)
            {
                transaction = await _context.Database.BeginTransactionAsync();
            }
            
            try
            {
                // Add spend history records in batch
                await _context.VirtualKeySpendHistory.AddRangeAsync(spendHistory);
                
                // Update keys in batches to reset spending
                // This is more efficient than updating one by one
                if (supportsTransactions)
                {
                    // Use SQL for relational databases
                    await _context.Database.ExecuteSqlRawAsync(
                        $"UPDATE VirtualKeys SET CurrentSpend = 0, BudgetStartDate = '{now:yyyy-MM-dd HH:mm:ss}', UpdatedAt = '{now:yyyy-MM-dd HH:mm:ss}' " + 
                        $"WHERE Id IN ({string.Join(",", keysToReset)})");
                }
                else
                {
                    // For in-memory database, update each entity directly
                    var keysToUpdate = await _context.VirtualKeys
                        .Where(k => keysToReset.Contains(k.Id))
                        .ToListAsync();
                        
                    foreach (var key in keysToUpdate)
                    {
                        key.CurrentSpend = 0;
                        key.BudgetStartDate = now;
                        key.UpdatedAt = now;
                    }
                }
                
                await _context.SaveChangesAsync();
                
                if (transaction != null)
                {
                    await transaction.CommitAsync();
                }
            }
            catch
            {
                if (transaction != null)
                {
                    await transaction.RollbackAsync();
                }
                throw;
            }
            finally
            {
                if (transaction != null)
                {
                    await transaction.DisposeAsync();
                }
            }
        }
        
        /// <summary>
        /// Disables all expired virtual keys
        /// </summary>
        public async Task DisableExpiredKeysAsync()
        {
            var now = DateTime.UtcNow;
            
            // Get only the IDs of expired keys that are currently active
            var expiredKeyIds = await _context.VirtualKeys
                .AsNoTracking()
                .Where(k => k.IsEnabled)
                .Where(k => k.ExpiresAt.HasValue && k.ExpiresAt.Value < now)
                .Select(k => k.Id)
                .ToListAsync();
                
            if (!expiredKeyIds.Any())
            {
                return;
            }
            
            // Check if the database provider supports transactions
            bool supportsTransactions = !(_context.Database.ProviderName?.Contains("InMemory") ?? false);
            
            // Create the transaction only if supported
            Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null;
            if (supportsTransactions)
            {
                transaction = await _context.Database.BeginTransactionAsync();
            }
            
            try
            {
                if (supportsTransactions)
                {
                    // Use SQL for relational databases
                    await _context.Database.ExecuteSqlRawAsync(
                        $"UPDATE VirtualKeys SET IsEnabled = 0, UpdatedAt = '{now:yyyy-MM-dd HH:mm:ss}' " + 
                        $"WHERE Id IN ({string.Join(",", expiredKeyIds)})");
                }
                else
                {
                    // For in-memory database, update each entity directly
                    var keysToUpdate = await _context.VirtualKeys
                        .Where(k => expiredKeyIds.Contains(k.Id))
                        .ToListAsync();
                        
                    foreach (var key in keysToUpdate)
                    {
                        key.IsEnabled = false;
                        key.UpdatedAt = now;
                    }
                }
                
                await _context.SaveChangesAsync();
                
                if (transaction != null)
                {
                    await transaction.CommitAsync();
                }
            }
            catch
            {
                if (transaction != null)
                {
                    await transaction.RollbackAsync();
                }
                throw;
            }
            finally
            {
                if (transaction != null)
                {
                    await transaction.DisposeAsync();
                }
            }
        }
        
        /// <summary>
        /// Checks keys approaching budget limits and returns their IDs
        /// </summary>
        /// <param name="thresholdPercentage">Threshold percentage (e.g., 90 for 90%)</param>
        /// <returns>List of key IDs approaching budget limits</returns>
        public async Task<List<int>> GetKeysApproachingBudgetLimitAsync(int thresholdPercentage = 90)
        {
            return await _context.VirtualKeys
                .AsNoTracking()
                .Where(k => k.IsEnabled)
                .Where(k => k.MaxBudget.HasValue && k.MaxBudget.Value > 0)
                .Where(k => k.MaxBudget.HasValue && (k.CurrentSpend / k.MaxBudget.Value) * 100 >= thresholdPercentage)
                .Select(k => k.Id)
                .ToListAsync();
        }
    }
}
