using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Data;
using ConduitLLM.Configuration.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static ConduitLLM.Configuration.Utilities.LogSanitizer;

namespace ConduitLLM.Configuration.Repositories
{
    /// <summary>
    /// Repository implementation for model deployments using Entity Framework Core
    /// </summary>
    public class ModelDeploymentRepository : IModelDeploymentRepository
    {
        private readonly IDbContextFactory<ConfigurationDbContext> _dbContextFactory;
        private readonly ILogger<ModelDeploymentRepository> _logger;

        /// <summary>
        /// Creates a new instance of the repository
        /// </summary>
        /// <param name="dbContextFactory">The database context factory</param>
        /// <param name="logger">The logger</param>
        public ModelDeploymentRepository(
            IDbContextFactory<ConfigurationDbContext> dbContextFactory,
            ILogger<ModelDeploymentRepository> logger)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public async Task<ModelDeploymentEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                return await dbContext.ModelDeployments
                    .AsNoTracking()
                    .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting model deployment with ID {DeploymentId}", S(id));
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<ModelDeploymentEntity?> GetByDeploymentNameAsync(string deploymentName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(deploymentName))
            {
                throw new ArgumentException("Deployment name cannot be null or empty", nameof(deploymentName));
            }

            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                return await dbContext.ModelDeployments
                    .AsNoTracking()
                    .FirstOrDefaultAsync(d => d.DeploymentName == deploymentName, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting model deployment with name {DeploymentName}", S(deploymentName));
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<List<ModelDeploymentEntity>> GetByProviderAsync(string providerName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(providerName))
            {
                throw new ArgumentException("Provider name cannot be null or empty", nameof(providerName));
            }

            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                return await dbContext.ModelDeployments
                    .AsNoTracking()
                    .Where(d => d.ProviderName == providerName)
                    .OrderBy(d => d.ModelName)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting model deployments for provider {ProviderName}", S(providerName));
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<List<ModelDeploymentEntity>> GetByModelNameAsync(string modelName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(modelName))
            {
                throw new ArgumentException("Model name cannot be null or empty", nameof(modelName));
            }

            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                return await dbContext.ModelDeployments
                    .AsNoTracking()
                    .Where(d => d.ModelName == modelName)
                    .OrderBy(d => d.ProviderName)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting model deployments for model {ModelName}", S(modelName));
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<List<ModelDeploymentEntity>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                return await dbContext.ModelDeployments
                    .AsNoTracking()
                    .OrderBy(d => d.ProviderName)
                    .ThenBy(d => d.ModelName)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all model deployments");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<Guid> CreateAsync(ModelDeploymentEntity modelDeployment, CancellationToken cancellationToken = default)
        {
            if (modelDeployment == null)
            {
                throw new ArgumentNullException(nameof(modelDeployment));
            }

            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

                // Set timestamps
                modelDeployment.CreatedAt = DateTime.UtcNow;
                modelDeployment.UpdatedAt = DateTime.UtcNow;

                dbContext.ModelDeployments.Add(modelDeployment);
                await dbContext.SaveChangesAsync(cancellationToken);
                return modelDeployment.Id;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error creating model deployment '{DeploymentName}'",
                    S(modelDeployment.DeploymentName));
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating model deployment '{DeploymentName}'",
                    S(modelDeployment.DeploymentName));
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> UpdateAsync(ModelDeploymentEntity modelDeployment, CancellationToken cancellationToken = default)
        {
            if (modelDeployment == null)
            {
                throw new ArgumentNullException(nameof(modelDeployment));
            }

            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

                // Set updated timestamp
                modelDeployment.UpdatedAt = DateTime.UtcNow;

                dbContext.ModelDeployments.Update(modelDeployment);
                int rowsAffected = await dbContext.SaveChangesAsync(cancellationToken);
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating model deployment with ID {DeploymentId}",
                    S(modelDeployment.Id));
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                var modelDeployment = await dbContext.ModelDeployments.FindAsync(new object[] { id }, cancellationToken);

                if (modelDeployment == null)
                {
                    return false;
                }

                dbContext.ModelDeployments.Remove(modelDeployment);
                int rowsAffected = await dbContext.SaveChangesAsync(cancellationToken);
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting model deployment with ID {DeploymentId}", S(id));
                throw;
            }
        }
    }
}
