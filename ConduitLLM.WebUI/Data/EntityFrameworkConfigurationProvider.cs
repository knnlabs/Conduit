using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.WebUI.Data;

/// <summary>
/// Loads configuration values from the ConfigurationDbContext.
/// </summary>
public class EntityFrameworkConfigurationProvider : ConfigurationProvider
{
    private readonly Action<DbContextOptionsBuilder> _optionsAction;

    public EntityFrameworkConfigurationProvider(Action<DbContextOptionsBuilder> optionsAction)
    {
        _optionsAction = optionsAction;
    }

    /// <summary>
    /// Loads the configuration data from the database.
    /// </summary>
    public override void Load()
    {
        var builder = new DbContextOptionsBuilder<ConfigurationDbContext>();
        _optionsAction(builder);

        // Create a single instance of the consolidated ConfigurationDbContext
        using var dbContext = new ConfigurationDbContext(builder.Options);

        // Ensure the database exists - might be redundant if EnsureCreated/Migrate is called elsewhere,
        // but safe to include here for direct provider usage scenarios.
        // Consider if this is needed based on application startup logic.
        // dbContext.Database.EnsureCreated();

        Data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        try
        {
            // Load Provider Credentials from the consolidated context
            var credentials = dbContext.ProviderCredentials.ToList(); 
            for (int i = 0; i < credentials.Count; i++)
            {
                var cred = credentials[i];
                string prefix = $"{nameof(ConduitSettings)}:{nameof(ConduitSettings.ProviderCredentials)}:{i}";
                Data[$"{prefix}:{nameof(ProviderCredential.ProviderName)}"] = cred.ProviderName;
                if (cred.ApiKey != null)
                    Data[$"{prefix}:{nameof(ProviderCredential.ApiKey)}"] = cred.ApiKey;
                // Correct property name: BaseUrl instead of ApiBase
                if (cred.BaseUrl != null) 
                    Data[$"{prefix}:{nameof(ProviderCredential.BaseUrl)}"] = cred.BaseUrl; 
                if (cred.ApiVersion != null)
                    Data[$"{prefix}:{nameof(ProviderCredential.ApiVersion)}"] = cred.ApiVersion;
            }

            // Load Model Mappings from the consolidated context
            var mappings = dbContext.ModelProviderMappings
                .Include(m => m.ProviderCredential)
                .ToList(); 
                
            for (int i = 0; i < mappings.Count; i++)
            {
                var map = mappings[i];
                string prefix = $"{nameof(ConduitSettings)}:{nameof(ConduitSettings.ModelMappings)}:{i}";
                Data[$"{prefix}:{nameof(ConduitLLM.Configuration.ModelProviderMapping.ModelAlias)}"] = map.ModelAlias;
                Data[$"{prefix}:{nameof(ConduitLLM.Configuration.ModelProviderMapping.ProviderName)}"] = map.ProviderCredential.ProviderName;
                Data[$"{prefix}:{nameof(ConduitLLM.Configuration.ModelProviderMapping.ProviderModelId)}"] = map.ProviderModelName;
                // Add optional overrides here if they were implemented
            }

            // Note: DefaultTimeoutSeconds and DefaultRetries from ConduitSettings are not included here
            // as they are simple values. They could be added to a separate 'GlobalSettings' table
            // or managed via appsettings.json if preferred, as they are less likely to change dynamically.
            // If needed, they could be added like:
            // Data[$"{nameof(ConduitSettings)}:{nameof(ConduitSettings.DefaultTimeoutSeconds)}"] = globalSettings?.DefaultTimeoutSeconds?.ToString();

        }
        catch (Exception ex)
        {
            // Log the error appropriately
            Console.Error.WriteLine($"Error loading configuration from database: {ex.Message}");
            // Depending on requirements, you might want to throw, or continue with potentially empty/partial data.
            // For now, we continue, allowing appsettings.json to potentially provide defaults if chained.
        }
    }
}
