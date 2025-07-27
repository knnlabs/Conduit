using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Data;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Utilities;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Configuration.Repositories
{
    /// <summary>
    /// Repository implementation for provider credentials using Entity Framework Core
    /// </summary>
    public class ProviderCredentialRepository : IProviderCredentialRepository
    {
        private readonly IDbContextFactory<ConfigurationDbContext> _dbContextFactory;
        private readonly ILogger<ProviderCredentialRepository> _logger;

        /// <summary>
        /// Creates a new instance of the repository
        /// </summary>
        /// <param name="dbContextFactory">The database context factory</param>
        /// <param name="logger">The logger</param>
        public ProviderCredentialRepository(
            IDbContextFactory<ConfigurationDbContext> dbContextFactory,
            ILogger<ProviderCredentialRepository> logger)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public async Task<ProviderCredential?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                return await dbContext.ProviderCredentials
                    .Include(pc => pc.ProviderKeyCredentials)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(pc => pc.Id == id, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting provider credential with ID {CredentialId}", LogSanitizer.SanitizeObject(id));
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<ProviderCredential?> GetByProviderTypeAsync(ProviderType providerType, CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                return await dbContext.ProviderCredentials
                    .Include(pc => pc.ProviderKeyCredentials)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(pc => pc.ProviderType == providerType, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting provider credential for provider type {ProviderType}", providerType);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<List<ProviderCredential>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                return await dbContext.ProviderCredentials
                    .Include(pc => pc.ProviderKeyCredentials)
                    .AsNoTracking()
                    .OrderBy(pc => pc.ProviderType)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all provider credentials");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<int> CreateAsync(ProviderCredential providerCredential, CancellationToken cancellationToken = default)
        {
            if (providerCredential == null)
            {
                throw new ArgumentNullException(nameof(providerCredential));
            }

            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

                // Set created/updated timestamps
                if (providerCredential.CreatedAt == default)
                {
                    providerCredential.CreatedAt = DateTime.UtcNow;
                }

                providerCredential.UpdatedAt = DateTime.UtcNow;

                dbContext.ProviderCredentials.Add(providerCredential);
                await dbContext.SaveChangesAsync(cancellationToken);
                return providerCredential.Id;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error creating provider credential for provider '{ProviderType}'",
                    LogSanitizer.SanitizeObject(providerCredential.ProviderType));
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating provider credential for provider '{ProviderType}'",
                    LogSanitizer.SanitizeObject(providerCredential.ProviderType));
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> UpdateAsync(ProviderCredential providerCredential, CancellationToken cancellationToken = default)
        {
            if (providerCredential == null)
            {
                throw new ArgumentNullException(nameof(providerCredential));
            }

            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

                // Ensure the entity is tracked
                dbContext.ProviderCredentials.Update(providerCredential);

                // Set the updated timestamp
                providerCredential.UpdatedAt = DateTime.UtcNow;

                // Save changes
                int rowsAffected = await dbContext.SaveChangesAsync(cancellationToken);
                return rowsAffected > 0;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError(ex, "Concurrency error updating provider credential with ID {CredentialId}",
                    LogSanitizer.SanitizeObject(providerCredential.Id));

                // Additional handling for concurrency issues could be implemented here
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating provider credential with ID {CredentialId}",
                    LogSanitizer.SanitizeObject(providerCredential.Id));
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                var providerCredential = await dbContext.ProviderCredentials.FindAsync(new object[] { id }, cancellationToken);

                if (providerCredential == null)
                {
                    return false;
                }

                dbContext.ProviderCredentials.Remove(providerCredential);
                int rowsAffected = await dbContext.SaveChangesAsync(cancellationToken);
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting provider credential with ID {CredentialId}", LogSanitizer.SanitizeObject(id));
                throw;
            }
        }
    }
}
