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
        private readonly IConfigurationDbContext _context;
        private readonly ILogger<ProviderKeyCredentialRepository> _logger;

        public ProviderKeyCredentialRepository(
            IConfigurationDbContext context,
            ILogger<ProviderKeyCredentialRepository> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<List<ProviderKeyCredential>> GetByProviderIdAsync(int providerCredentialId)
        {
            return await _context.ProviderKeyCredentials
                .Where(k => k.ProviderCredentialId == providerCredentialId)
                .OrderByDescending(k => k.IsPrimary)
                .ThenBy(k => k.ProviderAccountGroup)
                .ToListAsync();
        }

        public async Task<ProviderKeyCredential?> GetByIdAsync(int id)
        {
            return await _context.ProviderKeyCredentials
                .Include(k => k.ProviderCredential)
                .FirstOrDefaultAsync(k => k.Id == id);
        }

        public async Task<ProviderKeyCredential?> GetPrimaryKeyAsync(int providerCredentialId)
        {
            return await _context.ProviderKeyCredentials
                .FirstOrDefaultAsync(k => k.ProviderCredentialId == providerCredentialId 
                    && k.IsPrimary 
                    && k.IsEnabled);
        }

        public async Task<List<ProviderKeyCredential>> GetEnabledKeysByProviderIdAsync(int providerCredentialId)
        {
            return await _context.ProviderKeyCredentials
                .Where(k => k.ProviderCredentialId == providerCredentialId && k.IsEnabled)
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
                keyCredential.Id, keyCredential.ProviderCredentialId);

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
                keyCredential.Id, keyCredential.ProviderCredentialId);

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
                id, keyCredential.ProviderCredentialId);

            return true;
        }

        public async Task<bool> SetPrimaryKeyAsync(int providerCredentialId, int keyId)
        {
            using var transaction = await (_context as DbContext)!.Database.BeginTransactionAsync();
            try
            {
                // First, unset any existing primary keys
                var existingPrimaryKeys = await _context.ProviderKeyCredentials
                    .Where(k => k.ProviderCredentialId == providerCredentialId && k.IsPrimary)
                    .ToListAsync();

                foreach (var key in existingPrimaryKeys)
                {
                    key.IsPrimary = false;
                    key.UpdatedAt = DateTime.UtcNow;
                }

                // Set the new primary key
                var newPrimaryKey = await _context.ProviderKeyCredentials
                    .FirstOrDefaultAsync(k => k.Id == keyId && k.ProviderCredentialId == providerCredentialId);

                if (newPrimaryKey == null)
                    return false;

                newPrimaryKey.IsPrimary = true;
                newPrimaryKey.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Set key {KeyId} as primary for provider {ProviderId}", 
                    keyId, providerCredentialId);

                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to set primary key {KeyId} for provider {ProviderId}", 
                    keyId, providerCredentialId);
                throw;
            }
        }

        public async Task<bool> HasKeyCredentialsAsync(int providerCredentialId)
        {
            return await _context.ProviderKeyCredentials
                .AnyAsync(k => k.ProviderCredentialId == providerCredentialId);
        }

        public async Task<int> CountByProviderIdAsync(int providerCredentialId)
        {
            return await _context.ProviderKeyCredentials
                .CountAsync(k => k.ProviderCredentialId == providerCredentialId);
        }
    }
}