using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Data;
using ConduitLLM.Configuration.Entities;

using Microsoft.EntityFrameworkCore;

using ConduitLLM.Configuration.Interfaces;
namespace ConduitLLM.Configuration.Repositories
{
    /// <summary>
    /// Repository implementation for audio cost configurations.
    /// </summary>
    public class AudioCostRepository : IAudioCostRepository
    {
        private readonly ConduitDbContext _context;

        /// <summary>
        /// Initializes a new instance of the <see cref="AudioCostRepository"/> class.
        /// </summary>
        public AudioCostRepository(ConduitDbContext context)
        {
            _context = context;
        }

        /// <inheritdoc/>
        public async Task<List<AudioCost>> GetAllAsync()
        {
            return await _context.AudioCosts
                .OrderBy(c => c.ProviderId)
                .ThenBy(c => c.OperationType)
                .ThenBy(c => c.Model)
                .ToListAsync();
        }

        /// <inheritdoc/>
        public async Task<AudioCost?> GetByIdAsync(int id)
        {
            return await _context.AudioCosts.FindAsync(id);
        }

        /// <inheritdoc/>
        public async Task<List<AudioCost>> GetByProviderAsync(int providerId)
        {
            return await _context.AudioCosts
                .Where(c => c.ProviderId == providerId)
                .OrderBy(c => c.OperationType)
                .ThenBy(c => c.Model)
                .ToListAsync();
        }

        /// <inheritdoc/>
        public async Task<AudioCost?> GetCurrentCostAsync(int providerId, string operationType, string? model = null)
        {
            var now = DateTime.UtcNow;
            var query = _context.AudioCosts
                .Where(c => c.ProviderId == providerId &&
                           c.OperationType.ToLower() == operationType.ToLower() &&
                           c.IsActive &&
                           c.EffectiveFrom <= now &&
                           (c.EffectiveTo == null || c.EffectiveTo > now));

            if (!string.IsNullOrEmpty(model))
            {
                query = query.Where(c => c.Model == model);
            }
            else
            {
                query = query.Where(c => c.Model == null);
            }

            return await query.FirstOrDefaultAsync();
        }

        /// <inheritdoc/>
        public async Task<List<AudioCost>> GetEffectiveAtDateAsync(DateTime date)
        {
            return await _context.AudioCosts
                .Where(c => c.IsActive &&
                           c.EffectiveFrom <= date &&
                           (c.EffectiveTo == null || c.EffectiveTo > date))
                .OrderBy(c => c.ProviderId)
                .ThenBy(c => c.OperationType)
                .ThenBy(c => c.Model)
                .ToListAsync();
        }

        /// <inheritdoc/>
        public async Task<AudioCost> CreateAsync(AudioCost cost)
        {
            cost.CreatedAt = DateTime.UtcNow;
            cost.UpdatedAt = DateTime.UtcNow;

            // Deactivate previous costs if this is replacing an existing one
            if (cost.IsActive)
            {
                await DeactivatePreviousCostsAsync(cost.ProviderId, cost.OperationType, cost.Model);
            }

            _context.AudioCosts.Add(cost);
            await _context.SaveChangesAsync();

            return cost;
        }

        /// <inheritdoc/>
        public async Task<AudioCost> UpdateAsync(AudioCost cost)
        {
            cost.UpdatedAt = DateTime.UtcNow;

            _context.AudioCosts.Update(cost);
            await _context.SaveChangesAsync();

            return cost;
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteAsync(int id)
        {
            var cost = await _context.AudioCosts.FindAsync(id);
            if (cost == null)
                return false;

            _context.AudioCosts.Remove(cost);
            await _context.SaveChangesAsync();

            return true;
        }

        /// <inheritdoc/>
        public async Task DeactivatePreviousCostsAsync(int providerId, string operationType, string? model = null)
        {
            var costs = await _context.AudioCosts
                .Where(c => c.ProviderId == providerId &&
                           c.OperationType.ToLower() == operationType.ToLower() &&
                           c.Model == model &&
                           c.IsActive &&
                           c.EffectiveTo == null)
                .ToListAsync();

            foreach (var cost in costs)
            {
                cost.EffectiveTo = DateTime.UtcNow;
                cost.IsActive = false;
                cost.UpdatedAt = DateTime.UtcNow;
            }

            if (costs.Any())
            {
                await _context.SaveChangesAsync();
            }
        }

        /// <inheritdoc/>
        public async Task<List<AudioCost>> GetCostHistoryAsync(int providerId, string operationType, string? model = null)
        {
            var query = _context.AudioCosts
                .Where(c => c.ProviderId == providerId &&
                           c.OperationType.ToLower() == operationType.ToLower());

            if (!string.IsNullOrEmpty(model))
            {
                query = query.Where(c => c.Model == model);
            }
            else
            {
                query = query.Where(c => c.Model == null);
            }

            return await query
                .OrderByDescending(c => c.EffectiveFrom)
                .ToListAsync();
        }
    }
}
