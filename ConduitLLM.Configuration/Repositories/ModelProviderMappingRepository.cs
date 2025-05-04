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
    /// Repository implementation for model provider mappings using Entity Framework Core.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This repository provides data access operations for model provider mapping entities using Entity Framework Core.
    /// It implements the <see cref="IModelProviderMappingRepository"/> interface and enables the management of
    /// mappings between user-friendly model aliases and specific provider model implementations.
    /// </para>
    /// <para>
    /// The implementation follows these principles:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>Using short-lived DbContext instances for better performance and reliability</description></item>
    ///   <item><description>Comprehensive error handling with detailed logging</description></item>
    ///   <item><description>Non-tracking queries for read operations to improve performance</description></item>
    ///   <item><description>Automatic timestamp management for auditing purposes</description></item>
    ///   <item><description>Proper ordering of query results for consistent UI experience</description></item>
    /// </list>
    /// <para>
    /// Model provider mappings are a central component of the Conduit routing system,
    /// allowing users to reference models by friendly aliases while the system handles
    /// the mapping to specific provider implementations.
    /// </para>
    /// </remarks>
    public class ModelProviderMappingRepository : IModelProviderMappingRepository
    {
        private readonly IDbContextFactory<ConfigurationDbContext> _dbContextFactory;
        private readonly ILogger<ModelProviderMappingRepository> _logger;

        /// <summary>
        /// Creates a new instance of the repository
        /// </summary>
        /// <param name="dbContextFactory">The database context factory</param>
        /// <param name="logger">The logger</param>
        public ModelProviderMappingRepository(
            IDbContextFactory<ConfigurationDbContext> dbContextFactory,
            ILogger<ModelProviderMappingRepository> logger)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public async Task<Entities.ModelProviderMapping?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                return await dbContext.ModelProviderMappings
                    .AsNoTracking()
                    .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting model provider mapping with ID {MappingId}", id);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<Entities.ModelProviderMapping?> GetByModelNameAsync(string modelName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(modelName))
            {
                throw new ArgumentException("Model name cannot be null or empty", nameof(modelName));
            }

            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                return await dbContext.ModelProviderMappings
                    .AsNoTracking()
                    .FirstOrDefaultAsync(m => m.ModelAlias == modelName, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting model provider mapping for model {ModelName}", modelName);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<List<Entities.ModelProviderMapping>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                return await dbContext.ModelProviderMappings
                    .AsNoTracking()
                    .OrderBy(m => m.ModelAlias)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all model provider mappings");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<List<Entities.ModelProviderMapping>> GetByProviderAsync(string providerName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(providerName))
            {
                throw new ArgumentException("Provider name cannot be null or empty", nameof(providerName));
            }

            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                return await dbContext.ModelProviderMappings
                    .AsNoTracking()
                    // ProviderCredential relationship would need to be included and used here
                    // For now, just return all mappings
                    //.Where(m => m.ProviderCredential.ProviderName == providerName)
                    .OrderBy(m => m.ModelAlias)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting model provider mappings for provider {ProviderName}", providerName);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<int> CreateAsync(Entities.ModelProviderMapping modelProviderMapping, CancellationToken cancellationToken = default)
        {
            if (modelProviderMapping == null)
            {
                throw new ArgumentNullException(nameof(modelProviderMapping));
            }

            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                
                // Set timestamps
                modelProviderMapping.CreatedAt = DateTime.UtcNow;
                modelProviderMapping.UpdatedAt = DateTime.UtcNow;
                
                dbContext.ModelProviderMappings.Add(modelProviderMapping);
                await dbContext.SaveChangesAsync(cancellationToken);
                return modelProviderMapping.Id;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error creating model provider mapping for model '{ModelAlias}'", 
                    modelProviderMapping.ModelAlias);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating model provider mapping for model '{ModelAlias}'", 
                    modelProviderMapping.ModelAlias);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> UpdateAsync(Entities.ModelProviderMapping modelProviderMapping, CancellationToken cancellationToken = default)
        {
            if (modelProviderMapping == null)
            {
                throw new ArgumentNullException(nameof(modelProviderMapping));
            }

            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                
                // Set updated timestamp
                modelProviderMapping.UpdatedAt = DateTime.UtcNow;
                
                dbContext.ModelProviderMappings.Update(modelProviderMapping);
                int rowsAffected = await dbContext.SaveChangesAsync(cancellationToken);
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating model provider mapping with ID {MappingId}", 
                    modelProviderMapping.Id);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                var mapping = await dbContext.ModelProviderMappings.FindAsync(new object[] { id }, cancellationToken);
                
                if (mapping == null)
                {
                    return false;
                }
                
                dbContext.ModelProviderMappings.Remove(mapping);
                int rowsAffected = await dbContext.SaveChangesAsync(cancellationToken);
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting model provider mapping with ID {MappingId}", id);
                throw;
            }
        }
    }
}