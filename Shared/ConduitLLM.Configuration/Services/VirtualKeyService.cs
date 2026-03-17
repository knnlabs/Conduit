using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Configuration.Services
{
    /// <summary>
    /// Service for managing virtual keys
    /// </summary>
    public class VirtualKeyService : IVirtualKeyService
    {
        private readonly ConduitDbContext _context;
        private readonly IVirtualKeyGroupRepository _groupRepository;
        private readonly ILogger<VirtualKeyService> _logger;

        /// <summary>
        /// Initializes a new instance of the VirtualKeyService
        /// </summary>
        /// <param name="context">Database context</param>
        /// <param name="groupRepository">Virtual key group repository</param>
        /// <param name="logger">The logger</param>
        public VirtualKeyService(ConduitDbContext context, IVirtualKeyGroupRepository groupRepository, ILogger<VirtualKeyService> logger)
        {
            _context = context;
            _groupRepository = groupRepository;
            _logger = logger;
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
                _logger.LogDebug("Created new key group {GroupId} for virtual key {KeyName}", virtualKey.VirtualKeyGroupId, virtualKey.KeyName);
            }

            _context.VirtualKeys.Add(virtualKey);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created virtual key {KeyId} ({KeyName}) in group {GroupId}", virtualKey.Id, virtualKey.KeyName, virtualKey.VirtualKeyGroupId);
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
                _logger.LogInformation("Deleted virtual key {KeyId} ({KeyName})", id, virtualKey.KeyName);
            }
            else
            {
                _logger.LogWarning("Attempted to delete non-existent virtual key {KeyId}", id);
            }
        }

        /// <inheritdoc/>
        public async Task<List<VirtualKey>> GetAllVirtualKeysAsync()
        {
            return await _context.VirtualKeys.AsNoTracking().ToListAsync();
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
                .AsNoTracking()
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

            _logger.LogInformation("Updated virtual key {KeyId} ({KeyName})", virtualKey.Id, virtualKey.KeyName);
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
                _logger.LogDebug("Updated spend for key {KeyId} via group {GroupId}: {Amount:C}", id, group.Id, additionalSpend);
            }
            else
            {
                _logger.LogWarning("Cannot update spend for key {KeyId}: no associated group found", id);
            }
        }

        /// <inheritdoc/>
        public async Task<VirtualKey?> ValidateVirtualKeyForAuthenticationAsync(string keyValue, string? requestedModel = null)
        {
            var virtualKey = await _context.VirtualKeys
                .Include(k => k.VirtualKeyGroup)
                .AsNoTracking()
                .FirstOrDefaultAsync(k => k.KeyHash == keyValue);

            if (virtualKey == null)
            {
                _logger.LogDebug("Authentication validation failed: key not found");
                return null;
            }

            if (!virtualKey.IsEnabled)
            {
                _logger.LogDebug("Authentication validation failed: key {KeyId} is disabled", virtualKey.Id);
                return null;
            }

            if (virtualKey.ExpiresAt.HasValue && virtualKey.ExpiresAt.Value < DateTime.UtcNow)
            {
                _logger.LogDebug("Authentication validation failed: key {KeyId} expired at {ExpiresAt}", virtualKey.Id, virtualKey.ExpiresAt.Value);
                return null;
            }

            // For authentication, we don't check balance
            return virtualKey;
        }

        /// <inheritdoc/>
        public async Task<bool> ValidateVirtualKeyAsync(string keyValue)
        {
            var virtualKey = await _context.VirtualKeys
                .Include(k => k.VirtualKeyGroup)
                .AsNoTracking()
                .FirstOrDefaultAsync(k => k.KeyHash == keyValue);

            if (virtualKey == null)
            {
                _logger.LogDebug("Key validation failed: key not found");
                return false;
            }

            if (!virtualKey.IsEnabled)
            {
                _logger.LogDebug("Key validation failed: key {KeyId} is disabled", virtualKey.Id);
                return false;
            }

            if (virtualKey.ExpiresAt.HasValue && virtualKey.ExpiresAt.Value < DateTime.UtcNow)
            {
                _logger.LogDebug("Key validation failed: key {KeyId} expired at {ExpiresAt}", virtualKey.Id, virtualKey.ExpiresAt.Value);
                return false;
            }

            // Check group balance
            if (virtualKey.VirtualKeyGroup != null && virtualKey.VirtualKeyGroup.Balance <= 0)
            {
                _logger.LogDebug("Key validation failed: key {KeyId} group {GroupId} has insufficient balance", virtualKey.Id, virtualKey.VirtualKeyGroupId);
                return false;
            }

            return true;
        }
    }
}
