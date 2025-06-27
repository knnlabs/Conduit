using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR.Client;
using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.WebUI.Models;
using System.Text.Json;

namespace ConduitLLM.WebUI.Services
{
    /// <summary>
    /// SignalR-based implementation of navigation state service that receives real-time updates
    /// instead of polling. Falls back to polling if SignalR connection fails.
    /// </summary>
    public class SignalRNavigationStateService : INavigationStateService, IDisposable, IAsyncDisposable
    {
        private readonly IAdminApiClient _adminApiClient;
        private readonly IConduitApiClient _conduitApiClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<SignalRNavigationStateService> _logger;
        private readonly SignalRConnectionManager _signalRConnectionManager;
        private readonly ConcurrentDictionary<string, NavigationItemState> _stateCache;
        private readonly SemaphoreSlim _refreshSemaphore;
        private HubConnection? _hubConnection;
        private Timer? _reconnectTimer;
        private Timer? _fallbackPollingTimer;
        private DateTime _lastRefresh;
        private bool _disposed;
        private bool _usePollingFallback;
        private int _reconnectAttempts;
        private const int MaxReconnectAttempts = 5;
        private const int PollingIntervalSeconds = 30;

        /// <summary>
        /// Event raised when any navigation state changes.
        /// </summary>
        public event EventHandler<NavigationStateChangedEventArgs>? NavigationStateChanged;

        /// <summary>
        /// Initializes a new instance of the <see cref="SignalRNavigationStateService"/> class.
        /// </summary>
        public SignalRNavigationStateService(
            IAdminApiClient adminApiClient,
            IConduitApiClient conduitApiClient,
            IConfiguration configuration,
            ILogger<SignalRNavigationStateService> logger,
            SignalRConnectionManager signalRConnectionManager)
        {
            _adminApiClient = adminApiClient ?? throw new ArgumentNullException(nameof(adminApiClient));
            _conduitApiClient = conduitApiClient ?? throw new ArgumentNullException(nameof(conduitApiClient));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _signalRConnectionManager = signalRConnectionManager ?? throw new ArgumentNullException(nameof(signalRConnectionManager));
            _stateCache = new ConcurrentDictionary<string, NavigationItemState>();
            _refreshSemaphore = new SemaphoreSlim(1, 1);
            _lastRefresh = DateTime.MinValue;
            _usePollingFallback = false;
            _reconnectAttempts = 0;

            // Initialize SignalR connection
            _ = InitializeSignalRConnectionAsync();
        }

        private async Task InitializeSignalRConnectionAsync()
        {
            try
            {
                _logger.LogInformation("Initializing SignalR connection for navigation state");

                // Use centralized connection manager - no authentication needed for navigation state
                var connectionInfo = await _signalRConnectionManager.ConnectToHubAsync(
                    "navigation-state",
                    null, // No authentication required for navigation state
                    new HubConnectionOptions 
                    { 
                        EnableAutoReconnect = true,
                        LogLevel = _logger.IsEnabled(LogLevel.Debug) ? LogLevel.Debug : LogLevel.Information
                    });

                _hubConnection = connectionInfo.Connection;

                // Register event handlers
                RegisterSignalRHandlers();

                // Set up connection event handlers through SignalRConnectionManager
                _signalRConnectionManager.ConnectionStateChanged += OnConnectionStateChanged;
                
                _logger.LogInformation("SignalR connection established successfully");
                _reconnectAttempts = 0;
                
                // Perform initial state refresh
                await RefreshStatesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to establish SignalR connection");
                EnablePollingFallback();
            }
        }

        private void RegisterSignalRHandlers()
        {
            if (_hubConnection == null) return;

            // Handle navigation state updates
            _hubConnection.On<JsonElement>("NavigationStateUpdate", async (notification) =>
            {
                try
                {
                    var type = notification.GetProperty("type").GetString();
                    var data = notification.GetProperty("data");
                    
                    _logger.LogDebug("Received navigation state update: {Type}", type);

                    // Refresh states based on the type of update
                    switch (type)
                    {
                        case "ModelMappingChanged":
                            await RefreshChatAndImageStatesAsync();
                            break;
                        
                        case "ProviderHealthChanged":
                            await RefreshAllStatesAsync();
                            break;
                        
                        case "ModelCapabilitiesDiscovered":
                            await RefreshAudioAndImageStatesAsync();
                            break;
                        
                        case "ModelAvailabilityChanged":
                            await RefreshAllStatesAsync();
                            break;
                        
                        default:
                            _logger.LogWarning("Unknown navigation state update type: {Type}", type);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error handling navigation state update");
                }
            });

            // Handle model-specific updates
            _hubConnection.On<JsonElement>("ModelUpdate", async (notification) =>
            {
                try
                {
                    _logger.LogDebug("Received model update notification");
                    await RefreshAllStatesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error handling model update");
                }
            });
        }

        private Task OnConnectionClosed(Exception? exception)
        {
            if (_disposed) return Task.CompletedTask;

            if (exception != null)
            {
                _logger.LogError(exception, "SignalR connection closed with error");
            }
            else
            {
                _logger.LogWarning("SignalR connection closed");
            }

            _reconnectAttempts++;
            
            if (_reconnectAttempts >= MaxReconnectAttempts)
            {
                _logger.LogWarning("Max reconnection attempts reached, falling back to polling");
                EnablePollingFallback();
            }
            else
            {
                // Schedule reconnection attempt
                _reconnectTimer?.Dispose();
                _reconnectTimer = new Timer(async _ => await AttemptReconnectAsync(), null, 
                    TimeSpan.FromSeconds(Math.Pow(2, _reconnectAttempts)), // Exponential backoff
                    Timeout.InfiniteTimeSpan);
            }
            
            return Task.CompletedTask;
        }

        private Task OnReconnecting(Exception? exception)
        {
            _logger.LogInformation("SignalR connection reconnecting...");
            return Task.CompletedTask;
        }

        private async Task OnReconnected(string? connectionId)
        {
            _logger.LogInformation("SignalR connection reconnected with ID: {ConnectionId}", connectionId);
            _reconnectAttempts = 0;
            DisablePollingFallback();
            
            // Refresh all states after reconnection
            await RefreshStatesAsync();
        }

        private void OnConnectionStateChanged(object? sender, ConnectionStateChangedEventArgs e)
        {
            _logger.LogInformation("SignalR connection state changed to: {State}", e.CurrentState);
            
            switch (e.CurrentState)
            {
                case ConnectionState.Disconnected:
                    if (!string.IsNullOrEmpty(e.Error))
                    {
                        _logger.LogError("SignalR connection lost: {Error}", e.Error);
                        _ = OnConnectionClosed(new Exception(e.Error));
                    }
                    break;
                    
                case ConnectionState.Connected:
                    _reconnectAttempts = 0;
                    DisablePollingFallback();
                    _ = RefreshStatesAsync();
                    break;
                    
                case ConnectionState.Reconnecting:
                    _logger.LogInformation("SignalR connection reconnecting...");
                    break;
            }
        }

        private async Task AttemptReconnectAsync()
        {
            if (_disposed || _hubConnection == null) return;

            try
            {
                _logger.LogInformation("Attempting to reconnect SignalR connection (attempt {Attempt}/{Max})", 
                    _reconnectAttempts + 1, MaxReconnectAttempts);
                
                await _hubConnection.StartAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Reconnection attempt failed");
            }
        }

        private void EnablePollingFallback()
        {
            if (_usePollingFallback) return;

            _logger.LogWarning("Enabling polling fallback mode");
            _usePollingFallback = true;
            
            // Start polling timer
            _fallbackPollingTimer?.Dispose();
            _fallbackPollingTimer = new Timer(
                async _ => await RefreshStatesAsync(), 
                null, 
                TimeSpan.FromSeconds(PollingIntervalSeconds), 
                TimeSpan.FromSeconds(PollingIntervalSeconds));
        }

        private void DisablePollingFallback()
        {
            if (!_usePollingFallback) return;

            _logger.LogInformation("Disabling polling fallback mode, using real-time updates");
            _usePollingFallback = false;
            
            // Stop polling timer
            _fallbackPollingTimer?.Dispose();
            _fallbackPollingTimer = null;
        }

        /// <summary>
        /// Gets the state of a specific navigation item.
        /// </summary>
        public async Task<NavigationItemState> GetNavigationItemStateAsync(string route)
        {
            // In fallback mode or if cache is stale, refresh
            if (_usePollingFallback && DateTime.UtcNow - _lastRefresh > TimeSpan.FromSeconds(10))
            {
                await RefreshStatesAsync();
            }

            return _stateCache.GetValueOrDefault(route, new NavigationItemState { IsEnabled = true });
        }

        /// <summary>
        /// Gets the states of all navigation items.
        /// </summary>
        public async Task<Dictionary<string, NavigationItemState>> GetAllNavigationStatesAsync()
        {
            // In fallback mode or if cache is stale, refresh
            if (_usePollingFallback && DateTime.UtcNow - _lastRefresh > TimeSpan.FromSeconds(10))
            {
                await RefreshStatesAsync();
            }

            return new Dictionary<string, NavigationItemState>(_stateCache);
        }

        /// <summary>
        /// Gets detailed capability status information for diagnostics.
        /// </summary>
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
            await RefreshAllStatesAsync();
        }

        private async Task RefreshAllStatesAsync()
        {
            await _refreshSemaphore.WaitAsync();
            try
            {
                _logger.LogDebug("Refreshing all navigation states");

                var tasks = new List<Task>
                {
                    CheckChatInterfacePrerequisitesAsync(),
                    CheckAudioTestPrerequisitesAsync(),
                    CheckRequestLogsPrerequisitesAsync(),
                    CheckAudioUsagePrerequisitesAsync(),
                    CheckAudioProvidersPrerequisitesAsync(),
                    CheckImageGenerationPrerequisitesAsync(),
                    CheckVideoGenerationPrerequisitesAsync()
                };

                await Task.WhenAll(tasks);

                _lastRefresh = DateTime.UtcNow;
                _logger.LogDebug("All navigation states refreshed successfully");
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

        private async Task RefreshChatAndImageStatesAsync()
        {
            await _refreshSemaphore.WaitAsync();
            try
            {
                _logger.LogDebug("Refreshing chat and image generation states");

                await Task.WhenAll(
                    CheckChatInterfacePrerequisitesAsync(),
                    CheckImageGenerationPrerequisitesAsync()
                );

                _lastRefresh = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing chat and image states");
            }
            finally
            {
                _refreshSemaphore.Release();
            }
        }

        private async Task RefreshAudioAndImageStatesAsync()
        {
            await _refreshSemaphore.WaitAsync();
            try
            {
                _logger.LogDebug("Refreshing audio and image generation states");

                await Task.WhenAll(
                    CheckAudioTestPrerequisitesAsync(),
                    CheckAudioUsagePrerequisitesAsync(),
                    CheckAudioProvidersPrerequisitesAsync(),
                    CheckImageGenerationPrerequisitesAsync()
                );

                _lastRefresh = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing audio and image states");
            }
            finally
            {
                _refreshSemaphore.Release();
            }
        }

        // All the prerequisite checking methods remain the same as in the original NavigationStateService
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
                var summary = await _adminApiClient.GetLogsSummaryAsync(7);
                var hasLogs = summary?.TotalRequests > 0;

                var virtualKeys = await _adminApiClient.GetAllVirtualKeysAsync();
                var hasVirtualKeys = virtualKeys?.Any() ?? false;

                var newState = new NavigationItemState
                {
                    IsEnabled = true,
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
                var audioProviders = await _adminApiClient.GetAudioProvidersAsync();
                var hasAudioProviders = audioProviders?.Any() ?? false;

                var newState = new NavigationItemState
                {
                    IsEnabled = true,
                    TooltipMessage = !hasAudioProviders ? "Configure audio providers and use audio APIs to see usage data" : null,
                    RequiredConfigurationUrl = !hasAudioProviders ? "/audio-providers" : null,
                    ShowIndicator = !hasAudioProviders
                };

                UpdateState("/audio-usage", newState);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error checking audio usage prerequisites - API may not be available");
                
                var fallbackState = new NavigationItemState
                {
                    IsEnabled = true,
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

                // Fallback discovery logic (same as original)
                var hasDiscoveredImageModels = false;
                if (mappings?.Any() == true)
                {
                    try
                    {
                        var enabledMappings = mappings.Where(m => m.IsEnabled).ToList();
                        if (enabledMappings.Any())
                        {
                            var capabilityTests = enabledMappings
                                .Select(m => (m.ModelId, "ImageGeneration"))
                                .ToList();
                            
                            var bulkResults = await _conduitApiClient.TestBulkModelCapabilitiesAsync(capabilityTests);
                            
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
                        _logger.LogDebug(discEx, "Could not test bulk image generation capabilities");
                    }
                }

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
                
                var fallbackState = new NavigationItemState
                {
                    IsEnabled = false,
                    TooltipMessage = "Unable to verify image generation models - check API connection",
                    ShowIndicator = true
                };
                
                UpdateState("/image-generation", fallbackState);
            }
        }

        private async Task CheckVideoGenerationPrerequisitesAsync()
        {
            try
            {
                // Primary: Check database-configured models via Admin API
                var mappings = await _adminApiClient.GetAllModelProviderMappingsAsync();
                var hasConfiguredVideoModels = mappings?.Any(m => 
                    m.IsEnabled && 
                    m.SupportsVideoGeneration) ?? false;

                if (hasConfiguredVideoModels)
                {
                    var newState = new NavigationItemState
                    {
                        IsEnabled = true,
                        TooltipMessage = null,
                        RequiredConfigurationUrl = null,
                        ShowIndicator = false
                    };
                    UpdateState("/video-generation", newState);
                    return;
                }

                // Fallback: Check if any mapped models support video generation via Discovery API (using bulk API)
                var hasDiscoveredVideoModels = false;
                if (mappings?.Any() == true)
                {
                    try
                    {
                        var enabledMappings = mappings.Where(m => m.IsEnabled).ToList();
                        if (enabledMappings.Any())
                        {
                            // Use bulk API to test all models at once
                            var capabilityTests = enabledMappings
                                .Select(m => (m.ModelId, "VideoGeneration"))
                                .ToList();
                            
                            var bulkResults = await _conduitApiClient.TestBulkModelCapabilitiesAsync(capabilityTests);
                            
                            // Check if any model supports video generation
                            foreach (var mapping in enabledMappings)
                            {
                                var key = $"{mapping.ModelId}:VideoGeneration";
                                if (bulkResults.TryGetValue(key, out var supportsVideoGen) && supportsVideoGen)
                                {
                                    hasDiscoveredVideoModels = true;
                                    _logger.LogInformation("Discovered video generation capability for model: {Model}", mapping.ModelId);
                                    break;
                                }
                            }
                        }
                    }
                    catch (Exception discEx)
                    {
                        _logger.LogDebug(discEx, "Could not test bulk video generation capabilities, falling back to individual tests");
                        
                        // Fallback to individual API calls if bulk fails
                        foreach (var mapping in mappings.Where(m => m.IsEnabled))
                        {
                            try
                            {
                                var supportsVideoGen = await _conduitApiClient.TestModelCapabilityAsync(
                                    mapping.ModelId, "VideoGeneration");
                                if (supportsVideoGen)
                                {
                                    hasDiscoveredVideoModels = true;
                                    _logger.LogInformation("Discovered video generation capability for model: {Model}", mapping.ModelId);
                                    break;
                                }
                            }
                            catch (Exception individualEx)
                            {
                                _logger.LogDebug(individualEx, "Could not test video generation capability for model: {Model}", mapping.ModelId);
                            }
                        }
                    }
                }

                // Generate detailed feedback based on discovery results
                string tooltipMessage;
                if (hasDiscoveredVideoModels)
                {
                    tooltipMessage = "Video generation available (discovered via capability testing - consider updating model configuration)";
                }
                else if (mappings?.Any(m => m.IsEnabled) == true)
                {
                    tooltipMessage = $"No video generation models found among {mappings.Count(m => m.IsEnabled)} configured models. Add MiniMax or other video models.";
                }
                else
                {
                    tooltipMessage = "No models configured. Add and configure LLM providers with video generation capabilities.";
                }

                var finalState = new NavigationItemState
                {
                    IsEnabled = hasDiscoveredVideoModels,
                    TooltipMessage = tooltipMessage,
                    RequiredConfigurationUrl = hasDiscoveredVideoModels ? null : "/model-mappings",
                    ShowIndicator = !hasDiscoveredVideoModels
                };

                UpdateState("/video-generation", finalState);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking video generation prerequisites");
                
                // Set a safe default state when API is not available
                var fallbackState = new NavigationItemState
                {
                    IsEnabled = false,
                    TooltipMessage = "Unable to verify video generation models - check API connection",
                    ShowIndicator = true
                };
                
                UpdateState("/video-generation", fallbackState);
            }
        }

        private void UpdateState(string route, NavigationItemState newState)
        {
            var oldState = _stateCache.GetValueOrDefault(route);
            _stateCache[route] = newState;

            if (oldState?.IsEnabled != newState.IsEnabled || oldState?.ShowIndicator != newState.ShowIndicator)
            {
                NavigationStateChanged?.Invoke(this, new NavigationStateChangedEventArgs(route, newState));
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _reconnectTimer?.Dispose();
                _fallbackPollingTimer?.Dispose();
                _refreshSemaphore?.Dispose();
                
                // Unsubscribe from events
                _signalRConnectionManager.ConnectionStateChanged -= OnConnectionStateChanged;
                
                if (_hubConnection != null)
                {
                    _signalRConnectionManager.DisconnectFromHubAsync("navigation-state").AsTask().Wait(TimeSpan.FromSeconds(5));
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                _disposed = true;
                _reconnectTimer?.Dispose();
                _fallbackPollingTimer?.Dispose();
                _refreshSemaphore?.Dispose();
                
                // Unsubscribe from events
                _signalRConnectionManager.ConnectionStateChanged -= OnConnectionStateChanged;
                
                if (_hubConnection != null)
                {
                    await _signalRConnectionManager.DisconnectFromHubAsync("navigation-state");
                }
            }
        }
    }
}