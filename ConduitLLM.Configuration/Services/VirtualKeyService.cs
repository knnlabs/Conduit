using ConduitLLM.Configuration.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ConduitLLM.Configuration.Services
{
    /// <summary>
    /// Service for managing virtual keys
    /// </summary>
    public class VirtualKeyService : IVirtualKeyService
    {
        private readonly VirtualKeyDbContext _context;
        
        /// <summary>
        /// Initializes a new instance of the VirtualKeyService
        /// </summary>
        /// <param name="context">Database context</param>
        public VirtualKeyService(VirtualKeyDbContext context)
        {
            _context = context;
        }
        
        /// <inheritdoc/>
        public async Task<VirtualKey> CreateVirtualKeyAsync(VirtualKey virtualKey)
        {
            // Generate a unique key value if one wasn't provided
            if (string.IsNullOrEmpty(virtualKey.KeyHash))
            {
                virtualKey.KeyHash = $"vk_{Guid.NewGuid().ToString("N").Substring(0, 16)}";
            }
            
            // Set creation date if not provided
            if (virtualKey.CreatedAt == default)
            {
                virtualKey.CreatedAt = DateTime.UtcNow;
            }
            
            // Set update date
            virtualKey.UpdatedAt = DateTime.UtcNow;
            
            // Set initial spend to 0 if not provided
            if (virtualKey.CurrentSpend == default)
            {
                virtualKey.CurrentSpend = 0;
            }
            
            // Set initial budget start date if not provided
            if (virtualKey.BudgetStartDate == default)
            {
                virtualKey.BudgetStartDate = DateTime.UtcNow;
            }
            
            _context.VirtualKeys.Add(virtualKey);
            await _context.SaveChangesAsync();
            
            return virtualKey;
        }
        
        /// <inheritdoc/>
        public async Task DeleteVirtualKeyAsync(int id)
        {
            var virtualKey = await _context.VirtualKeys.FindAsync(id);
            if (virtualKey != null)
            {
                _context.VirtualKeys.Remove(virtualKey);
                await _context.SaveChangesAsync();
            }
        }
        
        /// <inheritdoc/>
        public async Task<List<VirtualKey>> GetAllVirtualKeysAsync()
        {
            return await _context.VirtualKeys.ToListAsync();
        }
        
        /// <inheritdoc/>
        public async Task<VirtualKey?> GetVirtualKeyByIdAsync(int id)
        {
            return await _context.VirtualKeys.FindAsync(id);
        }
        
        /// <inheritdoc/>
        public async Task<VirtualKey?> GetVirtualKeyByKeyValueAsync(string keyValue)
        {
            return await _context.VirtualKeys
                .FirstOrDefaultAsync(k => k.KeyHash == keyValue);
        }
        
        /// <inheritdoc/>
        public async Task ResetSpendAsync(int id)
        {
            var virtualKey = await _context.VirtualKeys.FindAsync(id);
            if (virtualKey != null)
            {
                // Create a record in the spend history
                _context.VirtualKeySpendHistory.Add(new VirtualKeySpendHistory
                {
                    VirtualKeyId = virtualKey.Id,
                    Amount = virtualKey.CurrentSpend,
                    Date = DateTime.UtcNow
                });
                
                // Reset the current spend
                virtualKey.CurrentSpend = 0;
                virtualKey.BudgetStartDate = DateTime.UtcNow;
                virtualKey.UpdatedAt = DateTime.UtcNow;
                
                _context.VirtualKeys.Update(virtualKey);
                await _context.SaveChangesAsync();
            }
        }
        
        /// <inheritdoc/>
        public async Task<VirtualKey> UpdateVirtualKeyAsync(VirtualKey virtualKey)
        {
            virtualKey.UpdatedAt = DateTime.UtcNow;
            _context.VirtualKeys.Update(virtualKey);
            await _context.SaveChangesAsync();
            
            return virtualKey;
        }
        
        /// <inheritdoc/>
        public async Task UpdateSpendAsync(int id, decimal additionalSpend)
        {
            var virtualKey = await _context.VirtualKeys.FindAsync(id);
            if (virtualKey != null)
            {
                virtualKey.CurrentSpend += additionalSpend;
                virtualKey.UpdatedAt = DateTime.UtcNow;
                
                _context.VirtualKeys.Update(virtualKey);
                await _context.SaveChangesAsync();
            }
        }
        
        /// <inheritdoc/>
        public async Task<bool> ValidateVirtualKeyAsync(string keyValue)
        {
            var virtualKey = await _context.VirtualKeys
                .FirstOrDefaultAsync(k => k.KeyHash == keyValue);
                
            if (virtualKey == null)
            {
                return false; // Key doesn't exist
            }
            
            if (!virtualKey.IsEnabled)
            {
                return false; // Key is disabled
            }
            
            if (virtualKey.ExpiresAt.HasValue && virtualKey.ExpiresAt.Value < DateTime.UtcNow)
            {
                return false; // Key is expired
            }
            
            if (virtualKey.MaxBudget.HasValue && virtualKey.MaxBudget.Value > 0 && virtualKey.CurrentSpend >= virtualKey.MaxBudget)
            {
                return false; // Budget exceeded
            }
            
            return true; // Key is valid
        }
    }
}
