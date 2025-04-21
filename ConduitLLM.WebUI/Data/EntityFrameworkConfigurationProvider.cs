using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

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

        // Create instances for both contexts using the same options builder
        // This assumes the DI setup correctly configures options for both
        using var webUiDbContext = new ConfigurationDbContext(builder.Options); 
        // Need options specifically for the Configuration context
        var configBuilder = new DbContextOptionsBuilder<ConduitLLM.Configuration.ConfigurationDbContext>();
        _optionsAction(configBuilder); // Apply the same configuration action
        using var configDbContext = new ConduitLLM.Configuration.ConfigurationDbContext(configBuilder.Options);

        // Ensure the database exists - might be redundant if EnsureCreated/Migrate is called elsewhere,
        // but safe to include here for direct provider usage scenarios.
        // Consider if this is needed based on application startup logic.
        // dbContext.Database.EnsureCreated();

        Data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        try
        {
            // Load Provider Credentials from the Config context
            var credentials = configDbContext.ProviderCredentials.ToList(); 
            for (int i = 0; i < credentials.Count; i++)
            {
                var cred = credentials[i];
                string prefix = $"{nameof(ConduitLLM.Configuration.ConduitSettings)}:{nameof(ConduitLLM.Configuration.ConduitSettings.ProviderCredentials)}:{i}";
                Data[$"{prefix}:{nameof(ConduitLLM.Configuration.Entities.ProviderCredential.ProviderName)}"] = cred.ProviderName;
                if (cred.ApiKey != null)
                    Data[$"{prefix}:{nameof(ConduitLLM.Configuration.Entities.ProviderCredential.ApiKey)}"] = cred.ApiKey;
                // Correct property name: BaseUrl instead of ApiBase
                if (cred.BaseUrl != null) 
                    Data[$"{prefix}:{nameof(ConduitLLM.Configuration.Entities.ProviderCredential.BaseUrl)}"] = cred.BaseUrl; 
                if (cred.ApiVersion != null)
                    Data[$"{prefix}:{nameof(ConduitLLM.Configuration.Entities.ProviderCredential.ApiVersion)}"] = cred.ApiVersion;
                // Remove access to non-existent 'Name' property
            }

            // Load Model Mappings from the WebUI context
            var mappings = webUiDbContext.ModelMappings.ToList(); 
            for (int i = 0; i < mappings.Count; i++)
            {
                var map = mappings[i];
                string prefix = $"{nameof(ConduitLLM.Configuration.ConduitSettings)}:{nameof(ConduitLLM.Configuration.ConduitSettings.ModelMappings)}:{i}";
                Data[$"{prefix}:{nameof(ConduitLLM.Configuration.ModelProviderMapping.ModelAlias)}"] = map.ModelAlias;
                Data[$"{prefix}:{nameof(ConduitLLM.Configuration.ModelProviderMapping.ProviderName)}"] = map.ProviderName;
                Data[$"{prefix}:{nameof(ConduitLLM.Configuration.ModelProviderMapping.ProviderModelId)}"] = map.ProviderModelId;
                // Add optional overrides here if they were implemented
            }

            // Note: DefaultTimeoutSeconds and DefaultRetries from ConduitSettings are not included here
            // as they are simple values. They could be added to a separate 'GlobalSettings' table
            // or managed via appsettings.json if preferred, as they are less likely to change dynamically.
            // If needed, they could be added like:
            // Data[$"{nameof(ConduitLLM.Configuration.ConduitSettings)}:{nameof(ConduitLLM.Configuration.ConduitSettings.DefaultTimeoutSeconds)}"] = globalSettings?.DefaultTimeoutSeconds?.ToString();

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
