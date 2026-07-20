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
                    .ThenInclude(a => a.ModelCost)
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

            return await ExecuteAsync(async context =>
            {
                var query = GetDbSet(context).AsNoTracking();
                query = ApplyDefaultIncludes(query);
                return await query.FirstOrDefaultAsync(m => m.ModelAlias == modelName, cancellationToken);
            }, cancellationToken, $"getting by model name {LoggingSanitizer.S(modelName)}");
        }

        public async Task<List<ModelProviderMapping>> GetAllByModelNameAsync(
            string modelName,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(modelName))
                throw new ArgumentException("Model name cannot be null or empty", nameof(modelName));
            return await ExecuteAsync(async context =>
            {
                var query = ApplyDefaultIncludes(GetDbSet(context).AsNoTracking());
                return await query.Where(mapping => mapping.ModelAlias == modelName)
                    .OrderBy(mapping => mapping.RoutingPriority).ThenBy(mapping => mapping.Id)
                    .ToListAsync(cancellationToken);
            }, cancellationToken, $"getting all mappings by model name {LoggingSanitizer.S(modelName)}");
        }

        public async Task<int?> GetCanonicalModelIdForAssociationAsync(int associationId, CancellationToken cancellationToken = default) =>
            await ExecuteAsync(async context => await context.ModelProviderTypeAssociations.AsNoTracking()
                .Where(association => association.Id == associationId)
                .Select(association => (int?)association.ModelId)
                .SingleOrDefaultAsync(cancellationToken), cancellationToken, $"getting canonical model for association {associationId}");

        /// <inheritdoc/>
        [Obsolete("Use GetByProviderPaginatedAsync instead. This method loads all records into memory and will be removed in a future version.")]
        public async Task<List<ModelProviderMapping>> GetByProviderAsync(
            ProviderType providerType,
            CancellationToken cancellationToken = default)
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
            }, cancellationToken, $"getting by provider type {providerType}");
        }

        /// <inheritdoc/>
        public async Task<(List<ModelProviderMapping> Items, int TotalCount)> GetByProviderPaginatedAsync(
            int providerId,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            return await GetFilteredPaginatedAsync(
                m => m.ProviderId == providerId,
                pageNumber,
                pageSize,
                q => q.OrderBy(m => m.ModelAlias),
                cancellationToken,
                $"getting paginated for provider {providerId}");
        }

        /// <inheritdoc/>
        public async Task<List<ModelProviderMapping>> GetByModelIdAsync(
            int modelId,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async context =>
            {
                var query = GetDbSet(context).AsNoTracking();
                query = ApplyDefaultIncludes(query);
                return await query
                    .Where(m => m.ModelProviderTypeAssociation != null && m.ModelProviderTypeAssociation.ModelId == modelId)
                    .OrderBy(m => m.ModelAlias)
                    .ToListAsync(cancellationToken);
            }, cancellationToken, $"getting by model ID {modelId}");
        }

        /// <inheritdoc/>
        public override async Task<bool> UpdateAsync(
            ModelProviderMapping modelProviderMapping,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(modelProviderMapping);

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
                existingEntity.ProviderOptions = modelProviderMapping.ProviderOptions;
                existingEntity.RoutingPriority = modelProviderMapping.RoutingPriority;
                existingEntity.RoutingWeight = modelProviderMapping.RoutingWeight;

                existingEntity.UpdatedAt = DateTime.UtcNow;

                Logger.LogInformation(
                    "Updating model mapping {ModelAlias} with AssociationId={AssociationId}",
                    existingEntity.ModelAlias,
                    existingEntity.ModelProviderTypeAssociationId);

                await context.SaveChangesAsync(cancellationToken);
                return true;
            }, cancellationToken, $"updating ID {modelProviderMapping.Id}");
        }
    }
}
