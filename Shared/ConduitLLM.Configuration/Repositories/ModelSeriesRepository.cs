using ConduitLLM.Configuration.Entities;

using Microsoft.EntityFrameworkCore;

namespace ConduitLLM.Configuration.Repositories
{
    /// <summary>
    /// Repository for ModelSeries entity operations.
    /// </summary>
    public class ModelSeriesRepository : IModelSeriesRepository
    {
        private readonly IDbContextFactory<ConduitDbContext> _dbContextFactory;

        public ModelSeriesRepository(IDbContextFactory<ConduitDbContext> dbContextFactory)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        }

        public async Task<ModelSeries?> GetByIdAsync(int id)
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();
            return await context.Set<ModelSeries>()
                .FirstOrDefaultAsync(s => s.Id == id);
        }

        public async Task<ModelSeries?> GetByIdWithAuthorAsync(int id)
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();
            return await context.Set<ModelSeries>()
                .Include(s => s.Author)
                .FirstOrDefaultAsync(s => s.Id == id);
        }

        public async Task<List<ModelSeries>> GetAllAsync()
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();
            return await context.Set<ModelSeries>()
                .OrderBy(s => s.Name)
                .ToListAsync();
        }

        public async Task<List<ModelSeries>> GetAllWithAuthorAsync()
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();
            return await context.Set<ModelSeries>()
                .Include(s => s.Author)
                .OrderBy(s => s.Name)
                .ToListAsync();
        }

        public async Task<ModelSeries?> GetByNameAndAuthorAsync(string name, int authorId)
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();
            return await context.Set<ModelSeries>()
                .FirstOrDefaultAsync(s => s.Name == name && s.AuthorId == authorId);
        }

        public async Task<List<Model>?> GetModelsInSeriesAsync(int seriesId)
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();
            var exists = await context.Set<ModelSeries>()
                .AnyAsync(s => s.Id == seriesId);
            
            if (!exists)
                return null;

            return await context.Set<Model>()
                .Where(m => m.ModelSeriesId == seriesId)
                .OrderBy(m => m.Name)
                .ToListAsync();
        }

        public async Task<ModelSeries> CreateAsync(ModelSeries series)
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();
            context.Set<ModelSeries>().Add(series);
            await context.SaveChangesAsync();
            return series;
        }

        public async Task<ModelSeries> UpdateAsync(ModelSeries series)
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();
            context.Set<ModelSeries>().Update(series);
            await context.SaveChangesAsync();
            return series;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();
            var series = await context.Set<ModelSeries>()
                .FirstOrDefaultAsync(s => s.Id == id);
            
            if (series == null)
                return false;

            context.Set<ModelSeries>().Remove(series);
            await context.SaveChangesAsync();
            return true;
        }
    }
}