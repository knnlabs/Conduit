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
        [Obsolete("Use GetPaginatedAsync instead. This method loads all records into memory and will be removed in a future version.")]
        public async Task<List<ModelCost>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async context =>
            {
                var query = GetDbSet(context).AsNoTracking();
                query = ApplyDefaultIncludes(query);
                query = ApplyDefaultOrdering(query);
                return await query.ToListAsync(cancellationToken);
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

                // Get all model mappings for this provider
                var providerMappings = await context.ModelProviderMappings
                    .AsNoTracking()
                    .Where(m => m.ProviderId == providerId)
                    .ToListAsync(cancellationToken);

                if (!providerMappings.Any())
                {
                    Logger.LogInformation("No model mappings found for provider {ProviderId}", providerId);
                    return new List<ModelCost>();
                }

                // Get model costs associated with models from this provider
                var query = GetDbSet(context).AsNoTracking();
                query = ApplyDefaultIncludes(query);
                query = query.Where(m => m.ModelProviderTypeAssociations.Any(mpta =>
                    mpta.Provider != null && mpta.IsEnabled));
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
                query = query.Where(m => m.ModelProviderTypeAssociations.Any(mpta =>
                    mpta.Provider != null && mpta.IsEnabled));

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
