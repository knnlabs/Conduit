using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Utilities;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using ConduitLLM.Configuration.Interfaces;
namespace ConduitLLM.Configuration.Repositories
{
    /// <summary>
    /// Repository implementation for providers using Entity Framework Core
    /// </summary>
    public class ProviderRepository : IProviderRepository
    {
        private readonly IDbContextFactory<ConduitDbContext> _dbContextFactory;
        private readonly ILogger<ProviderRepository> _logger;

        /// <summary>
        /// Creates a new instance of the repository
        /// </summary>
        /// <param name="dbContextFactory">The database context factory</param>
        /// <param name="logger">The logger</param>
        public ProviderRepository(
            IDbContextFactory<ConduitDbContext> dbContextFactory,
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
        [Obsolete("Use GetPaginatedAsync instead. This method loads all records into memory and will be removed in a future version.")]
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
        public async Task<(List<Provider> Items, int TotalCount)> GetPaginatedAsync(
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            if (pageNumber < 1)
            {
                throw new ArgumentException("Page number must be greater than or equal to 1", nameof(pageNumber));
            }

            if (pageSize < 1)
            {
                throw new ArgumentException("Page size must be greater than or equal to 1", nameof(pageSize));
            }

            const int maxPageSize = 100;
            if (pageSize > maxPageSize)
            {
                _logger.LogWarning("Requested page size {RequestedPageSize} exceeds maximum allowed {MaxPageSize}, limiting to maximum",
                    LogSanitizer.SanitizeObject(pageSize), LogSanitizer.SanitizeObject(maxPageSize));
                pageSize = maxPageSize;
            }

            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

                var query = dbContext.Providers
                    .Include(pc => pc.ProviderKeyCredentials)
                    .AsNoTracking();

                var totalCount = await query.CountAsync(cancellationToken);

                var items = await query
                    .OrderBy(pc => pc.ProviderType)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync(cancellationToken);

                return (items, totalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting paginated providers for page {PageNumber}, size {PageSize}",
                    LogSanitizer.SanitizeObject(pageNumber), LogSanitizer.SanitizeObject(pageSize));
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<Dictionary<int, string>> GetProviderNameMapAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                return await dbContext.Providers
                    .AsNoTracking()
                    .ToDictionaryAsync(p => p.Id, p => p.ProviderName ?? p.ProviderType.ToString(), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting provider name map");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<int> CountAsync(bool? enabledOnly = null, CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                var query = dbContext.Providers.AsNoTracking();

                if (enabledOnly.HasValue)
                {
                    query = query.Where(p => p.IsEnabled == enabledOnly.Value);
                }

                return await query.CountAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error counting providers (enabledOnly: {EnabledOnly})", enabledOnly);
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
