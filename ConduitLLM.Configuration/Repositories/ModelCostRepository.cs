using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Data;
using ConduitLLM.Configuration.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Configuration.Repositories
{
    /// <summary>
    /// Repository implementation for model costs using Entity Framework Core
    /// </summary>
    public class ModelCostRepository : IModelCostRepository
    {
        private readonly IDbContextFactory<ConfigurationDbContext> _dbContextFactory;
        private readonly ILogger<ModelCostRepository> _logger;

        /// <summary>
        /// Creates a new instance of the repository
        /// </summary>
        /// <param name="dbContextFactory">The database context factory</param>
        /// <param name="logger">The logger</param>
        public ModelCostRepository(
            IDbContextFactory<ConfigurationDbContext> dbContextFactory,
            ILogger<ModelCostRepository> logger)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public async Task<ModelCost?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                return await dbContext.ModelCosts
                    .AsNoTracking()
                    .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting model cost with ID {CostId}", id);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<ModelCost?> GetByModelNameAsync(string modelName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(modelName))
            {
                throw new ArgumentException("Model name cannot be null or empty", nameof(modelName));
            }

            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                return await dbContext.ModelCosts
                    .AsNoTracking()
                    .FirstOrDefaultAsync(m => m.ModelIdPattern == modelName, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting model cost for model {ModelName}", modelName);
                throw;
            }
        }
        
        /// <inheritdoc/>
        public async Task<ModelCost?> GetByModelIdPatternAsync(string modelIdPattern, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(modelIdPattern))
            {
                throw new ArgumentException("Model ID pattern cannot be null or empty", nameof(modelIdPattern));
            }

            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                return await dbContext.ModelCosts
                    .AsNoTracking()
                    .FirstOrDefaultAsync(m => m.ModelIdPattern == modelIdPattern, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting model cost for model ID pattern {ModelIdPattern}", modelIdPattern);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<List<ModelCost>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                return await dbContext.ModelCosts
                    .AsNoTracking()
                    .OrderBy(m => m.ModelIdPattern)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all model costs");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<List<ModelCost>> GetByProviderAsync(string providerName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(providerName))
            {
                throw new ArgumentException("Provider name cannot be null or empty", nameof(providerName));
            }

            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                return await dbContext.ModelCosts
                    .AsNoTracking()
                    // This method needs to be refactored since ModelCost doesn't have a ProviderName property
                    // For now, just return all model costs
                    .OrderBy(m => m.ModelIdPattern)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting model costs for provider {ProviderName}", providerName);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<int> CreateAsync(ModelCost modelCost, CancellationToken cancellationToken = default)
        {
            if (modelCost == null)
            {
                throw new ArgumentNullException(nameof(modelCost));
            }

            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                
                // Set timestamps
                modelCost.CreatedAt = DateTime.UtcNow;
                modelCost.UpdatedAt = DateTime.UtcNow;
                
                dbContext.ModelCosts.Add(modelCost);
                await dbContext.SaveChangesAsync(cancellationToken);
                return modelCost.Id;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error creating model cost for model '{ModelIdPattern}'", 
                    modelCost.ModelIdPattern);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating model cost for model '{ModelIdPattern}'", 
                    modelCost.ModelIdPattern);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> UpdateAsync(ModelCost modelCost, CancellationToken cancellationToken = default)
        {
            if (modelCost == null)
            {
                throw new ArgumentNullException(nameof(modelCost));
            }

            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                
                // Set updated timestamp
                modelCost.UpdatedAt = DateTime.UtcNow;
                
                dbContext.ModelCosts.Update(modelCost);
                int rowsAffected = await dbContext.SaveChangesAsync(cancellationToken);
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating model cost with ID {CostId}", 
                    modelCost.Id);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                var modelCost = await dbContext.ModelCosts.FindAsync(new object[] { id }, cancellationToken);
                
                if (modelCost == null)
                {
                    return false;
                }
                
                dbContext.ModelCosts.Remove(modelCost);
                int rowsAffected = await dbContext.SaveChangesAsync(cancellationToken);
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting model cost with ID {CostId}", id);
                throw;
            }
        }
    }
}