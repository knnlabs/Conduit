using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models.Routing;
using ConduitLLM.WebUI.Data;
using ConduitLLM.WebUI.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ConduitLLM.WebUI.Services
{
    /// <summary>
    /// Database repository for router configuration
    /// </summary>
    public class DbRouterConfigRepository : IRouterConfigRepository
    {
        private readonly IDbContextFactory<ConfigurationDbContext> _dbContextFactory;
        private readonly ILogger<DbRouterConfigRepository> _logger;

        /// <summary>
        /// Creates a new instance of DbRouterConfigRepository
        /// </summary>
        public DbRouterConfigRepository(
            IDbContextFactory<ConfigurationDbContext> dbContextFactory,
            ILogger<DbRouterConfigRepository> logger)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public async Task<RouterConfig?> GetRouterConfigAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                
                // Get the most recent router configuration
                var dbConfig = await dbContext.RouterConfigurations
                    .Include(r => r.ModelDeployments)
                    .Include(r => r.FallbackConfigurations)
                        .ThenInclude(f => f.FallbackMappings)
                    .OrderByDescending(r => r.LastUpdated)
                    .FirstOrDefaultAsync(cancellationToken);
                
                if (dbConfig == null)
                {
                    // Return a default configuration if none exists
                    return new RouterConfig
                    {
                        DefaultRoutingStrategy = "simple",
                        MaxRetries = 3,
                        RetryBaseDelayMs = 500,
                        RetryMaxDelayMs = 5000,
                        ModelDeployments = new List<ModelDeployment>(),
                        FallbackConfigurations = new List<FallbackConfiguration>(),
                        FallbacksEnabled = false
                    };
                }
                
                // Map the database entity to the router configuration model
                var config = new RouterConfig
                {
                    DefaultRoutingStrategy = dbConfig.DefaultRoutingStrategy,
                    MaxRetries = dbConfig.MaxRetries,
                    RetryBaseDelayMs = dbConfig.RetryBaseDelayMs,
                    RetryMaxDelayMs = dbConfig.RetryMaxDelayMs,
                    FallbacksEnabled = dbConfig.FallbacksEnabled,
                    ModelDeployments = dbConfig.ModelDeployments.Select(MapToModelDeployment).ToList(),
                    FallbackConfigurations = dbConfig.FallbackConfigurations.Select(MapToFallbackConfiguration).ToList()
                };
                
                // Also set up the Fallbacks dictionary for backward compatibility
                foreach (var fallback in dbConfig.FallbackConfigurations)
                {
                    var primaryDeployment = dbConfig.ModelDeployments
                        .FirstOrDefault(m => m.Id == fallback.PrimaryModelDeploymentId);
                    
                    if (primaryDeployment != null)
                    {
                        var fallbackDeploymentIds = fallback.FallbackMappings
                            .OrderBy(m => m.Order)
                            .Select(m => m.ModelDeploymentId)
                            .ToList();
                            
                        var fallbackDeployments = dbConfig.ModelDeployments
                            .Where(m => fallbackDeploymentIds.Contains(m.Id))
                            .ToList();
                            
                        if (fallbackDeployments.Any())
                        {
                            config.Fallbacks[primaryDeployment.ModelName] = fallbackDeployments
                                .Select(d => d.ModelName)
                                .ToList();
                        }
                    }
                }
                
                return config;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving router configuration from database");
                return null;
            }
        }

        /// <inheritdoc/>
        public async Task SaveRouterConfigAsync(RouterConfig config, CancellationToken cancellationToken = default)
        {
            try
            {
                await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                
                // Start a transaction to ensure consistency
                await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
                
                try
                {
                    // Create a new router configuration
                    var dbConfig = new RouterConfigEntity
                    {
                        DefaultRoutingStrategy = config.DefaultRoutingStrategy,
                        MaxRetries = config.MaxRetries,
                        RetryBaseDelayMs = config.RetryBaseDelayMs,
                        RetryMaxDelayMs = config.RetryMaxDelayMs,
                        FallbacksEnabled = config.FallbacksEnabled,
                        LastUpdated = DateTime.UtcNow
                    };
                    
                    // Add model deployments
                    foreach (var deployment in config.ModelDeployments)
                    {
                        dbConfig.ModelDeployments.Add(MapToModelDeploymentEntity(deployment));
                    }
                    
                    // Add fallback configurations
                    if (config.FallbackConfigurations != null)
                    {
                        foreach (var fallback in config.FallbackConfigurations)
                        {
                            var dbFallback = MapToFallbackConfigurationEntity(fallback);
                            
                            // Add fallback model mappings
                            for (int i = 0; i < fallback.FallbackModelDeploymentIds.Count; i++)
                            {
                                var fallbackModelId = Guid.Parse(fallback.FallbackModelDeploymentIds[i]);
                                
                                dbFallback.FallbackMappings.Add(new FallbackModelMappingEntity
                                {
                                    ModelDeploymentId = fallbackModelId,
                                    Order = i
                                });
                            }
                            
                            dbConfig.FallbackConfigurations.Add(dbFallback);
                        }
                    }
                    
                    // Add the new configuration to the database
                    dbContext.RouterConfigurations.Add(dbConfig);
                    await dbContext.SaveChangesAsync(cancellationToken);
                    
                    // Commit the transaction
                    await transaction.CommitAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    // Rollback the transaction on error
                    await transaction.RollbackAsync(cancellationToken);
                    throw new Exception("Error saving router configuration to database", ex);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving router configuration to database");
                throw;
            }
        }
        
        /// <summary>
        /// Maps a model deployment entity to a model deployment model
        /// </summary>
        private static ModelDeployment MapToModelDeployment(ModelDeploymentEntity entity)
        {
            return new ModelDeployment
            {
                Id = entity.Id,
                ModelName = entity.ModelName,
                ProviderName = entity.ProviderName,
                Weight = entity.Weight,
                HealthCheckEnabled = entity.HealthCheckEnabled,
                IsEnabled = entity.IsEnabled,
                RPM = entity.RPM,
                TPM = entity.TPM,
                InputTokenCostPer1K = entity.InputTokenCostPer1K,
                OutputTokenCostPer1K = entity.OutputTokenCostPer1K,
                Priority = entity.Priority,
                IsHealthy = entity.IsHealthy
            };
        }
        
        /// <summary>
        /// Maps a model deployment model to a model deployment entity
        /// </summary>
        private static ModelDeploymentEntity MapToModelDeploymentEntity(ModelDeployment model)
        {
            return new ModelDeploymentEntity
            {
                Id = model.Id,
                ModelName = model.ModelName,
                ProviderName = model.ProviderName,
                Weight = model.Weight,
                HealthCheckEnabled = model.HealthCheckEnabled,
                IsEnabled = model.IsEnabled,
                RPM = model.RPM,
                TPM = model.TPM,
                InputTokenCostPer1K = model.InputTokenCostPer1K,
                OutputTokenCostPer1K = model.OutputTokenCostPer1K,
                Priority = model.Priority,
                IsHealthy = model.IsHealthy,
                LastUpdated = DateTime.UtcNow
            };
        }
        
        /// <summary>
        /// Maps a fallback configuration entity to a fallback configuration model
        /// </summary>
        private static FallbackConfiguration MapToFallbackConfiguration(FallbackConfigurationEntity entity)
        {
            return new FallbackConfiguration
            {
                Id = entity.Id,
                PrimaryModelDeploymentId = entity.PrimaryModelDeploymentId.ToString(),
                FallbackModelDeploymentIds = entity.FallbackMappings
                    .OrderBy(m => m.Order)
                    .Select(m => m.ModelDeploymentId.ToString())
                    .ToList()
            };
        }
        
        /// <summary>
        /// Maps a fallback configuration model to a fallback configuration entity
        /// </summary>
        private static FallbackConfigurationEntity MapToFallbackConfigurationEntity(FallbackConfiguration model)
        {
            return new FallbackConfigurationEntity
            {
                Id = model.Id,
                PrimaryModelDeploymentId = Guid.Parse(model.PrimaryModelDeploymentId),
                FallbackMappings = new List<FallbackModelMappingEntity>(),
                LastUpdated = DateTime.UtcNow
            };
        }
    }
}
