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
    /// Repository implementation for providers using Entity Framework Core
    /// </summary>
    public class ProviderRepository : IProviderRepository
    {
        private readonly IDbContextFactory<ConfigurationDbContext> _dbContextFactory;
        private readonly ILogger<ProviderRepository> _logger;

        /// <summary>
        /// Creates a new instance of the repository
        /// </summary>
        /// <param name="dbContextFactory">The database context factory</param>
        /// <param name="logger">The logger</param>
        public ProviderRepository(
            IDbContextFactory<ConfigurationDbContext> dbContextFactory,
            ILogger<ProviderRepository> logger)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public async Task<Provider?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                return await dbContext.Providers
                    .Include(pc => pc.ProviderKeyCredentials)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(pc => pc.Id == id, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting provider with ID {ProviderId}", LogSanitizer.SanitizeObject(id));
                throw;
            }
        }


        /// <inheritdoc/>
        public async Task<List<Provider>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                return await dbContext.Providers
                    .Include(pc => pc.ProviderKeyCredentials)
                    .AsNoTracking()
                    .OrderBy(pc => pc.ProviderType)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all providers");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<int> CreateAsync(Provider provider, CancellationToken cancellationToken = default)
        {
            if (provider == null)
            {
                throw new ArgumentNullException(nameof(provider));
            }

            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

                // Set created/updated timestamps
                if (provider.CreatedAt == default)
                {
                    provider.CreatedAt = DateTime.UtcNow;
                }

                provider.UpdatedAt = DateTime.UtcNow;

                dbContext.Providers.Add(provider);
                await dbContext.SaveChangesAsync(cancellationToken);
                return provider.Id;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error creating provider for provider '{ProviderType}'",
                    LogSanitizer.SanitizeObject(provider.ProviderType));
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating provider for provider '{ProviderType}'",
                    LogSanitizer.SanitizeObject(provider.ProviderType));
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> UpdateAsync(Provider provider, CancellationToken cancellationToken = default)
        {
            if (provider == null)
            {
                throw new ArgumentNullException(nameof(provider));
            }

            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

                // Ensure the entity is tracked
                dbContext.Providers.Update(provider);

                // Set the updated timestamp
                provider.UpdatedAt = DateTime.UtcNow;

                // Save changes
                int rowsAffected = await dbContext.SaveChangesAsync(cancellationToken);
                return rowsAffected > 0;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError(ex, "Concurrency error updating provider with ID {ProviderId}",
                    LogSanitizer.SanitizeObject(provider.Id));

                // Additional handling for concurrency issues could be implemented here
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating provider with ID {ProviderId}",
                    LogSanitizer.SanitizeObject(provider.Id));
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                var provider = await dbContext.Providers.FindAsync(new object[] { id }, cancellationToken);

                if (provider == null)
                {
                    return false;
                }

                dbContext.Providers.Remove(provider);
                int rowsAffected = await dbContext.SaveChangesAsync(cancellationToken);
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting provider with ID {ProviderId}", LogSanitizer.SanitizeObject(id));
                throw;
            }
        }
    }
}
