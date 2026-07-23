using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using ConduitLLM.Configuration;
using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Default implementation of IProviderMetadataRegistry.
    /// Automatically discovers and registers provider metadata implementations.
    /// </summary>
    public class ProviderMetadataRegistry : IProviderMetadataRegistry
    {
        private readonly ConcurrentDictionary<ProviderType, IProviderMetadata> _providers;
        private readonly ILogger<ProviderMetadataRegistry> _logger;
        private readonly List<string> _registrationErrors;

        /// <summary>
        /// Initializes a new instance of the ProviderRegistry class.
        /// </summary>
        public ProviderMetadataRegistry(ILogger<ProviderMetadataRegistry> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _providers = new ConcurrentDictionary<ProviderType, IProviderMetadata>();
            _registrationErrors = new List<string>();
            
            // Discover and register all providers
            DiscoverAndRegisterProviders();
        }

        /// <inheritdoc />
        public IProviderMetadata GetMetadata(ProviderType providerType)
        {
            if (_providers.TryGetValue(providerType, out var metadata))
            {
                return metadata;
            }

            throw new ProviderNotFoundException(providerType);
        }

        /// <inheritdoc />
        public bool TryGetMetadata(ProviderType providerType, out IProviderMetadata? metadata)
        {
            return _providers.TryGetValue(providerType, out metadata);
        }

        /// <inheritdoc />
        public IEnumerable<IProviderMetadata> GetAllMetadata()
        {
            return _providers.Values.OrderBy(p => (int)p.ProviderType);
        }

        /// <inheritdoc />
        public bool IsRegistered(ProviderType providerType)
        {
            return _providers.ContainsKey(providerType);
        }

        /// <inheritdoc />
        public ProviderRegistryDiagnostics GetDiagnostics()
        {
            var diagnostics = new ProviderRegistryDiagnostics
            {
                TotalProviders = _providers.Count,
                RegisteredProviders = _providers.Keys
                    .OrderBy(p => (int)p)
                    .Select(p => p.ToString())
                    .ToList(),
                RegistrationErrors = new List<string>(_registrationErrors)
            };

            return diagnostics;
        }

        /// <summary>
        /// Discovers and registers all provider metadata implementations.
        /// </summary>
        private void DiscoverAndRegisterProviders()
        {
            _logger.LogInformation("Starting provider discovery and registration");

            try
            {
                // Get all types that implement IProviderMetadata
                var providerMetadataTypes = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => !a.IsDynamic && a.FullName != null && 
                               (a.FullName.StartsWith("ConduitLLM") || a.FullName.StartsWith("Extensions")))
                    .SelectMany(a =>
                    {
                        try
                        {
                            return a.GetTypes();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to get types from assembly {Assembly}", a.FullName);
                            return Array.Empty<Type>();
                        }
                    })
                    .Where(t => t.IsClass && !t.IsAbstract && 
                               typeof(IProviderMetadata).IsAssignableFrom(t))
                    .ToList();

                _logger.LogInformation("Found {Count} provider metadata implementations", 
                    providerMetadataTypes.Count);

                // Register each provider
                foreach (var type in providerMetadataTypes)
                {
                    try
                    {
                        // Create instance
                        var instance = Activator.CreateInstance(type) as IProviderMetadata;
                        if (instance == null)
                        {
                            var error = $"Failed to create instance of {type.Name}";
                            _logger.LogError(error);
                            _registrationErrors.Add(error);
                            continue;
                        }

                        // Register provider
                        if (_providers.TryAdd(instance.ProviderType, instance))
                        {
                            _logger.LogInformation("Registered provider {Provider} ({Type})", 
                                instance.DisplayName, instance.ProviderType);
                        }
                        else
                        {
                            var error = $"Duplicate registration attempted for {instance.ProviderType}";
                            _logger.LogWarning(error);
                            _registrationErrors.Add(error);
                        }
                    }
                    catch (Exception ex)
                    {
                        var error = $"Failed to register provider {type.Name}: {ex.Message}";
                        _logger.LogError(ex, "Failed to register provider {Type}", type.Name);
                        _registrationErrors.Add(error);
                    }
                }

                // Verify all enum values have implementations
                var missingProviders = Enum.GetValues<ProviderType>()
                    .Where(pt => !_providers.ContainsKey(pt))
                    .ToList();

                if (missingProviders.Any())
                {
                    var missing = string.Join(", ", missingProviders);
                    var error = $"Missing provider implementations for: {missing}";
                    _logger.LogWarning(error);
                    _registrationErrors.Add(error);
                }

                _logger.LogInformation("Provider registration completed. Registered {Count} of {Total} providers",
                    _providers.Count, Enum.GetValues<ProviderType>().Length);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error during provider discovery");
                _registrationErrors.Add($"Critical error: {ex.Message}");
            }
        }

    }
}
