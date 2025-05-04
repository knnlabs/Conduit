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
    /// Repository implementation for router configurations using Entity Framework Core
    /// </summary>
    public class RouterConfigRepository : IRouterConfigRepository
    {
        private readonly IDbContextFactory<ConfigurationDbContext> _dbContextFactory;
        private readonly ILogger<RouterConfigRepository> _logger;

        /// <summary>
        /// Creates a new instance of the repository
        /// </summary>
        /// <param name="dbContextFactory">The database context factory</param>
        /// <param name="logger">The logger</param>
        public RouterConfigRepository(
            IDbContextFactory<ConfigurationDbContext> dbContextFactory,
            ILogger<RouterConfigRepository> logger)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public async Task<RouterConfigEntity?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                return await dbContext.RouterConfigs
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting router configuration with ID {ConfigId}", id);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<RouterConfigEntity?> GetActiveConfigAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                return await dbContext.RouterConfigs
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r => r.IsActive, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active router configuration");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<List<RouterConfigEntity>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                return await dbContext.RouterConfigs
                    .AsNoTracking()
                    .OrderByDescending(r => r.IsActive)
                    .ThenByDescending(r => r.UpdatedAt)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all router configurations");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<int> CreateAsync(RouterConfigEntity routerConfig, CancellationToken cancellationToken = default)
        {
            if (routerConfig == null)
            {
                throw new ArgumentNullException(nameof(routerConfig));
            }

            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                
                // Set timestamps
                routerConfig.CreatedAt = DateTime.UtcNow;
                routerConfig.UpdatedAt = DateTime.UtcNow;
                
                if (routerConfig.IsActive)
                {
                    // Deactivate all other configs
                    var activeConfigs = await dbContext.RouterConfigs
                        .Where(r => r.IsActive)
                        .ToListAsync(cancellationToken);
                        
                    foreach (var config in activeConfigs)
                    {
                        config.IsActive = false;
                    }
                }
                
                dbContext.RouterConfigs.Add(routerConfig);
                await dbContext.SaveChangesAsync(cancellationToken);
                return routerConfig.Id;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error creating router configuration '{ConfigName}'", 
                    routerConfig.Name);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating router configuration '{ConfigName}'", 
                    routerConfig.Name);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> UpdateAsync(RouterConfigEntity routerConfig, CancellationToken cancellationToken = default)
        {
            if (routerConfig == null)
            {
                throw new ArgumentNullException(nameof(routerConfig));
            }

            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                
                // Set updated timestamp
                routerConfig.UpdatedAt = DateTime.UtcNow;
                
                if (routerConfig.IsActive)
                {
                    // Deactivate all other configs
                    var activeConfigs = await dbContext.RouterConfigs
                        .Where(r => r.IsActive && r.Id != routerConfig.Id)
                        .ToListAsync(cancellationToken);
                        
                    foreach (var config in activeConfigs)
                    {
                        config.IsActive = false;
                    }
                }
                
                dbContext.RouterConfigs.Update(routerConfig);
                int rowsAffected = await dbContext.SaveChangesAsync(cancellationToken);
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating router configuration with ID {ConfigId}", 
                    routerConfig.Id);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> ActivateAsync(int id, CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                
                var routerConfig = await dbContext.RouterConfigs.FindAsync(new object[] { id }, cancellationToken);
                if (routerConfig == null)
                {
                    return false;
                }
                
                // Deactivate all configs
                var configs = await dbContext.RouterConfigs.ToListAsync(cancellationToken);
                foreach (var config in configs)
                {
                    config.IsActive = (config.Id == id);
                    config.UpdatedAt = DateTime.UtcNow;
                }
                
                int rowsAffected = await dbContext.SaveChangesAsync(cancellationToken);
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error activating router configuration with ID {ConfigId}", id);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                var routerConfig = await dbContext.RouterConfigs.FindAsync(new object[] { id }, cancellationToken);
                
                if (routerConfig == null)
                {
                    return false;
                }
                
                if (routerConfig.IsActive)
                {
                    _logger.LogWarning("Attempting to delete active router configuration {ConfigId}", id);
                    // You might want to prevent this or activate another config
                }
                
                dbContext.RouterConfigs.Remove(routerConfig);
                int rowsAffected = await dbContext.SaveChangesAsync(cancellationToken);
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting router configuration with ID {ConfigId}", id);
                throw;
            }
        }
    }
}