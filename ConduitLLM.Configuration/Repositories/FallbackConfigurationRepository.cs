using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Data;
using ConduitLLM.Configuration.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static ConduitLLM.Configuration.Utilities.LogSanitizer;

namespace ConduitLLM.Configuration.Repositories
{
    /// <summary>
    /// Repository implementation for fallback configurations using Entity Framework Core
    /// </summary>
    public class FallbackConfigurationRepository : IFallbackConfigurationRepository
    {
        private readonly IDbContextFactory<ConfigurationDbContext> _dbContextFactory;
        private readonly ILogger<FallbackConfigurationRepository> _logger;

        /// <summary>
        /// Creates a new instance of the repository
        /// </summary>
        /// <param name="dbContextFactory">The database context factory</param>
        /// <param name="logger">The logger</param>
        public FallbackConfigurationRepository(
            IDbContextFactory<ConfigurationDbContext> dbContextFactory,
            ILogger<FallbackConfigurationRepository> logger)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public async Task<FallbackConfigurationEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                return await dbContext.FallbackConfigurations
                    .AsNoTracking()
                    .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting fallback configuration with ID {ConfigId}", id);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<FallbackConfigurationEntity?> GetActiveConfigAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                return await dbContext.FallbackConfigurations
                    .AsNoTracking()
                    .FirstOrDefaultAsync(f => f.IsActive, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active fallback configuration");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<List<FallbackConfigurationEntity>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                return await dbContext.FallbackConfigurations
                    .AsNoTracking()
                    .OrderByDescending(f => f.IsActive)
                    .ThenByDescending(f => f.UpdatedAt)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all fallback configurations");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<Guid> CreateAsync(FallbackConfigurationEntity fallbackConfig, CancellationToken cancellationToken = default)
        {
            if (fallbackConfig == null)
            {
                throw new ArgumentNullException(nameof(fallbackConfig));
            }

            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

                // Set timestamps
                fallbackConfig.CreatedAt = DateTime.UtcNow;
                fallbackConfig.UpdatedAt = DateTime.UtcNow;

                if (fallbackConfig.IsActive)
                {
                    // Deactivate all other configs
                    var activeConfigs = await dbContext.FallbackConfigurations
                        .Where(f => f.IsActive)
                        .ToListAsync(cancellationToken);

                    foreach (var config in activeConfigs)
                    {
                        config.IsActive = false;
                    }
                }

                dbContext.FallbackConfigurations.Add(fallbackConfig);
                await dbContext.SaveChangesAsync(cancellationToken);
                return fallbackConfig.Id;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error creating fallback configuration '{ConfigName}'",
                    fallbackConfig.Name.Replace(Environment.NewLine, ""));
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating fallback configuration '{ConfigName}'",
                    fallbackConfig.Name.Replace(Environment.NewLine, ""));
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> UpdateAsync(FallbackConfigurationEntity fallbackConfig, CancellationToken cancellationToken = default)
        {
            if (fallbackConfig == null)
            {
                throw new ArgumentNullException(nameof(fallbackConfig));
            }

            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

                // Set updated timestamp
                fallbackConfig.UpdatedAt = DateTime.UtcNow;

                if (fallbackConfig.IsActive)
                {
                    // Deactivate all other configs
                    var activeConfigs = await dbContext.FallbackConfigurations
                        .Where(f => f.IsActive && f.Id != fallbackConfig.Id)
                        .ToListAsync(cancellationToken);

                    foreach (var config in activeConfigs)
                    {
                        config.IsActive = false;
                    }
                }

                dbContext.FallbackConfigurations.Update(fallbackConfig);
                int rowsAffected = await dbContext.SaveChangesAsync(cancellationToken);
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating fallback configuration with ID {ConfigId}",
                    fallbackConfig.Id);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> ActivateAsync(Guid id, CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

                var fallbackConfig = await dbContext.FallbackConfigurations.FindAsync(new object[] { id }, cancellationToken);
                if (fallbackConfig == null)
                {
                    return false;
                }

                // Deactivate all configs
                var configs = await dbContext.FallbackConfigurations.ToListAsync(cancellationToken);
                foreach (var config in configs)
                {
                    config.IsActive = (config.Id == id);
                    config.UpdatedAt = DateTime.UtcNow;
                }

                int rowsAffected = await dbContext.SaveChangesAsync(cancellationToken);
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error activating fallback configuration with ID {ConfigId}", id);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                var fallbackConfig = await dbContext.FallbackConfigurations.FindAsync(new object[] { id }, cancellationToken);

                if (fallbackConfig == null)
                {
                    return false;
                }

                if (fallbackConfig.IsActive)
                {
                    _logger.LogWarning("Attempting to delete active fallback configuration {ConfigId}", id);
                    // You might want to prevent this or activate another config
                }

                // Check for related mappings
                var mappings = await dbContext.FallbackModelMappings
                    .Where(m => m.FallbackConfigurationId == id)
                    .ToListAsync(cancellationToken);

                if (mappings.Any())
                {
                    // Remove related mappings if there are any
                    dbContext.FallbackModelMappings.RemoveRange(mappings);
                }

                dbContext.FallbackConfigurations.Remove(fallbackConfig);
                int rowsAffected = await dbContext.SaveChangesAsync(cancellationToken);
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting fallback configuration with ID {ConfigId}", id);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<List<FallbackModelMappingEntity>> GetMappingsAsync(Guid fallbackConfigId, CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                return await dbContext.FallbackModelMappings
                    .AsNoTracking()
                    .Where(m => m.FallbackConfigurationId == fallbackConfigId)
                    .OrderBy(m => m.SourceModelName)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting mappings for fallback configuration {ConfigId}", fallbackConfigId);
                throw;
            }
        }
    }
}
