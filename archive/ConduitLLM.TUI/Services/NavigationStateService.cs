using Microsoft.Extensions.Logging;
using ConduitLLM.TUI.Models;
using ConduitLLM.AdminClient.Models;

namespace ConduitLLM.TUI.Services;

public class NavigationStateService
{
    private readonly CoreApiService _coreApiService;
    private readonly AdminApiService _adminApiService;
    private readonly ILogger<NavigationStateService> _logger;
    private NavigationStateDto? _cachedState;
    private DateTime _lastRefresh = DateTime.MinValue;
    private readonly TimeSpan _cacheExpiry = TimeSpan.FromMinutes(5);

    public NavigationStateService(
        CoreApiService coreApiService,
        AdminApiService adminApiService,
        ILogger<NavigationStateService> logger)
    {
        _coreApiService = coreApiService;
        _adminApiService = adminApiService;
        _logger = logger;
    }

    /// <summary>
    /// Gets the current navigation state, loading it if necessary.
    /// </summary>
    /// <param name="forceRefresh">Whether to force a refresh even if cache is valid.</param>
    /// <returns>The navigation state with UI section availability and provider details.</returns>
    public async Task<NavigationStateDto> GetNavigationStateAsync(bool forceRefresh = false)
    {
        try
        {
            // Check if we can use cached state
            if (!forceRefresh && _cachedState != null && DateTime.UtcNow - _lastRefresh < _cacheExpiry)
            {
                _logger.LogDebug("Returning cached navigation state");
                return _cachedState;
            }

            _logger.LogInformation("Loading navigation state from APIs");

            // Load data from both APIs
            var (providers, modelMappings, modelCapabilities, providerHealth) = await LoadDataFromAPIsAsync();

            // Build the navigation state
            var navigationState = BuildNavigationState(providers, modelMappings, modelCapabilities, providerHealth);

            // Cache the result
            _cachedState = navigationState;
            _lastRefresh = DateTime.UtcNow;

            _logger.LogInformation("Navigation state loaded successfully. Sections available: Chat={Chat}, Images={Images}, Video={Video}, Audio={Audio}, Embeddings={Embeddings}",
                navigationState.Sections.Chat, navigationState.Sections.Images, navigationState.Sections.Video, 
                navigationState.Sections.Audio, navigationState.Sections.Embeddings);

            return navigationState;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load navigation state");
            
            // Return a fallback state with error information
            return new NavigationStateDto
            {
                ErrorMessage = $"Failed to load navigation state: {ex.Message}",
                LastRefreshed = DateTime.UtcNow,
                Sections = new UISectionAvailability(), // All false by default
                ProviderDetails = new List<ProviderNavigationInfo>()
            };
        }
    }

    /// <summary>
    /// Invalidates the cached navigation state, forcing a refresh on next access.
    /// </summary>
    public void InvalidateCache()
    {
        _logger.LogDebug("Navigation state cache invalidated");
        _cachedState = null;
        _lastRefresh = DateTime.MinValue;
    }

    private async Task<(List<ProviderCredentialDto> providers, List<ModelProviderMappingDto> modelMappings, 
        Dictionary<string, List<ModelCapabilityDto>> modelCapabilities, Dictionary<string, ProviderHealthStatus> providerHealth)> 
        LoadDataFromAPIsAsync()
    {
        var providers = new List<ProviderCredentialDto>();
        var modelMappings = new List<ModelProviderMappingDto>();
        var modelCapabilities = new Dictionary<string, List<ModelCapabilityDto>>();
        var providerHealth = new Dictionary<string, ProviderHealthStatus>();

        // Load providers and model mappings from Admin API
        try
        {
            providers = await _adminApiService.GetProvidersAsync();
            _logger.LogDebug("Loaded {Count} providers from Admin API", providers.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load providers from Admin API");
        }

        try
        {
            modelMappings = await _adminApiService.GetModelMappingsAsync();
            _logger.LogDebug("Loaded {Count} model mappings from Admin API", modelMappings.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load model mappings from Admin API");
        }

        // Load model capabilities from Admin API (discovery)
        try
        {
            modelCapabilities = await _adminApiService.DiscoverModelsAsync();
            _logger.LogDebug("Discovered models from {Count} providers", modelCapabilities.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to discover models from Admin API");
        }

        // TODO: Load provider health when available in SDK
        // For now, determine health based on provider enabled status
        foreach (var provider in providers)
        {
            providerHealth[provider.ProviderName] = provider.IsEnabled 
                ? ProviderHealthStatus.Healthy 
                : ProviderHealthStatus.Unknown;
        }

        return (providers, modelMappings, modelCapabilities, providerHealth);
    }

    private NavigationStateDto BuildNavigationState(
        List<ProviderCredentialDto> providers,
        List<ModelProviderMappingDto> modelMappings,
        Dictionary<string, List<ModelCapabilityDto>> modelCapabilities,
        Dictionary<string, ProviderHealthStatus> providerHealth)
    {
        var navigationState = new NavigationStateDto
        {
            LastRefreshed = DateTime.UtcNow
        };

        // Build provider details
        navigationState.ProviderDetails = BuildProviderDetails(providers, modelMappings, modelCapabilities, providerHealth);

        // Determine UI section availability based on enabled models and their capabilities
        navigationState.Sections = DetermineUISectionAvailability(navigationState.ProviderDetails);

        // Build legacy lists for backward compatibility
        navigationState.Providers = providers.Select(p => p.ProviderName).ToList();
        navigationState.Models = modelMappings.Where(m => m.IsEnabled).Select(m => m.ModelId).ToList();

        return navigationState;
    }

    private List<ProviderNavigationInfo> BuildProviderDetails(
        List<ProviderCredentialDto> providers,
        List<ModelProviderMappingDto> modelMappings,
        Dictionary<string, List<ModelCapabilityDto>> modelCapabilities,
        Dictionary<string, ProviderHealthStatus> providerHealth)
    {
        var providerDetails = new List<ProviderNavigationInfo>();

        foreach (var provider in providers)
        {
            var providerInfo = new ProviderNavigationInfo
            {
                Id = provider.Id,
                Name = provider.ProviderName,
                IsEnabled = provider.IsEnabled,
                HealthStatus = providerHealth.GetValueOrDefault(provider.ProviderName, ProviderHealthStatus.Unknown)
            };

            // Get models for this provider  
            // ProviderId in the mapping is actually the provider name, not numeric ID
            var providerMappings = modelMappings.Where(m => m.ProviderId == provider.ProviderName).ToList();
            var providerCapabilities = modelCapabilities.GetValueOrDefault(provider.ProviderName, new List<ModelCapabilityDto>());

            foreach (var mapping in providerMappings)
            {
                // Find capabilities for this model (using ProviderModelId for the model name on provider side)
                var modelCapability = providerCapabilities.FirstOrDefault(c => c.ModelId == mapping.ProviderModelId);
                
                var modelInfo = new ModelNavigationInfo
                {
                    ModelId = mapping.ProviderModelId,
                    Alias = mapping.ModelId, // ModelId in mapping is actually the alias
                    IsEnabled = mapping.IsEnabled,
                    IsAvailable = modelCapability?.IsAvailable ?? false,
                    Capabilities = modelCapability?.Capabilities ?? new List<string>()
                };

                providerInfo.Models.Add(modelInfo);
            }

            // Add models that were discovered but don't have mappings
            foreach (var capability in providerCapabilities)
            {
                if (!providerInfo.Models.Any(m => m.ModelId == capability.ModelId))
                {
                    var modelInfo = new ModelNavigationInfo
                    {
                        ModelId = capability.ModelId,
                        IsEnabled = false, // No mapping means not enabled
                        IsAvailable = capability.IsAvailable,
                        Capabilities = capability.Capabilities
                    };

                    providerInfo.Models.Add(modelInfo);
                }
            }

            providerDetails.Add(providerInfo);
        }

        return providerDetails;
    }

    private UISectionAvailability DetermineUISectionAvailability(List<ProviderNavigationInfo> providerDetails)
    {
        var availability = new UISectionAvailability();

        // Get all enabled models from enabled providers
        var enabledModels = providerDetails
            .Where(p => p.IsEnabled)
            .SelectMany(p => p.Models)
            .Where(m => m.IsEnabled && m.IsAvailable)
            .ToList();

        if (enabledModels.Any())
        {
            var allCapabilities = enabledModels.SelectMany(m => m.Capabilities).ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Determine section availability based on capabilities
            availability.Chat = HasChatCapability(allCapabilities);
            availability.Embeddings = HasEmbeddingsCapability(allCapabilities);
            availability.Images = HasImageGenerationCapability(allCapabilities);
            availability.Video = HasVideoGenerationCapability(allCapabilities);
            availability.Audio = HasAudioCapability(allCapabilities);
        }

        return availability;
    }

    private static bool HasChatCapability(HashSet<string> capabilities)
    {
        return capabilities.Any(c => 
            c.Equals("Chat", StringComparison.OrdinalIgnoreCase) ||
            c.Equals("ChatCompletion", StringComparison.OrdinalIgnoreCase) ||
            c.Equals("TextGeneration", StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasEmbeddingsCapability(HashSet<string> capabilities)
    {
        return capabilities.Any(c => 
            c.Equals("Embeddings", StringComparison.OrdinalIgnoreCase) ||
            c.Equals("TextEmbedding", StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasImageGenerationCapability(HashSet<string> capabilities)
    {
        return capabilities.Any(c => 
            c.Equals("ImageGeneration", StringComparison.OrdinalIgnoreCase) ||
            c.Equals("Images", StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasVideoGenerationCapability(HashSet<string> capabilities)
    {
        return capabilities.Any(c => 
            c.Equals("VideoGeneration", StringComparison.OrdinalIgnoreCase) ||
            c.Equals("Video", StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasAudioCapability(HashSet<string> capabilities)
    {
        return capabilities.Any(c => 
            c.Equals("AudioTranscription", StringComparison.OrdinalIgnoreCase) ||
            c.Equals("TextToSpeech", StringComparison.OrdinalIgnoreCase) ||
            c.Equals("Audio", StringComparison.OrdinalIgnoreCase) ||
            c.Equals("Speech", StringComparison.OrdinalIgnoreCase));
    }
}