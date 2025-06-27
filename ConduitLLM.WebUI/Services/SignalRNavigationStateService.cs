using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.WebUI.Models;

namespace ConduitLLM.WebUI.Services
{
    /// <summary>
    /// Navigation state service that uses server-side SignalR for real-time updates
    /// </summary>
    public class SignalRNavigationStateService : ServerSideSignalRListenerBase, INavigationStateService, IDisposable
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<SignalRNavigationStateService> _logger;
        private readonly ServerSideSignalRService _signalRService;
        private readonly ConcurrentDictionary<string, NavigationItemState> _stateCache;
        private readonly SemaphoreSlim _refreshSemaphore;
        private DateTime _lastRefresh;
        private bool _disposed;

        public SignalRNavigationStateService(
            IConfiguration configuration,
            ILogger<SignalRNavigationStateService> logger,
            ServerSideSignalRService signalRService)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _signalRService = signalRService ?? throw new ArgumentNullException(nameof(signalRService));
            _stateCache = new ConcurrentDictionary<string, NavigationItemState>();
            _refreshSemaphore = new SemaphoreSlim(1, 1);
            _lastRefresh = DateTime.MinValue;

            // Register as a listener for SignalR events
            _signalRService.RegisterListener(this);
            
            // Initialize with default states (enabled by default)
            InitializeDefaultStates();
        }

        private void InitializeDefaultStates()
        {
            var routes = new[] { "chat", "embeddings", "audio", "images", "video" };
            foreach (var route in routes)
            {
                _stateCache.TryAdd(route, new NavigationItemState
                {
                    IsEnabled = true,
                    TooltipMessage = null,
                    ShowIndicator = false
                });
            }
        }

        public async Task<NavigationItemState> GetChatStateAsync()
        {
            return await GetNavigationItemStateAsync("chat");
        }

        public async Task<NavigationItemState> GetEmbeddingsStateAsync()
        {
            return await GetNavigationItemStateAsync("embeddings");
        }

        public async Task<NavigationItemState> GetAudioStateAsync()
        {
            return await GetNavigationItemStateAsync("audio");
        }

        public async Task<NavigationItemState> GetImagesStateAsync()
        {
            return await GetNavigationItemStateAsync("images");
        }

        public async Task<NavigationItemState> GetVideoStateAsync()
        {
            return await GetNavigationItemStateAsync("video");
        }

        public async Task<NavigationItemState> GetNavigationItemStateAsync(string route)
        {
            return await Task.FromResult(_stateCache.GetOrAdd(route, _ => new NavigationItemState
            {
                IsEnabled = true,
                TooltipMessage = null,
                ShowIndicator = false
            }));
        }

        public async Task<Dictionary<string, NavigationItemState>> GetAllNavigationStatesAsync()
        {
            return await Task.FromResult(new Dictionary<string, NavigationItemState>(_stateCache));
        }

        public async Task<CapabilityStatusInfo> GetCapabilityStatusAsync()
        {
            // Return a basic status - the real implementation should query the actual capability status
            return await Task.FromResult(new CapabilityStatusInfo
            {
                TotalConfiguredModels = 0,
                ImageGenerationModels = 0,
                VideoGenerationModels = 0,
                VisionModels = 0,
                AudioTranscriptionModels = 0,
                TextToSpeechModels = 0,
                RealtimeAudioModels = 0,
                HasError = false
            });
        }

        public event EventHandler<NavigationStateChangedEventArgs>? NavigationStateChanged;

        public async Task RefreshStatesAsync()
        {
            // In this simplified version, we rely on SignalR events to update states
            // No active refresh is needed
            await Task.CompletedTask;
        }

        private void UpdateState(string key, NavigationItemState state)
        {
            _stateCache.AddOrUpdate(key, state, (_, __) => state);
            _logger.LogTrace("Updated navigation state for {Key}: Enabled={IsEnabled}, Message={Message}", 
                key, state.IsEnabled, state.TooltipMessage);
            
            // Raise the event
            NavigationStateChanged?.Invoke(this, new NavigationStateChangedEventArgs(key, state));
        }

        // SignalR event handlers
        public override async Task OnNavigationStateChanged(JsonElement data)
        {
            _logger.LogDebug("Received navigation state change notification");
            
            try
            {
                // Parse the navigation state update
                if (data.TryGetProperty("stateKey", out var stateKeyElement) &&
                    data.TryGetProperty("state", out var stateElement))
                {
                    var stateKey = stateKeyElement.GetString();
                    if (!string.IsNullOrEmpty(stateKey))
                    {
                        var state = JsonSerializer.Deserialize<NavigationItemState>(stateElement.GetRawText());
                        if (state != null)
                        {
                            UpdateState(stateKey, state);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing navigation state change");
            }
            
            await Task.CompletedTask;
        }

        public override async Task OnConnectionStateChanged(string hubName, ConnectionState state)
        {
            if (hubName == "notifications" && state == ConnectionState.Connected)
            {
                // When reconnected, states will be updated via SignalR events
                _logger.LogInformation("SignalR reconnected to notifications hub");
            }
            
            await Task.CompletedTask;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _signalRService.UnregisterListener(this);
                _refreshSemaphore?.Dispose();
                _disposed = true;
            }
        }
    }
}