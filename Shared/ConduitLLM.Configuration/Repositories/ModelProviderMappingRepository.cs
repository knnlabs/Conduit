using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Utilities;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Configuration.Repositories
{
    /// <summary>
    /// Repository implementation for model provider mappings using Entity Framework Core.
    /// </summary>
    public class ModelProviderMappingRepository : RepositoryBase<ModelProviderMapping, int>, IModelProviderMappingRepository
    {
        /// <summary>
        /// Creates a new instance of the repository
        /// </summary>
        /// <param name="dbContextFactory">The database context factory</param>
        /// <param name="logger">The logger</param>
        public ModelProviderMappingRepository(
            IDbContextFactory<ConduitDbContext> dbContextFactory,
            ILogger<ModelProviderMappingRepository> logger)
            : base(dbContextFactory, logger)
        {
        }

        /// <inheritdoc/>
        protected override DbSet<ModelProviderMapping> GetDbSet(ConduitDbContext context)
        {
            return context.ModelProviderMappings;
        }

        /// <inheritdoc/>
        protected override IQueryable<ModelProviderMapping> ApplyDefaultIncludes(IQueryable<ModelProviderMapping> query)
        {
            return query
                .Include(m => m.Provider)
                .Include(m => m.ModelProviderTypeAssociation)
                    .ThenInclude(a => a.Model)
                        .ThenInclude(m => m.Series);
        }

        /// <inheritdoc/>
        protected override IQueryable<ModelProviderMapping> ApplyDefaultOrdering(IQueryable<ModelProviderMapping> query)
        {
            return query.OrderBy(m => m.ModelAlias);
        }

        /// <inheritdoc/>
        public async Task<ModelProviderMapping?> GetByModelNameAsync(
            string modelName,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(modelName))
            {
                throw new ArgumentException("Model name cannot be null or empty", nameof(modelName));
            }

            try
            {
                return await ExecuteAsync(async context =>
                {
                    var query = GetDbSet(context).AsNoTracking();
                    query = ApplyDefaultIncludes(query);
                    return await query.FirstOrDefaultAsync(m => m.ModelAlias == modelName, cancellationToken);
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error getting model provider mapping for model {ModelName}", LoggingSanitizer.S(modelName));
                throw;
            }
        }

        /// <inheritdoc/>
        [Obsolete("Use GetPaginatedAsync instead. This method loads all records into memory and will be removed in a future version.")]
        public async Task<List<ModelProviderMapping>> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await ExecuteAsync(async context =>
                {
                    var query = GetDbSet(context).AsNoTracking();
                    query = ApplyDefaultIncludes(query);
                    query = ApplyDefaultOrdering(query);
                    return await query.ToListAsync(cancellationToken);
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error getting all model provider mappings");
                throw;
            }
        }

        /// <inheritdoc/>
        [Obsolete("Use GetByProviderPaginatedAsync instead. This method loads all records into memory and will be removed in a future version.")]
        public async Task<List<ModelProviderMapping>> GetByProviderAsync(
            ProviderType providerType,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await ExecuteAsync(async context =>
                {
                    var credential = await context.Providers
                        .AsNoTracking()
                        .FirstOrDefaultAsync(pc => pc.ProviderType == providerType, cancellationToken);

                    if (credential == null)
                    {
                        return new List<ModelProviderMapping>();
                    }

                    // Then find mappings with this credential ID
                    var query = GetDbSet(context).AsNoTracking();
                    query = ApplyDefaultIncludes(query);
                    return await query
                        .Where(m => m.ProviderId == credential.Id)
                        .OrderBy(m => m.ModelAlias)
                        .ToListAsync(cancellationToken);
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error getting model provider mappings for provider type {ProviderType}", providerType);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<(List<ModelProviderMapping> Items, int TotalCount)> GetByProviderPaginatedAsync(
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

            if (pageSize > MaxPageSize)
            {
                Logger.LogWarning("Requested page size {RequestedPageSize} exceeds maximum allowed {MaxPageSize}, limiting to maximum",
                    pageSize, MaxPageSize);
                pageSize = MaxPageSize;
            }

            try
            {
                return await ExecuteAsync(async context =>
                {
                    var query = GetDbSet(context).AsNoTracking();
                    query = ApplyDefaultIncludes(query);
                    query = query.Where(m => m.ProviderId == providerId);

                    var totalCount = await query.CountAsync(cancellationToken);

                    var items = await query
                        .OrderBy(m => m.ModelAlias)
                        .Skip((pageNumber - 1) * pageSize)
                        .Take(pageSize)
                        .ToListAsync(cancellationToken);

                    return (items, totalCount);
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error getting paginated model provider mappings for provider {ProviderId}, page {PageNumber}, size {PageSize}",
                    providerId, pageNumber, pageSize);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<List<ModelProviderMapping>> GetByModelIdAsync(
            int modelId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await ExecuteAsync(async context =>
                {
                    var query = GetDbSet(context).AsNoTracking();
                    query = ApplyDefaultIncludes(query);
                    return await query
                        .Where(m => m.ModelProviderTypeAssociation != null && m.ModelProviderTypeAssociation.ModelId == modelId)
                        .OrderBy(m => m.ModelAlias)
                        .ToListAsync(cancellationToken);
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error getting model provider mappings for model ID {ModelId}", modelId);
                throw;
            }
        }

        /// <inheritdoc/>
        public override async Task<bool> UpdateAsync(
            ModelProviderMapping modelProviderMapping,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(modelProviderMapping);

            try
            {
                return await ExecuteAsync(async context =>
                {
                    // Get existing entity to ensure it exists
                    var existingEntity = await GetDbSet(context)
                        .FirstOrDefaultAsync(m => m.Id == modelProviderMapping.Id, cancellationToken);

                    if (existingEntity == null)
                    {
                        Logger.LogWarning("Cannot update non-existent model provider mapping with ID {MappingId}", modelProviderMapping.Id);
                        return false;
                    }

                    // Update fields
                    existingEntity.ModelAlias = modelProviderMapping.ModelAlias;
                    existingEntity.ProviderModelId = modelProviderMapping.ProviderModelId;
                    existingEntity.ProviderId = modelProviderMapping.ProviderId;
                    existingEntity.IsEnabled = modelProviderMapping.IsEnabled;
                    existingEntity.ModelProviderTypeAssociationId = modelProviderMapping.ModelProviderTypeAssociationId;

                    existingEntity.UpdatedAt = DateTime.UtcNow;

                    Logger.LogInformation(
                        "Updating model mapping {ModelAlias} with AssociationId={AssociationId}",
                        existingEntity.ModelAlias,
                        existingEntity.ModelProviderTypeAssociationId);

                    await context.SaveChangesAsync(cancellationToken);
                    return true;
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error updating model provider mapping with ID {MappingId}", modelProviderMapping.Id);
                throw;
            }
        }
    }
}
