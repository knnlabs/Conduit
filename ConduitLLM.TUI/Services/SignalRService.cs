using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using ConduitLLM.TUI.Configuration;
using ConduitLLM.AdminClient.Models;
using ConduitLLM.TUI.Models;
using ConduitLLM.TUI.Utils;
using System.Net.Http;
using System.Net.Sockets;
using ConduitLLM.TUI.Constants;

namespace ConduitLLM.TUI.Services;

public class SignalRService : IAsyncDisposable
{
    private readonly AppConfiguration _config;
    private readonly StateManager _stateManager;
    private readonly ILogger<SignalRService> _logger;
    private readonly AdminApiService _adminApiService;
    private HubConnection? _navigationStateHub;
    private HubConnection? _videoGenerationHub;
    private HubConnection? _imageGenerationHub;
    private string? _virtualKey;

    public event EventHandler<NavigationStateUpdateDto>? NavigationStateUpdated;
    public event EventHandler<VideoGenerationStatusDto>? VideoGenerationStatusUpdated;
    public event EventHandler<ImageGenerationStatusDto>? ImageGenerationStatusUpdated;
    
    // Configuration-related events
    public event Action<bool>? ConnectionStateChanged;
    
    // TODO: Implement these events when configuration change notifications are added
    #pragma warning disable CS0067 // Event is never used
    public event EventHandler<GlobalSettingChangedEventArgs>? GlobalSettingChanged;
    public event EventHandler<HttpClientConfigChangedEventArgs>? HttpClientConfigChanged;
    public event EventHandler<CacheConfigChangedEventArgs>? CacheConfigChanged;
    public event EventHandler<RouterConfigChangedEventArgs>? RouterConfigChanged;
    public event EventHandler<IpFilterChangedEventArgs>? IpFilterChanged;
    public event EventHandler<AudioConfigChangedEventArgs>? AudioConfigChanged;
    public event EventHandler<SystemHealthChangedEventArgs>? SystemHealthChanged;
    #pragma warning restore CS0067

    public SignalRService(AppConfiguration config, StateManager stateManager, AdminApiService adminApiService, ILogger<SignalRService> logger)
    {
        _config = config;
        _stateManager = stateManager;
        _adminApiService = adminApiService;
        _logger = logger;
    }

    public async Task ConnectAsync()
    {
        try
        {
            // First, get or create a virtual key for the TUI
            await EnsureVirtualKeyAsync();
            
            if (string.IsNullOrEmpty(_virtualKey))
            {
                _logger.LogWarning("Could not obtain virtual key for SignalR connections");
                UpdateConnectionState(false);
                return;
            }

            // Navigation State Hub (using SystemNotificationHub at /hubs/notifications)
            _navigationStateHub = new HubConnectionBuilder()
                .WithUrl($"{_config.CoreApiUrl}{UIConstants.SignalR.Hubs.Notifications}", options =>
                {
                    options.Headers[UIConstants.Configuration.ApiKeyHeader] = _virtualKey;
                })
                .WithAutomaticReconnect()
                .Build();

            // Listen for model mapping changes
            _navigationStateHub.On<ModelMappingNotification>(UIConstants.SignalR.Methods.OnModelMappingChanged, notification =>
            {
                _logger.LogInformation("Received model mapping change: {ModelAlias} ({ChangeType})", notification.ModelAlias, notification.ChangeType);
                // For now, trigger a navigation state update event (we might need to fetch the full state)
                NavigationStateUpdated?.Invoke(this, new NavigationStateUpdateDto());
            });
            
            // Listen for provider health changes
            _navigationStateHub.On<ProviderHealthNotification>(UIConstants.SignalR.Methods.OnProviderHealthChanged, notification =>
            {
                _logger.LogInformation("Received provider health change: {Provider} - {Status}", notification.Provider, notification.Status);
                NavigationStateUpdated?.Invoke(this, new NavigationStateUpdateDto());
            });
            
            // Listen for model capabilities discovery
            _navigationStateHub.On<ModelCapabilitiesNotification>(UIConstants.SignalR.Methods.OnModelCapabilitiesDiscovered, notification =>
            {
                _logger.LogInformation("Received model capabilities discovered: {Provider} ({ModelCount} models)", notification.ProviderName, notification.ModelCount);
                NavigationStateUpdated?.Invoke(this, new NavigationStateUpdateDto());
            });

            _navigationStateHub.Reconnecting += error =>
            {
                _logger.LogWarning("SignalR reconnecting: {Error}", error?.Message);
                UpdateConnectionState(false);
                return Task.CompletedTask;
            };

            _navigationStateHub.Reconnected += connectionId =>
            {
                _logger.LogInformation("SignalR reconnected: {ConnectionId}", connectionId);
                UpdateConnectionState(true);
                return Task.CompletedTask;
            };

            _navigationStateHub.Closed += error =>
            {
                _logger.LogWarning("SignalR closed: {Error}", error?.Message);
                UpdateConnectionState(false);
                return Task.CompletedTask;
            };

            // Video Generation Hub
            _videoGenerationHub = new HubConnectionBuilder()
                .WithUrl($"{_config.CoreApiUrl}{UIConstants.SignalR.Hubs.VideoGeneration}", options =>
                {
                    options.Headers[UIConstants.Configuration.ApiKeyHeader] = _virtualKey;
                })
                .WithAutomaticReconnect()
                .Build();

            _videoGenerationHub.On<VideoGenerationStatusDto>(UIConstants.SignalR.Methods.VideoGenerationProgress, status =>
            {
                _logger.LogInformation("Video generation progress: {TaskId} - {Status}", status.TaskId, status.Status);
                VideoGenerationStatusUpdated?.Invoke(this, status);
            });

            _videoGenerationHub.On<VideoGenerationStatusDto>(UIConstants.SignalR.Methods.VideoGenerationCompleted, status =>
            {
                _logger.LogInformation("Video generation completed: {TaskId}", status.TaskId);
                VideoGenerationStatusUpdated?.Invoke(this, status);
            });

            _videoGenerationHub.On<VideoGenerationStatusDto>(UIConstants.SignalR.Methods.VideoGenerationFailed, status =>
            {
                _logger.LogError("Video generation failed: {TaskId} - {Error}", status.TaskId, status.ErrorMessage);
                VideoGenerationStatusUpdated?.Invoke(this, status);
            });

            // Image Generation Hub
            _imageGenerationHub = new HubConnectionBuilder()
                .WithUrl($"{_config.CoreApiUrl}{UIConstants.SignalR.Hubs.ImageGeneration}", options =>
                {
                    options.Headers[UIConstants.Configuration.ApiKeyHeader] = _virtualKey;
                })
                .WithAutomaticReconnect()
                .Build();

            _imageGenerationHub.On<ImageGenerationStatusDto>(UIConstants.SignalR.Methods.ImageGenerationProgress, status =>
            {
                _logger.LogInformation("Image generation progress: {TaskId} - {Status}", status.TaskId, status.Status);
                ImageGenerationStatusUpdated?.Invoke(this, status);
            });

            _imageGenerationHub.On<ImageGenerationStatusDto>(UIConstants.SignalR.Methods.ImageGenerationCompleted, status =>
            {
                _logger.LogInformation("Image generation completed: {TaskId}", status.TaskId);
                ImageGenerationStatusUpdated?.Invoke(this, status);
            });

            _imageGenerationHub.On<ImageGenerationStatusDto>(UIConstants.SignalR.Methods.ImageGenerationFailed, status =>
            {
                _logger.LogError("Image generation failed: {TaskId} - {Error}", status.TaskId, status.ErrorMessage);
                ImageGenerationStatusUpdated?.Invoke(this, status);
            });

            // Start all connections
            var tasks = new[]
            {
                _navigationStateHub.StartAsync(),
                _videoGenerationHub.StartAsync(),
                _imageGenerationHub.StartAsync()
            };
            
            await Task.WhenAll(tasks);

            UpdateConnectionState(true);
            _logger.LogInformation("All SignalR connections established");
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("401"))
        {
            // This happens when the virtual key is invalid
            _logger.LogWarning("SignalR connection failed: Unauthorized. Virtual key may be invalid or disabled.");
            UpdateConnectionState(false);
            // Don't throw - allow the app to continue without SignalR
        }
        catch (HttpRequestException ex) when (ex.InnerException is SocketException)
        {
            // This is expected when the API is not running
            _logger.LogWarning("SignalR connection failed: API server is not available at {BaseUrl}", _config.CoreApiUrl);
            var troubleshooting = ConnectionHelper.GetConnectionTroubleshootingMessage(_config.CoreApiUrl, ex);
            _logger.LogInformation("Connection troubleshooting info:\n{Troubleshooting}", troubleshooting);
            UpdateConnectionState(false);
            // Don't throw - allow the app to continue without SignalR
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error connecting to SignalR");
            var troubleshooting = ConnectionHelper.GetConnectionTroubleshootingMessage(_config.CoreApiUrl, ex);
            _logger.LogInformation("Connection troubleshooting info:\n{Troubleshooting}", troubleshooting);
            UpdateConnectionState(false);
            // Don't throw - allow the app to continue without SignalR
        }
    }

    private async Task EnsureVirtualKeyAsync()
    {
        try
        {
            // First check if a virtual key is already selected in the StateManager
            if (!string.IsNullOrEmpty(_stateManager.SelectedVirtualKey))
            {
                _virtualKey = _stateManager.SelectedVirtualKey;
                _logger.LogInformation("Using selected virtual key from StateManager");
                return;
            }
            
            // Try to get the WebUI virtual key from configuration using the specific key
            try
            {
                var webUIKeySetting = await _adminApiService.GetSettingByKeyAsync(UIConstants.Configuration.WebUIVirtualKey);
                if (webUIKeySetting != null && !string.IsNullOrEmpty(webUIKeySetting.Value))
                {
                    _virtualKey = webUIKeySetting.Value;
                    _stateManager.SelectedVirtualKey = _virtualKey;
                    _logger.LogInformation("Using WebUI virtual key from configuration");
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error retrieving WebUI virtual key from configuration");
            }
            
            _logger.LogWarning("No WebUI virtual key found in configuration. The TUI requires a virtual key for Core API access.");
            _logger.LogWarning("Please ensure the WebUI has been started at least once to create the shared virtual key.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ensuring virtual key for TUI");
        }
    }

    public async Task JoinVideoGenerationGroupAsync(string taskId)
    {
        if (_videoGenerationHub?.State == HubConnectionState.Connected)
        {
            await _videoGenerationHub.InvokeAsync(UIConstants.SignalR.Methods.JoinTaskGroup, taskId);
            _logger.LogInformation("Joined video generation group: {TaskId}", taskId);
        }
    }

    public async Task LeaveVideoGenerationGroupAsync(string taskId)
    {
        if (_videoGenerationHub?.State == HubConnectionState.Connected)
        {
            await _videoGenerationHub.InvokeAsync(UIConstants.SignalR.Methods.LeaveTaskGroup, taskId);
            _logger.LogInformation("Left video generation group: {TaskId}", taskId);
        }
    }

    public async Task JoinImageGenerationGroupAsync(string taskId)
    {
        if (_imageGenerationHub?.State == HubConnectionState.Connected)
        {
            await _imageGenerationHub.InvokeAsync(UIConstants.SignalR.Methods.JoinTaskGroup, taskId);
            _logger.LogInformation("Joined image generation group: {TaskId}", taskId);
        }
    }

    public async Task LeaveImageGenerationGroupAsync(string taskId)
    {
        if (_imageGenerationHub?.State == HubConnectionState.Connected)
        {
            await _imageGenerationHub.InvokeAsync(UIConstants.SignalR.Methods.LeaveTaskGroup, taskId);
            _logger.LogInformation("Left image generation group: {TaskId}", taskId);
        }
    }

    /// <summary>
    /// Update the connection state and fire the ConnectionStateChanged event.
    /// </summary>
    private void UpdateConnectionState(bool isConnected)
    {
        var previousState = _stateManager.IsConnected;
        _stateManager.IsConnected = isConnected;
        
        if (previousState != isConnected)
        {
            ConnectionStateChanged?.Invoke(isConnected);
            _logger.LogInformation("SignalR connection state changed: {IsConnected}", isConnected);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_navigationStateHub != null)
        {
            await _navigationStateHub.DisposeAsync();
        }
        if (_videoGenerationHub != null)
        {
            await _videoGenerationHub.DisposeAsync();
        }
        if (_imageGenerationHub != null)
        {
            await _imageGenerationHub.DisposeAsync();
        }
    }
}

// DTOs for SignalR events
public class NavigationStateUpdateDto
{
    public List<ProviderCredentialDto> Providers { get; set; } = new();
    public List<ModelProviderMappingDto> ModelMappings { get; set; } = new();
    public Dictionary<string, List<ModelCapabilityDto>> ModelCapabilities { get; set; } = new();
}

// Notification DTOs from SystemNotificationHub
public class ModelMappingNotification
{
    public int MappingId { get; set; }
    public string ModelAlias { get; set; } = string.Empty;
    public string ChangeType { get; set; } = string.Empty;
    public string Priority { get; set; } = "Medium";
}

public class ProviderHealthNotification
{
    public string Provider { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public double? ResponseTimeMs { get; set; }
    public string Priority { get; set; } = "Medium";
    public string? Details { get; set; }
}

public class ModelCapabilitiesNotification
{
    public string ProviderName { get; set; } = string.Empty;
    public int ModelCount { get; set; }
    public int EmbeddingCount { get; set; }
    public int VisionCount { get; set; }
    public int ImageGenCount { get; set; }
    public int VideoGenCount { get; set; }
    public string Priority { get; set; } = "Low";
}

public class VideoGenerationStatusDto
{
    public string TaskId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public double Progress { get; set; }
    public string? VideoUrl { get; set; }
    public string? ErrorMessage { get; set; }
}

public class ImageGenerationStatusDto
{
    public string TaskId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public double Progress { get; set; }
    public List<string> ImageUrls { get; set; } = new();
    public string? ErrorMessage { get; set; }
}

#region Configuration Event Args

public class GlobalSettingChangedEventArgs : EventArgs
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string ChangeType { get; set; } = string.Empty; // Created, Updated, Deleted
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class HttpClientConfigChangedEventArgs : EventArgs
{
    public int ConfigId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string ChangeType { get; set; } = string.Empty; // Created, Updated, Deleted
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object>? ChangedProperties { get; set; }
}

public class CacheConfigChangedEventArgs : EventArgs
{
    public int ConfigId { get; set; }
    public string CacheType { get; set; } = string.Empty;
    public string ChangeType { get; set; } = string.Empty; // Created, Updated, Deleted
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object>? ChangedProperties { get; set; }
}

public class RouterConfigChangedEventArgs : EventArgs
{
    public string Strategy { get; set; } = string.Empty;
    public string ChangeType { get; set; } = string.Empty; // Updated
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object>? ChangedProperties { get; set; }
}

public class IpFilterChangedEventArgs : EventArgs
{
    public int? RuleId { get; set; } // Null for settings changes
    public string IpAddress { get; set; } = string.Empty;
    public string FilterType { get; set; } = string.Empty; // Allow, Deny
    public string ChangeType { get; set; } = string.Empty; // Created, Updated, Deleted, SettingsUpdated
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class AudioConfigChangedEventArgs : EventArgs
{
    public string Provider { get; set; } = string.Empty;
    public string ChangeType { get; set; } = string.Empty; // Created, Updated, Deleted
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object>? ChangedProperties { get; set; }
}

public class SystemHealthChangedEventArgs : EventArgs
{
    public string HealthStatus { get; set; } = string.Empty;
    public Dictionary<string, object>? Metrics { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

#endregion