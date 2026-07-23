using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Configuration.Repositories
{
    /// <summary>
    /// Repository implementation for model costs using Entity Framework Core.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This repository provides data access operations for model cost entities using Entity Framework Core.
    /// It extends RepositoryBase for standard CRUD operations and implements domain-specific methods
    /// from IModelCostRepository.
    /// </para>
    /// <para>
    /// ModelCost entities store pricing information for different LLM models, including input token costs,
    /// output token costs, and additional costs for specific operations like embeddings or image generation.
    /// This repository enables the application to manage these cost records and calculate usage expenses.
    /// </para>
    /// </remarks>
    public class ModelCostRepository : RepositoryBase<ModelCost, int>, IModelCostRepository
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ModelCostRepository"/> class.
        /// </summary>
        /// <param name="dbContextFactory">The database context factory used to create DbContext instances.</param>
        /// <param name="logger">The logger for recording diagnostic information.</param>
        public ModelCostRepository(
            IDbContextFactory<ConduitDbContext> dbContextFactory,
            ILogger<ModelCostRepository> logger)
            : base(dbContextFactory, logger)
        {
        }

        /// <inheritdoc/>
        protected override DbSet<ModelCost> GetDbSet(ConduitDbContext context)
        {
            return context.ModelCosts;
        }

        /// <inheritdoc/>
        protected override IQueryable<ModelCost> ApplyDefaultIncludes(IQueryable<ModelCost> query)
        {
            return query
                .Include(m => m.ModelProviderTypeAssociations)
                    .ThenInclude(mpta => mpta.Model);
        }

        /// <inheritdoc/>
        protected override IQueryable<ModelCost> ApplyDefaultOrdering(IQueryable<ModelCost> query)
        {
            return query.OrderBy(m => m.CostName);
        }

        private static IQueryable<ModelCost> ApplyProviderFilter(
            IQueryable<ModelCost> query,
            ConduitDbContext context,
            int providerId)
        {
            var providerAssociationIds = context.ModelProviderMappings
                .AsNoTracking()
                .Where(mapping => mapping.ProviderId == providerId)
                .Select(mapping => mapping.ModelProviderTypeAssociationId);

            return query.Where(cost => cost.ModelProviderTypeAssociations.Any(association =>
                association.IsEnabled && providerAssociationIds.Contains(association.Id)));
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Overrides the graph-traversing <c>DbSet.Update()</c> in the base class and marks only
        /// the root <see cref="ModelCost"/> entity as modified. Entities returned by
        /// <c>GetByIdAsync</c>/<c>GetByCostNameAsync</c> carry the included
        /// ModelProviderTypeAssociations → Model graph, and <c>Model.Series</c> is a phantom
        /// <c>new ModelSeries()</c> (Id = 0, with a phantom <c>ModelAuthor</c>) because the
        /// query does not include it. A graph-wide Update() attached those phantoms as Added,
        /// inserting empty ModelSeries/ModelAuthor rows and failing with unique-constraint
        /// violations on subsequent saves (issue #977). Association changes are managed
        /// explicitly by the Admin service, not through this method.
        /// </remarks>
        public override async Task<bool> UpdateAsync(ModelCost entity, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(entity);

            try
            {
                await using var context = await DbContextFactory.CreateDbContextAsync(cancellationToken);

                OnBeforeUpdate(entity);

                // Attach only the root entity — never the detached navigation graph.
                context.Entry(entity).State = EntityState.Modified;
                int rowsAffected = await context.SaveChangesAsync(cancellationToken);

                return rowsAffected > 0;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                Logger.LogWarning(ex, "Concurrency conflict updating {EntityType} with ID {Id} — another process modified this entity",
                    EntityTypeName, entity.Id);
                throw;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error updating {EntityType} with ID {Id}", EntityTypeName, entity.Id);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<ModelCost?> GetByCostNameAsync(string costName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(costName))
            {
                throw new ArgumentException("Cost name cannot be null or empty", nameof(costName));
            }

            return await ExecuteAsync(async context =>
            {
                var query = GetDbSet(context).AsNoTracking();
                query = ApplyDefaultIncludes(query);
                return await query.FirstOrDefaultAsync(m => m.CostName == costName, cancellationToken);
            }, cancellationToken);
        }

        /// <inheritdoc/>
        [Obsolete("Use GetByProviderPaginatedAsync instead. This method loads all records into memory and will be removed in a future version.")]
        public async Task<List<ModelCost>> GetByProviderAsync(int providerId, CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async context =>
            {
                // Verify provider exists
                var provider = await context.Providers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == providerId, cancellationToken);

                if (provider == null)
                {
                    Logger.LogWarning("No provider found with ID {ProviderId}", providerId);
                    return new List<ModelCost>();
                }

                var hasProviderMappings = await context.ModelProviderMappings
                    .AsNoTracking()
                    .AnyAsync(mapping => mapping.ProviderId == providerId, cancellationToken);

                if (!hasProviderMappings)
                {
                    Logger.LogInformation("No model mappings found for provider {ProviderId}", providerId);
                    return new List<ModelCost>();
                }

                // Get model costs associated with models from this provider
                var query = GetDbSet(context).AsNoTracking();
                query = ApplyDefaultIncludes(query);
                query = ApplyProviderFilter(query, context, providerId);
                query = ApplyDefaultOrdering(query);

                return await query.ToListAsync(cancellationToken);
            }, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<(List<ModelCost> Items, int TotalCount)> GetByProviderPaginatedAsync(
            int providerId,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            (pageNumber, pageSize) = NormalizePagination(pageNumber, pageSize);

            return await ExecuteAsync(async context =>
            {
                // Verify provider exists
                var provider = await context.Providers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == providerId, cancellationToken);

                if (provider == null)
                {
                    Logger.LogWarning("No provider found with ID {ProviderId}", providerId);
                    return (new List<ModelCost>(), 0);
                }

                var query = GetDbSet(context).AsNoTracking();
                query = ApplyDefaultIncludes(query);
                query = ApplyProviderFilter(query, context, providerId);

                var totalCount = await query.CountAsync(cancellationToken);

                query = ApplyDefaultOrdering(query);
                var items = await query
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync(cancellationToken);

                return (items, totalCount);
            }, cancellationToken);
        }
    }
}
