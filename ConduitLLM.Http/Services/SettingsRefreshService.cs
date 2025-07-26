using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Data;
using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Http.Services
{
    /// <summary>
    /// Service for refreshing in-memory settings from the database
    /// Thread-safe implementation for runtime configuration updates
    /// </summary>
    public class SettingsRefreshService : ISettingsRefreshService
    {
        private readonly IDbContextFactory<ConfigurationDbContext> _dbContextFactory;
        private readonly IOptionsMonitor<ConduitSettings> _settingsMonitor;
        private readonly ILogger<SettingsRefreshService> _logger;
        private readonly SemaphoreSlim _refreshLock = new(1, 1);

        public SettingsRefreshService(
            IDbContextFactory<ConfigurationDbContext> dbContextFactory,
            IOptionsMonitor<ConduitSettings> settingsMonitor,
            ILogger<SettingsRefreshService> logger)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
            _settingsMonitor = settingsMonitor ?? throw new ArgumentNullException(nameof(settingsMonitor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task RefreshModelMappingsAsync()
        {
            await _refreshLock.WaitAsync();
            try
            {
                _logger.LogInformation("Refreshing model mappings from database");
                
                await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
                var settings = _settingsMonitor.CurrentValue;

                // Load model mappings with provider information
                var modelMappingsEntities = await dbContext.ModelProviderMappings
                    .Include(m => m.ProviderCredential)
                    .Where(m => m.IsEnabled)
                    .ToListAsync();

                if (modelMappingsEntities.Any())
                {
                    _logger.LogInformation("Found {Count} enabled model mappings in database", modelMappingsEntities.Count);

                    // Convert database entities to configuration models
                    var modelMappingsList = modelMappingsEntities.Select(m => new ConduitLLM.Configuration.ModelProviderMapping
                    {
                        ModelAlias = m.ModelAlias,
                        ProviderId = m.ProviderCredentialId,
                        ProviderType = m.ProviderCredential.ProviderType,
                        ProviderModelId = m.ProviderModelName
                    }).ToList();

                    // Thread-safe update of settings
                    lock (settings)
                    {
                        settings.ModelMappings = modelMappingsList;
                    }

                    foreach (var mapping in modelMappingsList)
                    {
                        _logger.LogInformation("Refreshed model mapping: {ModelAlias} -> {ProviderType}/{ProviderModelId}",
                            mapping.ModelAlias, mapping.ProviderType, mapping.ProviderModelId);
                    }
                }
                else
                {
                    _logger.LogWarning("No enabled model mappings found in database");
                    lock (settings)
                    {
                        settings.ModelMappings = new List<ConduitLLM.Configuration.ModelProviderMapping>();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh model mappings from database");
                throw;
            }
            finally
            {
                _refreshLock.Release();
            }
        }

        /// <inheritdoc />
        public async Task RefreshProviderCredentialsAsync()
        {
            await _refreshLock.WaitAsync();
            try
            {
                _logger.LogInformation("Refreshing provider credentials from database");
                
                await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
                var settings = _settingsMonitor.CurrentValue;

                // Load enabled provider credentials with their keys
                var providerCredsList = await dbContext.ProviderCredentials
                    .Include(p => p.ProviderKeyCredentials)
                    .Where(p => p.IsEnabled)
                    .ToListAsync();

                if (providerCredsList.Any())
                {
                    _logger.LogInformation("Found {Count} enabled provider credentials in database", providerCredsList.Count);

                    // Convert database entities to configuration models
                    var providersList = providerCredsList.Select(p => 
                    {
                        // Get the primary key or first enabled key
                        var primaryKey = p.ProviderKeyCredentials?
                            .FirstOrDefault(k => k.IsPrimary && k.IsEnabled) ??
                            p.ProviderKeyCredentials?.FirstOrDefault(k => k.IsEnabled);
                        
                        return new ProviderCredentials
                        {
                            ProviderType = p.ProviderType,
                            ApiKey = primaryKey?.ApiKey ?? string.Empty,
                            BaseUrl = p.BaseUrl
                        };
                    }).ToList();

                    // Thread-safe update of settings
                    lock (settings)
                    {
                        settings.ProviderCredentials = providersList;
                    }

                    foreach (var cred in providersList)
                    {
                        _logger.LogInformation("Refreshed credentials for provider: {ProviderType}", cred.ProviderType);
                    }
                }
                else
                {
                    _logger.LogWarning("No enabled provider credentials found in database");
                    lock (settings)
                    {
                        settings.ProviderCredentials = new List<ProviderCredentials>();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh provider credentials from database");
                throw;
            }
            finally
            {
                _refreshLock.Release();
            }
        }

        /// <inheritdoc />
        public async Task RefreshAllSettingsAsync()
        {
            _logger.LogInformation("Refreshing all settings from database");
            
            // Refresh both provider credentials and model mappings
            await RefreshProviderCredentialsAsync();
            await RefreshModelMappingsAsync();
            
            _logger.LogInformation("All settings refreshed successfully");
        }
    }
}