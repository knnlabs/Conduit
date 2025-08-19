using ConduitLLM.Configuration.Data;
using ConduitLLM.Configuration.Entities;
using Microsoft.EntityFrameworkCore;

namespace ConduitLLM.Configuration.Repositories
{
    /// <summary>
    /// Repository for ModelCapabilities entity operations.
    /// </summary>
    public class ModelCapabilitiesRepository : IModelCapabilitiesRepository
    {
        private readonly IDbContextFactory<ConduitDbContext> _dbContextFactory;

        public ModelCapabilitiesRepository(IDbContextFactory<ConduitDbContext> dbContextFactory)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        }

        public async Task<ModelCapabilities?> GetByIdAsync(int id)
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();
            return await context.Set<ModelCapabilities>()
                .FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task<List<ModelCapabilities>> GetAllAsync()
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();
            return await context.Set<ModelCapabilities>()
                .OrderBy(c => c.Id)
                .ToListAsync();
        }

        public async Task<List<Model>?> GetModelsUsingCapabilitiesAsync(int capabilitiesId)
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();
            var exists = await context.Set<ModelCapabilities>()
                .AnyAsync(c => c.Id == capabilitiesId);
            
            if (!exists)
                return null;

            return await context.Set<Model>()
                .Where(m => m.ModelCapabilitiesId == capabilitiesId)
                .OrderBy(m => m.Name)
                .ToListAsync();
        }

        public async Task<ModelCapabilities> CreateAsync(ModelCapabilities capabilities)
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();
            context.Set<ModelCapabilities>().Add(capabilities);
            await context.SaveChangesAsync();
            return capabilities;
        }

        public async Task<ModelCapabilities> UpdateAsync(ModelCapabilities capabilities)
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();
            context.Set<ModelCapabilities>().Update(capabilities);
            await context.SaveChangesAsync();
            return capabilities;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();
            var capabilities = await context.Set<ModelCapabilities>()
                .FirstOrDefaultAsync(c => c.Id == id);
            
            if (capabilities == null)
                return false;

            context.Set<ModelCapabilities>().Remove(capabilities);
            await context.SaveChangesAsync();
            return true;
        }
    }
}