using ConduitLLM.Configuration.Utilities;
using ConduitLLM.Functions.Entities;
using ConduitLLM.Functions.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Configuration.Repositories;

/// <summary>
/// Repository implementation for function cost mappings using RepositoryBase.
/// </summary>
public class FunctionCostMappingRepository : RepositoryBase<FunctionCostMapping, int>, IFunctionCostMappingRepository
{
    public FunctionCostMappingRepository(
        IDbContextFactory<ConduitDbContext> dbContextFactory,
        ILogger<FunctionCostMappingRepository> logger)
        : base(dbContextFactory, logger) { }

    protected override DbSet<FunctionCostMapping> GetDbSet(ConduitDbContext context)
        => context.FunctionCostMappings;

    protected override IQueryable<FunctionCostMapping> ApplyDefaultIncludes(IQueryable<FunctionCostMapping> query)
        => query.Include(m => m.FunctionConfiguration).Include(m => m.FunctionCost);

    public async Task<List<FunctionCostMapping>> GetByFunctionConfigurationIdAsync(
        int functionConfigurationId, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async db =>
        {
            return await ApplyDefaultIncludes(GetDbSet(db).AsNoTracking())
                .Where(m => m.FunctionConfigurationId == functionConfigurationId)
                .OrderByDescending(m => m.IsActive)
                .ThenByDescending(m => m.FunctionCost!.Priority)
                .ToListAsync(cancellationToken);
        }, cancellationToken, "GetByFunctionConfigurationId");
    }

    public async Task<FunctionCostMapping?> GetActiveMappingAsync(
        int functionConfigurationId, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async db =>
        {
            return await GetDbSet(db).AsNoTracking()
                .Include(m => m.FunctionCost)
                .Where(m => m.FunctionConfigurationId == functionConfigurationId && m.IsActive)
                .OrderByDescending(m => m.FunctionCost!.Priority)
                .FirstOrDefaultAsync(cancellationToken);
        }, cancellationToken, "GetActiveMapping");
    }

    public async Task DeactivateAllForFunctionAsync(
        int functionConfigurationId, CancellationToken cancellationToken = default)
    {
        await ExecuteAsync(async db =>
        {
            var mappings = await GetDbSet(db)
                .Where(m => m.FunctionConfigurationId == functionConfigurationId && m.IsActive)
                .ToListAsync(cancellationToken);

            foreach (var mapping in mappings)
            {
                mapping.IsActive = false;
            }

            await db.SaveChangesAsync(cancellationToken);
            return true;
        }, cancellationToken, "DeactivateAllForFunction");
    }
}
