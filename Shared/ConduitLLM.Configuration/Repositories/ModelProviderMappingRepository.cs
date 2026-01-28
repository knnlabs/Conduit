using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Utilities;
using ModelProviderMappingEntity = ConduitLLM.Configuration.Entities.ModelProviderMapping;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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
                    .Include(m => m.ModelProviderTypeAssociation)
                        .ThenInclude(a => a.Model)
                            .ThenInclude(m => m.Series)
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
                    .Include(m => m.ModelProviderTypeAssociation)
                        .ThenInclude(a => a.Model)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(m => m.ModelAlias == modelName, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting model provider mapping for model {ModelName}", LoggingSanitizer.S(modelName));
                throw;
            }
        }

        /// <inheritdoc/>
        [Obsolete("Use GetPaginatedAsync instead. This method loads all records into memory and will be removed in a future version.")]
        public async Task<List<ModelProviderMappingEntity>> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                return await dbContext.ModelProviderMappings
                    .Include(m => m.Provider)
                    .Include(m => m.ModelProviderTypeAssociation)
                        .ThenInclude(a => a.Model)
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
        public async Task<(List<ModelProviderMappingEntity> Items, int TotalCount)> GetPaginatedAsync(
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
                    pageSize, maxPageSize);
                pageSize = maxPageSize;
            }

            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

                var query = dbContext.ModelProviderMappings
                    .Include(m => m.Provider)
                    .Include(m => m.ModelProviderTypeAssociation)
                        .ThenInclude(a => a.Model)
                    .AsNoTracking();

                var totalCount = await query.CountAsync(cancellationToken);

                var items = await query
                    .OrderBy(m => m.ModelAlias)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync(cancellationToken);

                return (items, totalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting paginated model provider mappings for page {PageNumber}, size {PageSize}",
                    pageNumber, pageSize);
                throw;
            }
        }

        /// <inheritdoc/>
        [Obsolete("Use GetByProviderPaginatedAsync instead. This method loads all records into memory and will be removed in a future version.")]
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
        public async Task<(List<ModelProviderMappingEntity> Items, int TotalCount)> GetByProviderPaginatedAsync(
            int providerId,
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
                    pageSize, maxPageSize);
                pageSize = maxPageSize;
            }

            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

                var query = dbContext.ModelProviderMappings
                    .Include(m => m.Provider)
                    .Include(m => m.ModelProviderTypeAssociation)
                        .ThenInclude(a => a.Model)
                    .AsNoTracking()
                    .Where(m => m.ProviderId == providerId);

                var totalCount = await query.CountAsync(cancellationToken);

                var items = await query
                    .OrderBy(m => m.ModelAlias)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync(cancellationToken);

                return (items, totalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting paginated model provider mappings for provider {ProviderId}, page {PageNumber}, size {PageSize}",
                    providerId, pageNumber, pageSize);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<List<ModelProviderMappingEntity>> GetByModelIdAsync(
            int modelId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                return await dbContext.ModelProviderMappings
                    .Include(m => m.Provider)
                    .Include(m => m.ModelProviderTypeAssociation)
                        .ThenInclude(a => a.Model)
                    .AsNoTracking()
                    .Where(m => m.ModelProviderTypeAssociation != null && m.ModelProviderTypeAssociation.ModelId == modelId)
                    .OrderBy(m => m.ModelAlias)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting model provider mappings for model ID {ModelId}", modelId);
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
                _logger.LogError(ex, "Error creating model provider mapping for {ModelAlias}", LoggingSanitizer.S(modelProviderMapping.ModelAlias));
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
                existingEntity.ModelProviderTypeAssociationId = modelProviderMapping.ModelProviderTypeAssociationId;
                
                existingEntity.UpdatedAt = DateTime.UtcNow;

                _logger.LogInformation(
                    "Updating model mapping {ModelAlias} with AssociationId={AssociationId}",
                    existingEntity.ModelAlias,
                    existingEntity.ModelProviderTypeAssociationId);

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
