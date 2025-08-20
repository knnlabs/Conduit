using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Data;
using ConduitLLM.Configuration.Interfaces;
using ModelProviderMappingEntity = ConduitLLM.Configuration.Entities.ModelProviderMapping;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static ConduitLLM.Configuration.Utilities.LogSanitizer;

namespace ConduitLLM.Configuration.Repositories
{
    /// <summary>
    /// Repository implementation for model provider mappings using Entity Framework Core.
    /// </summary>
    public class ModelProviderMappingRepository : IModelProviderMappingRepository
    {
        private readonly IDbContextFactory<ConduitDbContext> _dbContextFactory;
        private readonly ILogger<ModelProviderMappingRepository> _logger;

        /// <summary>
        /// Creates a new instance of the repository
        /// </summary>
        /// <param name="dbContextFactory">The database context factory</param>
        /// <param name="logger">The logger</param>
        public ModelProviderMappingRepository(
            IDbContextFactory<ConduitDbContext> dbContextFactory,
            ILogger<ModelProviderMappingRepository> logger)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public async Task<ModelProviderMappingEntity?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                return await dbContext.ModelProviderMappings
                    .Include(m => m.Provider)
                    .Include(m => m.Model)
                        .ThenInclude(m => m!.Capabilities)
                    .Include(m => m.Model)
                        .ThenInclude(m => m!.Series)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting model provider mapping with ID {MappingId}", id);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<ModelProviderMappingEntity?> GetByModelNameAsync(
            string modelName,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(modelName))
            {
                throw new ArgumentException("Model name cannot be null or empty", nameof(modelName));
            }

            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                return await dbContext.ModelProviderMappings
                    .Include(m => m.Provider)
                    .Include(m => m.Model)
                        .ThenInclude(m => m!.Capabilities)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(m => m.ModelAlias == modelName, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting model provider mapping for model {ModelName}", modelName.Replace(Environment.NewLine, ""));
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<List<ModelProviderMappingEntity>> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                return await dbContext.ModelProviderMappings
                    .Include(m => m.Provider)
                    .AsNoTracking()
                    .OrderBy(m => m.ModelAlias)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all model provider mappings");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<List<ModelProviderMappingEntity>> GetByProviderAsync(
            ProviderType providerType,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

                var credential = await dbContext.Providers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(pc => pc.ProviderType == providerType, cancellationToken);

                if (credential == null)
                {
                    return new List<ModelProviderMappingEntity>();
                }

                // Then find mappings with this credential ID
                return await dbContext.ModelProviderMappings
                    .Include(m => m.Provider)
                    .AsNoTracking()
                    .Where(m => m.ProviderId == credential.Id)
                    .OrderBy(m => m.ModelAlias)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting model provider mappings for provider type {ProviderType}", providerType);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<int> CreateAsync(
            ModelProviderMappingEntity modelProviderMapping,
            CancellationToken cancellationToken = default)
        {
            if (modelProviderMapping == null)
            {
                throw new ArgumentNullException(nameof(modelProviderMapping));
            }

            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

                // Set timestamps
                modelProviderMapping.CreatedAt = DateTime.UtcNow;
                modelProviderMapping.UpdatedAt = DateTime.UtcNow;

                dbContext.ModelProviderMappings.Add(modelProviderMapping);
                await dbContext.SaveChangesAsync(cancellationToken);

                return modelProviderMapping.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating model provider mapping for {ModelAlias}", modelProviderMapping.ModelAlias.Replace(Environment.NewLine, ""));
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> UpdateAsync(
            ModelProviderMappingEntity modelProviderMapping,
            CancellationToken cancellationToken = default)
        {
            if (modelProviderMapping == null)
            {
                throw new ArgumentNullException(nameof(modelProviderMapping));
            }

            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

                // Get existing entity to ensure it exists
                var existingEntity = await dbContext.ModelProviderMappings
                    .FirstOrDefaultAsync(m => m.Id == modelProviderMapping.Id, cancellationToken);

                if (existingEntity == null)
                {
                    _logger.LogWarning("Cannot update non-existent model provider mapping with ID {MappingId}", modelProviderMapping.Id);
                    return false;
                }

                // Update fields
                existingEntity.ModelAlias = modelProviderMapping.ModelAlias;
                existingEntity.ProviderModelId = modelProviderMapping.ProviderModelId;
                existingEntity.ProviderId = modelProviderMapping.ProviderId;
                existingEntity.IsEnabled = modelProviderMapping.IsEnabled;
                existingEntity.ModelId = modelProviderMapping.ModelId;
                existingEntity.MaxContextTokensOverride = modelProviderMapping.MaxContextTokensOverride;
                existingEntity.ProviderVariation = modelProviderMapping.ProviderVariation;
                existingEntity.QualityScore = modelProviderMapping.QualityScore;
                existingEntity.IsDefault = modelProviderMapping.IsDefault;
                existingEntity.DefaultCapabilityType = modelProviderMapping.DefaultCapabilityType;
                
                existingEntity.UpdatedAt = DateTime.UtcNow;

                _logger.LogInformation(
                    "Updating model mapping {ModelAlias} with ModelId={ModelId}",
                    existingEntity.ModelAlias,
                    existingEntity.ModelId);

                await dbContext.SaveChangesAsync(cancellationToken);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating model provider mapping with ID {MappingId}", modelProviderMapping.Id);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

                var entity = await dbContext.ModelProviderMappings
                    .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

                if (entity == null)
                {
                    _logger.LogWarning("Cannot delete non-existent model provider mapping with ID {MappingId}", id);
                    return false;
                }

                dbContext.ModelProviderMappings.Remove(entity);
                await dbContext.SaveChangesAsync(cancellationToken);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting model provider mapping with ID {MappingId}", id);
                throw;
            }
        }
    }
}
