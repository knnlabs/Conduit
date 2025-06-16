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
    /// Repository implementation for fallback model mappings using Entity Framework Core
    /// </summary>
    public class FallbackModelMappingRepository : IFallbackModelMappingRepository
    {
        private readonly IDbContextFactory<ConfigurationDbContext> _dbContextFactory;
        private readonly ILogger<FallbackModelMappingRepository> _logger;

        /// <summary>
        /// Creates a new instance of the repository
        /// </summary>
        /// <param name="dbContextFactory">The database context factory</param>
        /// <param name="logger">The logger</param>
        public FallbackModelMappingRepository(
            IDbContextFactory<ConfigurationDbContext> dbContextFactory,
            ILogger<FallbackModelMappingRepository> logger)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public async Task<FallbackModelMappingEntity?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                return await dbContext.FallbackModelMappings
                    .AsNoTracking()
                    .Include(m => m.FallbackConfiguration)
                    .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting fallback model mapping with ID {MappingId}", id);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<FallbackModelMappingEntity?> GetBySourceModelAsync(
            Guid fallbackConfigId,
            string sourceModelName,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(sourceModelName))
            {
                throw new ArgumentException("Source model name cannot be null or empty", nameof(sourceModelName));
            }

            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                return await dbContext.FallbackModelMappings
                    .AsNoTracking()
                    .Include(m => m.FallbackConfiguration)
                    .FirstOrDefaultAsync(m =>
                        m.FallbackConfigurationId == fallbackConfigId &&
                        m.SourceModelName == sourceModelName,
                        cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting fallback model mapping for source model {SourceModel} in config {ConfigId}",
                    S(sourceModelName), fallbackConfigId);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<List<FallbackModelMappingEntity>> GetByFallbackConfigIdAsync(
            Guid fallbackConfigId,
            CancellationToken cancellationToken = default)
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
                _logger.LogError(ex, "Error getting fallback model mappings for config {ConfigId}", fallbackConfigId);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<List<FallbackModelMappingEntity>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                return await dbContext.FallbackModelMappings
                    .AsNoTracking()
                    .Include(m => m.FallbackConfiguration)
                    .OrderBy(m => m.FallbackConfiguration != null ? m.FallbackConfiguration.Name : string.Empty)
                    .ThenBy(m => m.SourceModelName)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all fallback model mappings");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<int> CreateAsync(FallbackModelMappingEntity fallbackModelMapping, CancellationToken cancellationToken = default)
        {
            if (fallbackModelMapping == null)
            {
                throw new ArgumentNullException(nameof(fallbackModelMapping));
            }

            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

                // Set timestamps
                fallbackModelMapping.CreatedAt = DateTime.UtcNow;
                fallbackModelMapping.UpdatedAt = DateTime.UtcNow;

                // Check if the mapping already exists
                var existingMapping = await dbContext.FallbackModelMappings
                    .FirstOrDefaultAsync(m =>
                        m.FallbackConfigurationId == fallbackModelMapping.FallbackConfigurationId &&
                        m.SourceModelName == fallbackModelMapping.SourceModelName,
                        cancellationToken);

                if (existingMapping != null)
                {
                    throw new InvalidOperationException(
                        $"A mapping for source model '{fallbackModelMapping.SourceModelName}' " +
                        $"already exists in fallback configuration ID {fallbackModelMapping.FallbackConfigurationId}");
                }

                // Verify that the fallback configuration exists
                var configExists = await dbContext.FallbackConfigurations
                    .AnyAsync(f => f.Id == fallbackModelMapping.FallbackConfigurationId, cancellationToken);

                if (!configExists)
                {
                    throw new InvalidOperationException(
                        $"Fallback configuration with ID {fallbackModelMapping.FallbackConfigurationId} does not exist");
                }

                dbContext.FallbackModelMappings.Add(fallbackModelMapping);
                await dbContext.SaveChangesAsync(cancellationToken);
                return fallbackModelMapping.Id;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error creating fallback model mapping for source model '{SourceModel}'",
                    S(fallbackModelMapping.SourceModelName));
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating fallback model mapping for source model '{SourceModel}'",
                    S(fallbackModelMapping.SourceModelName));
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> UpdateAsync(FallbackModelMappingEntity fallbackModelMapping, CancellationToken cancellationToken = default)
        {
            if (fallbackModelMapping == null)
            {
                throw new ArgumentNullException(nameof(fallbackModelMapping));
            }

            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

                // Set updated timestamp
                fallbackModelMapping.UpdatedAt = DateTime.UtcNow;

                // Check if we're changing the source model name and if that would create a duplicate
                var existingMapping = await dbContext.FallbackModelMappings
                    .FirstOrDefaultAsync(m =>
                        m.Id != fallbackModelMapping.Id &&
                        m.FallbackConfigurationId == fallbackModelMapping.FallbackConfigurationId &&
                        m.SourceModelName == fallbackModelMapping.SourceModelName,
                        cancellationToken);

                if (existingMapping != null)
                {
                    throw new InvalidOperationException(
                        $"Another mapping for source model '{fallbackModelMapping.SourceModelName}' " +
                        $"already exists in fallback configuration ID {fallbackModelMapping.FallbackConfigurationId}");
                }

                dbContext.FallbackModelMappings.Update(fallbackModelMapping);
                int rowsAffected = await dbContext.SaveChangesAsync(cancellationToken);
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating fallback model mapping with ID {MappingId}",
                    fallbackModelMapping.Id);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                var fallbackModelMapping = await dbContext.FallbackModelMappings.FindAsync(new object[] { id }, cancellationToken);

                if (fallbackModelMapping == null)
                {
                    return false;
                }

                dbContext.FallbackModelMappings.Remove(fallbackModelMapping);
                int rowsAffected = await dbContext.SaveChangesAsync(cancellationToken);
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting fallback model mapping with ID {MappingId}", id);
                throw;
            }
        }
    }
}
