using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;

using Microsoft.EntityFrameworkCore;

namespace ConduitLLM.Configuration.Services
{
    /// <summary>
    /// Service for managing virtual keys
    /// </summary>
    public class VirtualKeyService : IVirtualKeyService
    {
        private readonly ConduitDbContext _context;
        private readonly IVirtualKeyGroupRepository _groupRepository;

        /// <summary>
        /// Initializes a new instance of the VirtualKeyService
        /// </summary>
        /// <param name="context">Database context</param>
        /// <param name="groupRepository">Virtual key group repository</param>
        public VirtualKeyService(ConduitDbContext context, IVirtualKeyGroupRepository groupRepository)
        {
            _context = context;
            _groupRepository = groupRepository;
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

            // If no group is assigned, create a new single-key group
            if (virtualKey.VirtualKeyGroupId == 0)
            {
                var group = new VirtualKeyGroup
                {
                    GroupName = virtualKey.KeyName,
                    Balance = 0, // Start with zero balance, user needs to add credits
                    LifetimeCreditsAdded = 0,
                    LifetimeSpent = 0
                };
                
                virtualKey.VirtualKeyGroupId = await _groupRepository.CreateAsync(group);
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
            // Budget tracking is now at the group level
            // This method is deprecated but kept for interface compatibility
            await Task.CompletedTask;
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
            // Spending is now tracked at the group level
            // This method is deprecated but kept for interface compatibility
            var group = await _groupRepository.GetByKeyIdAsync(id);
            if (group != null)
            {
                await _groupRepository.AdjustBalanceAsync(group.Id, -additionalSpend);
            }
        }

        /// <inheritdoc/>
        public async Task<bool> ValidateVirtualKeyAsync(string keyValue)
        {
            var virtualKey = await _context.VirtualKeys
                .Include(k => k.VirtualKeyGroup)
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

            // Check group balance
            if (virtualKey.VirtualKeyGroup != null && virtualKey.VirtualKeyGroup.Balance <= 0)
            {
                return false; // No balance available
            }

            return true; // Key is valid
        }
    }
}
