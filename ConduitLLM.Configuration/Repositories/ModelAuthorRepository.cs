using ConduitLLM.Configuration.Data;
using ConduitLLM.Configuration.Entities;
using Microsoft.EntityFrameworkCore;

namespace ConduitLLM.Configuration.Repositories
{
    /// <summary>
    /// Repository for ModelAuthor entity operations.
    /// </summary>
    public class ModelAuthorRepository : IModelAuthorRepository
    {
        private readonly IDbContextFactory<ConduitDbContext> _dbContextFactory;

        public ModelAuthorRepository(IDbContextFactory<ConduitDbContext> dbContextFactory)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        }

        public async Task<ModelAuthor?> GetByIdAsync(int id)
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();
            return await context.Set<ModelAuthor>()
                .FirstOrDefaultAsync(a => a.Id == id);
        }

        public async Task<List<ModelAuthor>> GetAllAsync()
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();
            return await context.Set<ModelAuthor>()
                .OrderBy(a => a.Name)
                .ToListAsync();
        }

        public async Task<ModelAuthor?> GetByNameAsync(string name)
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();
            return await context.Set<ModelAuthor>()
                .FirstOrDefaultAsync(a => a.Name == name);
        }

        public async Task<List<ModelSeries>?> GetSeriesByAuthorAsync(int authorId)
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();
            var exists = await context.Set<ModelAuthor>()
                .AnyAsync(a => a.Id == authorId);
            
            if (!exists)
                return null;

            return await context.Set<ModelSeries>()
                .Where(s => s.AuthorId == authorId)
                .OrderBy(s => s.Name)
                .ToListAsync();
        }

        public async Task<ModelAuthor> CreateAsync(ModelAuthor author)
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();
            context.Set<ModelAuthor>().Add(author);
            await context.SaveChangesAsync();
            return author;
        }

        public async Task<ModelAuthor> UpdateAsync(ModelAuthor author)
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();
            context.Set<ModelAuthor>().Update(author);
            await context.SaveChangesAsync();
            return author;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();
            var author = await context.Set<ModelAuthor>()
                .FirstOrDefaultAsync(a => a.Id == id);
            
            if (author == null)
                return false;

            context.Set<ModelAuthor>().Remove(author);
            await context.SaveChangesAsync();
            return true;
        }
    }
}