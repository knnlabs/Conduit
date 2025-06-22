using System.Collections.Concurrent;

using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.WebUI.Models;

namespace ConduitLLM.WebUI.Services
{
    /// <summary>
    /// Service implementation for managing navigation item states based on system prerequisites.
    /// </summary>
    public class NavigationStateService : INavigationStateService, IDisposable
    {
        private readonly IAdminApiClient _adminApiClient;
        private readonly IConduitApiClient _conduitApiClient;
        private readonly ILogger<NavigationStateService> _logger;
        private readonly ConcurrentDictionary<string, NavigationItemState> _stateCache;
        private readonly SemaphoreSlim _refreshSemaphore;
        private readonly Timer _refreshTimer;
        private DateTime _lastRefresh;
        private bool _disposed;

        /// <summary>
        /// Event raised when any navigation state changes.
        /// </summary>
        public event EventHandler<NavigationStateChangedEventArgs>? NavigationStateChanged;

        /// <summary>
        /// Initializes a new instance of the <see cref="NavigationStateService"/> class.
        /// </summary>
        /// <param name="adminApiClient">The admin API client for checking prerequisites.</param>
        /// <param name="conduitApiClient">The conduit API client for discovery API access.</param>
        /// <param name="logger">The logger instance.</param>
        public NavigationStateService(IAdminApiClient adminApiClient, IConduitApiClient conduitApiClient, ILogger<NavigationStateService> logger)
        {
            _adminApiClient = adminApiClient ?? throw new ArgumentNullException(nameof(adminApiClient));
            _conduitApiClient = conduitApiClient ?? throw new ArgumentNullException(nameof(conduitApiClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _stateCache = new ConcurrentDictionary<string, NavigationItemState>();
            _refreshSemaphore = new SemaphoreSlim(1, 1);
            _lastRefresh = DateTime.MinValue;

            // Set up periodic refresh every 30 seconds
            _refreshTimer = new Timer(async _ => await RefreshStatesAsync(), null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        }

        /// <summary>
        /// Gets the state of a specific navigation item.
        /// </summary>
        /// <param name="route">The route of the navigation item.</param>
        /// <returns>The state of the navigation item.</returns>
        public async Task<NavigationItemState> GetNavigationItemStateAsync(string route)
        {
            // Check if we need to refresh (cache for 10 seconds)
            if (DateTime.UtcNow - _lastRefresh > TimeSpan.FromSeconds(10))
            {
                await RefreshStatesAsync();
            }

            return _stateCache.GetValueOrDefault(route, new NavigationItemState { IsEnabled = true });
        }

        /// <summary>
        /// Gets the states of all navigation items.
        /// </summary>
        /// <returns>A dictionary mapping routes to their states.</returns>
        public async Task<Dictionary<string, NavigationItemState>> GetAllNavigationStatesAsync()
        {
            // Check if we need to refresh
            if (DateTime.UtcNow - _lastRefresh > TimeSpan.FromSeconds(10))
            {
                await RefreshStatesAsync();
            }

            return new Dictionary<string, NavigationItemState>(_stateCache);
        }

        /// <summary>
        /// Gets detailed capability status information for diagnostics.
        /// </summary>
        /// <returns>Detailed capability status information.</returns>
        public async Task<CapabilityStatusInfo> GetCapabilityStatusAsync()
        {
            try
            {
                var mappings = await _adminApiClient.GetAllModelProviderMappingsAsync();
                var status = new CapabilityStatusInfo();

                if (mappings?.Any() == true)
                {
                    status.TotalConfiguredModels = mappings.Count(m => m.IsEnabled);
                    status.ImageGenerationModels = mappings.Count(m => m.IsEnabled && m.SupportsImageGeneration);
                    status.VisionModels = mappings.Count(m => m.IsEnabled && m.SupportsVision);
                    status.AudioTranscriptionModels = mappings.Count(m => m.IsEnabled && m.SupportsAudioTranscription);
                    status.TextToSpeechModels = mappings.Count(m => m.IsEnabled && m.SupportsTextToSpeech);
                    status.RealtimeAudioModels = mappings.Count(m => m.IsEnabled && m.SupportsRealtimeAudio);

                    status.ConfiguredModels = mappings
                        .Where(m => m.IsEnabled)
                        .Select(m => new ModelCapabilityInfo
                        {
                            ModelId = m.ModelId,
                            ProviderId = m.ProviderId,
                            SupportsImageGeneration = m.SupportsImageGeneration,
                            SupportsVision = m.SupportsVision,
                            SupportsAudioTranscription = m.SupportsAudioTranscription,
                            SupportsTextToSpeech = m.SupportsTextToSpeech,
                            SupportsRealtimeAudio = m.SupportsRealtimeAudio
                        })
                        .ToList();
                }

                return status;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting capability status");
                return new CapabilityStatusInfo { HasError = true, ErrorMessage = ex.Message };
            }
        }

        /// <summary>
        /// Forces a refresh of all navigation states.
        /// </summary>
        public async Task RefreshStatesAsync()
        {
            await _refreshSemaphore.WaitAsync();
            try
            {
                _logger.LogDebug("Refreshing navigation states");

                // Check prerequisites for each navigation item
                var tasks = new List<Task>();

                // Chat Interface prerequisites
                tasks.Add(CheckChatInterfacePrerequisitesAsync());

                // Audio Test prerequisites
                tasks.Add(CheckAudioTestPrerequisitesAsync());

                // Request Logs prerequisites (optional)
                tasks.Add(CheckRequestLogsPrerequisitesAsync());

                // Audio Usage prerequisites (optional)
                tasks.Add(CheckAudioUsagePrerequisitesAsync());

                // Audio Providers prerequisites
                tasks.Add(CheckAudioProvidersPrerequisitesAsync());

                // Image Generation prerequisites
                tasks.Add(CheckImageGenerationPrerequisitesAsync());

                await Task.WhenAll(tasks);

                _lastRefresh = DateTime.UtcNow;
                _logger.LogDebug("Navigation states refreshed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing navigation states");
            }
            finally
            {
                _refreshSemaphore.Release();
            }
        }

        private async Task CheckChatInterfacePrerequisitesAsync()
        {
            try
            {
                var mappings = await _adminApiClient.GetAllModelProviderMappingsAsync();
                var hasActiveMappings = mappings?.Any(m => m.IsEnabled) ?? false;

                var newState = new NavigationItemState
                {
                    IsEnabled = hasActiveMappings,
                    TooltipMessage = hasActiveMappings ? null : "Configure LLM providers and model mappings to use the chat interface",
                    RequiredConfigurationUrl = hasActiveMappings ? null : "/model-mappings",
                    ShowIndicator = !hasActiveMappings
                };

                UpdateState("/chat", newState);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking chat interface prerequisites");
            }
        }

        private async Task CheckAudioTestPrerequisitesAsync()
        {
            try
            {
                var audioProviders = await _adminApiClient.GetAudioProvidersAsync();
                var hasAudioProviders = audioProviders?.Any() ?? false;

                var newState = new NavigationItemState
                {
                    IsEnabled = hasAudioProviders,
                    TooltipMessage = hasAudioProviders ? null : "Configure audio providers with transcription, TTS, or realtime capabilities",
                    RequiredConfigurationUrl = hasAudioProviders ? null : "/audio-providers",
                    ShowIndicator = !hasAudioProviders
                };

                UpdateState("/audio-test", newState);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking audio test prerequisites");
            }
        }

        private async Task CheckRequestLogsPrerequisitesAsync()
        {
            try
            {
                // Request logs can work without prerequisites, but are more useful with data
                var summary = await _adminApiClient.GetLogsSummaryAsync(7); // Get last 7 days
                var hasLogs = summary?.TotalRequests > 0;

                var virtualKeys = await _adminApiClient.GetAllVirtualKeysAsync();
                var hasVirtualKeys = virtualKeys?.Any() ?? false;

                var newState = new NavigationItemState
                {
                    IsEnabled = true, // Always enabled, but show indicator if no data
                    TooltipMessage = !hasLogs ? "No API requests logged yet. Make some API calls to see request logs." : null,
                    ShowIndicator = !hasLogs && !hasVirtualKeys
                };

                UpdateState("/request-logs", newState);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking request logs prerequisites");
            }
        }

        private async Task CheckAudioUsagePrerequisitesAsync()
        {
            try
            {
                // Audio usage can work without prerequisites, but needs providers and usage
                var audioProviders = await _adminApiClient.GetAudioProvidersAsync();
                var hasAudioProviders = audioProviders?.Any() ?? false;

                var newState = new NavigationItemState
                {
                    IsEnabled = true, // Always enabled
                    TooltipMessage = !hasAudioProviders ? "Configure audio providers and use audio APIs to see usage data" : null,
                    RequiredConfigurationUrl = !hasAudioProviders ? "/audio-providers" : null,
                    ShowIndicator = !hasAudioProviders
                };

                UpdateState("/audio-usage", newState);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error checking audio usage prerequisites - API may not be available");
                
                // Set a safe default state when audio API is not available
                var fallbackState = new NavigationItemState
                {
                    IsEnabled = true, // Always enabled
                    TooltipMessage = "Audio usage monitoring - configure audio providers to see prerequisites",
                    RequiredConfigurationUrl = null,
                    ShowIndicator = false
                };
                
                UpdateState("/audio-usage", fallbackState);
            }
        }

        private async Task CheckAudioProvidersPrerequisitesAsync()
        {
            try
            {
                // Check if any LLM providers are configured
                var providers = await _adminApiClient.GetAllProviderCredentialsAsync();
                var hasLLMProviders = providers?.Any() ?? false;

                var newState = new NavigationItemState
                {
                    IsEnabled = hasLLMProviders,
                    TooltipMessage = hasLLMProviders ? null : "Configure LLM providers first to enable audio capabilities",
                    RequiredConfigurationUrl = hasLLMProviders ? null : "/llm-providers",
                    ShowIndicator = !hasLLMProviders
                };

                UpdateState("/audio-providers", newState);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking audio providers prerequisites");
            }
        }

        private async Task CheckImageGenerationPrerequisitesAsync()
        {
            try
            {
                // Primary: Check database-configured models via Admin API
                var mappings = await _adminApiClient.GetAllModelProviderMappingsAsync();
                var hasConfiguredImageModels = mappings?.Any(m => 
                    m.IsEnabled && 
                    m.SupportsImageGeneration) ?? false;

                if (hasConfiguredImageModels)
                {
                    var newState = new NavigationItemState
                    {
                        IsEnabled = true,
                        TooltipMessage = null,
                        RequiredConfigurationUrl = null,
                        ShowIndicator = false
                    };
                    UpdateState("/image-generation", newState);
                    return;
                }

                // Fallback: Check if any mapped models support image generation via Discovery API (using bulk API)
                var hasDiscoveredImageModels = false;
                if (mappings?.Any() == true)
                {
                    try
                    {
                        var enabledMappings = mappings.Where(m => m.IsEnabled).ToList();
                        if (enabledMappings.Any())
                        {
                            // Use bulk API to test all models at once
                            var capabilityTests = enabledMappings
                                .Select(m => (m.ModelId, "ImageGeneration"))
                                .ToList();
                            
                            var bulkResults = await _conduitApiClient.TestBulkModelCapabilitiesAsync(capabilityTests);
                            
                            // Check if any model supports image generation
                            foreach (var mapping in enabledMappings)
                            {
                                var key = $"{mapping.ModelId}:ImageGeneration";
                                if (bulkResults.TryGetValue(key, out var supportsImageGen) && supportsImageGen)
                                {
                                    hasDiscoveredImageModels = true;
                                    _logger.LogInformation("Discovered image generation capability for model: {Model}", mapping.ModelId);
                                    break;
                                }
                            }
                        }
                    }
                    catch (Exception discEx)
                    {
                        _logger.LogDebug(discEx, "Could not test bulk image generation capabilities, falling back to individual tests");
                        
                        // Fallback to individual API calls if bulk fails
                        foreach (var mapping in mappings.Where(m => m.IsEnabled))
                        {
                            try
                            {
                                var supportsImageGen = await _conduitApiClient.TestModelCapabilityAsync(
                                    mapping.ModelId, "ImageGeneration");
                                if (supportsImageGen)
                                {
                                    hasDiscoveredImageModels = true;
                                    _logger.LogInformation("Discovered image generation capability for model: {Model}", mapping.ModelId);
                                    break;
                                }
                            }
                            catch (Exception individualEx)
                            {
                                _logger.LogDebug(individualEx, "Could not test image generation capability for model: {Model}", mapping.ModelId);
                            }
                        }
                    }
                }

                // Generate detailed feedback based on discovery results
                string tooltipMessage;
                if (hasDiscoveredImageModels)
                {
                    tooltipMessage = "Image generation available (discovered via capability testing - consider updating model configuration)";
                }
                else if (mappings?.Any(m => m.IsEnabled) == true)
                {
                    tooltipMessage = $"No image generation models found among {mappings.Count(m => m.IsEnabled)} configured models. Add DALL-E, MiniMax, or Stable Diffusion models.";
                }
                else
                {
                    tooltipMessage = "No models configured. Add and configure LLM providers with image generation capabilities.";
                }

                var finalState = new NavigationItemState
                {
                    IsEnabled = hasDiscoveredImageModels,
                    TooltipMessage = tooltipMessage,
                    RequiredConfigurationUrl = hasDiscoveredImageModels ? null : "/model-mappings",
                    ShowIndicator = !hasDiscoveredImageModels
                };

                UpdateState("/image-generation", finalState);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking image generation prerequisites");
                
                // Set a safe default state when API is not available
                var fallbackState = new NavigationItemState
                {
                    IsEnabled = false,
                    TooltipMessage = "Unable to verify image generation models - check API connection",
                    ShowIndicator = true
                };
                
                UpdateState("/image-generation", fallbackState);
            }
        }

        private void UpdateState(string route, NavigationItemState newState)
        {
            var oldState = _stateCache.GetValueOrDefault(route);
            _stateCache[route] = newState;

            // Raise event if state changed
            if (oldState?.IsEnabled != newState.IsEnabled || oldState?.ShowIndicator != newState.ShowIndicator)
            {
                NavigationStateChanged?.Invoke(this, new NavigationStateChangedEventArgs(route, newState));
            }
        }

        /// <summary>
        /// Disposes the service and releases resources.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _refreshTimer?.Dispose();
                _refreshSemaphore?.Dispose();
                _disposed = true;
            }
        }
    }
}
