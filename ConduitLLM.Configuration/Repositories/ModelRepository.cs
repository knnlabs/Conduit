using Microsoft.EntityFrameworkCore;
using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Configuration.Repositories
{
    /// <summary>
    /// Repository implementation for Model entity operations.
    /// </summary>
    public class ModelRepository : IModelRepository
    {
        private readonly IDbContextFactory<ConduitDbContext> _dbContextFactory;

        public ModelRepository(IDbContextFactory<ConduitDbContext> dbContextFactory)
        {
            _dbContextFactory = dbContextFactory;
        }

        public async Task<Model?> GetByIdAsync(int id)
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();
            return await context.Set<Model>()
                .FirstOrDefaultAsync(m => m.Id == id);
        }

        public async Task<Model?> GetByIdWithDetailsAsync(int id)
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();
            return await context.Set<Model>()
                .Include(m => m.Capabilities)
                .Include(m => m.Series)
                    .ThenInclude(s => s.Author)
                .Include(m => m.Identifiers)
                .FirstOrDefaultAsync(m => m.Id == id);
        }

        public async Task<List<Model>> GetAllAsync()
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();
            return await context.Set<Model>()
                .OrderBy(m => m.Name)
                .ToListAsync();
        }

        public async Task<List<Model>> GetAllWithDetailsAsync()
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();
            return await context.Set<Model>()
                .Include(m => m.Capabilities)
                .Include(m => m.Series)
                    .ThenInclude(s => s.Author)
                .OrderBy(m => m.Name)
                .ToListAsync();
        }

        public async Task<Model?> GetByIdentifierAsync(string identifier)
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();
            // First check ModelIdentifiers table
            var modelIdentifier = await context.Set<ModelProviderTypeAssociation>()
                .Include(mi => mi.Model)
                    .ThenInclude(m => m.Capabilities)
                .Include(mi => mi.Model)
                    .ThenInclude(m => m.Series)
                .Where(mi => mi.Identifier == identifier)
                .OrderBy(mi => mi.IsPrimary ? 0 : 1) // Prefer primary identifier
                .FirstOrDefaultAsync();

            if (modelIdentifier != null)
                return modelIdentifier.Model;

            // Fallback: Check by model name
            return await context.Set<Model>()
                .Include(m => m.Capabilities)
                .Include(m => m.Series)
                .FirstOrDefaultAsync(m => m.Name == identifier);
        }

        public async Task<List<Model>> GetBySeriesAsync(int seriesId)
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();
            return await context.Set<Model>()
                .Include(m => m.Capabilities)
                .Where(m => m.ModelSeriesId == seriesId)
                .OrderBy(m => m.Name)
                .ToListAsync();
        }

        public async Task<Model> CreateAsync(Model model)
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();
            context.Set<Model>().Add(model);
            await context.SaveChangesAsync();
            return model;
        }

        public async Task<Model> UpdateAsync(Model model)
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();
            context.Set<Model>().Update(model);
            await context.SaveChangesAsync();
            return model;
        }

        public async Task<bool> ExistsAsync(int id)
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();
            return await context.Set<Model>()
                .AnyAsync(m => m.Id == id);
        }

        public async Task<Model?> GetByNameAsync(string name)
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();
            return await context.Set<Model>()
                .Include(m => m.Capabilities)
                .FirstOrDefaultAsync(m => m.Name == name);
        }

        public async Task<List<Model>> SearchByNameAsync(string query)
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();
            var lowerQuery = query.ToLower();
            return await context.Set<Model>()
                .Include(m => m.Capabilities)
                .Where(m => m.Name.ToLower().Contains(lowerQuery) && m.IsActive)
                .OrderBy(m => m.Name)
                .ToListAsync();
        }

        public async Task<bool> HasMappingReferencesAsync(int modelId)
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();
            return await context.Set<ModelProviderMapping>()
                .AnyAsync(m => m.ModelId == modelId);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();
            var model = await context.Set<Model>().FindAsync(id);
            if (model == null)
                return false;
            
            context.Set<Model>().Remove(model);
            await context.SaveChangesAsync();
            return true;
        }

        public async Task<List<Model>> GetByProviderAsync(string providerName)
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();
            
            // Get model IDs that have identifiers for this provider
            var modelIds = await context.Set<ModelProviderTypeAssociation>()
                .Where(mi => mi.Provider == providerName.ToLower())
                .Select(mi => mi.ModelId)
                .Distinct()
                .ToListAsync();

            // Return models with those IDs, including capabilities and identifiers
            return await context.Set<Model>()
                .Include(m => m.Capabilities)
                .Include(m => m.Series)
                    .ThenInclude(s => s.Author)
                .Include(m => m.Identifiers)
                .Where(m => modelIds.Contains(m.Id))
                .OrderBy(m => m.Name)
                .ToListAsync();
        }

        public async Task<bool> DeleteIdentifierAsync(int modelId, int identifierId)
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();
            
            var identifier = await context.Set<ModelProviderTypeAssociation>()
                .FirstOrDefaultAsync(i => i.Id == identifierId && i.ModelId == modelId);
            
            if (identifier == null)
            {
                return false;
            }
            
            context.Set<ModelProviderTypeAssociation>().Remove(identifier);
            await context.SaveChangesAsync();
            
            return true;
        }
    }
}