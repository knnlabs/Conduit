using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Data;
using ConduitLLM.Configuration.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Configuration.Repositories
{
    /// <summary>
    /// Repository implementation for ProviderKeyCredential operations
    /// </summary>
    public class ProviderKeyCredentialRepository : IProviderKeyCredentialRepository
    {
        private readonly ConfigurationDbContext _context;
        private readonly ILogger<ProviderKeyCredentialRepository> _logger;

        public ProviderKeyCredentialRepository(
            ConfigurationDbContext context,
            ILogger<ProviderKeyCredentialRepository> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<List<ProviderKeyCredential>> GetAllAsync()
        {
            return await _context.ProviderKeyCredentials
                .Include(k => k.Provider)
                .OrderBy(k => k.ProviderId)
                .ThenByDescending(k => k.IsPrimary)
                .ThenBy(k => k.ProviderAccountGroup)
                .ToListAsync();
        }

        public async Task<List<ProviderKeyCredential>> GetByProviderIdAsync(int ProviderId)
        {
            return await _context.ProviderKeyCredentials
                .Where(k => k.ProviderId == ProviderId)
                .OrderByDescending(k => k.IsPrimary)
                .ThenBy(k => k.ProviderAccountGroup)
                .ToListAsync();
        }

        public async Task<ProviderKeyCredential?> GetByIdAsync(int id)
        {
            return await _context.ProviderKeyCredentials
                .Include(k => k.Provider)
                .FirstOrDefaultAsync(k => k.Id == id);
        }

        public async Task<ProviderKeyCredential?> GetPrimaryKeyAsync(int ProviderId)
        {
            return await _context.ProviderKeyCredentials
                .FirstOrDefaultAsync(k => k.ProviderId == ProviderId 
                    && k.IsPrimary 
                    && k.IsEnabled);
        }

        public async Task<List<ProviderKeyCredential>> GetEnabledKeysByProviderIdAsync(int ProviderId)
        {
            return await _context.ProviderKeyCredentials
                .Where(k => k.ProviderId == ProviderId && k.IsEnabled)
                .OrderByDescending(k => k.IsPrimary)
                .ThenBy(k => k.ProviderAccountGroup)
                .ToListAsync();
        }

        public async Task<ProviderKeyCredential> CreateAsync(ProviderKeyCredential keyCredential)
        {
            if (keyCredential == null)
                throw new ArgumentNullException(nameof(keyCredential));

            keyCredential.CreatedAt = DateTime.UtcNow;
            keyCredential.UpdatedAt = DateTime.UtcNow;

            _context.ProviderKeyCredentials.Add(keyCredential);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created key credential {KeyId} for provider {ProviderId}", 
                keyCredential.Id, keyCredential.ProviderId);

            return keyCredential;
        }

        public async Task<bool> UpdateAsync(ProviderKeyCredential keyCredential)
        {
            if (keyCredential == null)
                throw new ArgumentNullException(nameof(keyCredential));

            var existingKey = await _context.ProviderKeyCredentials
                .FirstOrDefaultAsync(k => k.Id == keyCredential.Id);

            if (existingKey == null)
                return false;

            // Update properties
            existingKey.ProviderAccountGroup = keyCredential.ProviderAccountGroup;
            existingKey.ApiKey = keyCredential.ApiKey;
            existingKey.BaseUrl = keyCredential.BaseUrl;
            existingKey.IsPrimary = keyCredential.IsPrimary;
            existingKey.IsEnabled = keyCredential.IsEnabled;
            existingKey.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Updated key credential {KeyId} for provider {ProviderId}", 
                keyCredential.Id, keyCredential.ProviderId);

            return true;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var keyCredential = await _context.ProviderKeyCredentials
                .FirstOrDefaultAsync(k => k.Id == id);

            if (keyCredential == null)
                return false;

            _context.ProviderKeyCredentials.Remove(keyCredential);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Deleted key credential {KeyId} for provider {ProviderId}", 
                id, keyCredential.ProviderId);

            return true;
        }

        public async Task<bool> SetPrimaryKeyAsync(int ProviderId, int keyId)
        {
            using var transaction = await (_context as DbContext)!.Database.BeginTransactionAsync();
            try
            {
                // First, unset any existing primary keys
                var existingPrimaryKeys = await _context.ProviderKeyCredentials
                    .Where(k => k.ProviderId == ProviderId && k.IsPrimary)
                    .ToListAsync();

                foreach (var key in existingPrimaryKeys)
                {
                    key.IsPrimary = false;
                    key.UpdatedAt = DateTime.UtcNow;
                }

                // Save changes to unset primary keys first to avoid constraint violation
                if (existingPrimaryKeys.Any())
                {
                    await _context.SaveChangesAsync();
                }

                // Set the new primary key
                var newPrimaryKey = await _context.ProviderKeyCredentials
                    .FirstOrDefaultAsync(k => k.Id == keyId && k.ProviderId == ProviderId);

                if (newPrimaryKey == null)
                    return false;

                newPrimaryKey.IsPrimary = true;
                newPrimaryKey.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Set key {KeyId} as primary for provider {ProviderId}", 
                    keyId, ProviderId);

                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to set primary key {KeyId} for provider {ProviderId}", 
                    keyId, ProviderId);
                throw;
            }
        }

        public async Task<bool> HasKeyCredentialsAsync(int ProviderId)
        {
            return await _context.ProviderKeyCredentials
                .AnyAsync(k => k.ProviderId == ProviderId);
        }

        public async Task<int> CountByProviderIdAsync(int ProviderId)
        {
            return await _context.ProviderKeyCredentials
                .CountAsync(k => k.ProviderId == ProviderId);
        }
    }
}