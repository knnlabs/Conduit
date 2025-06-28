using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using ConduitLLM.TUI.Configuration;
using ConduitLLM.AdminClient.Models;
using ConduitLLM.TUI.Models;
using System.Net.Http;
using System.Net.Sockets;

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
                _stateManager.IsConnected = false;
                return;
            }

            // Navigation State Hub
            _navigationStateHub = new HubConnectionBuilder()
                .WithUrl($"{_config.CoreApiUrl}/hubs/navigation-state", options =>
                {
                    options.Headers["X-API-Key"] = _virtualKey;
                })
                .WithAutomaticReconnect()
                .Build();

            _navigationStateHub.On<NavigationStateUpdateDto>("NavigationStateUpdated", update =>
            {
                _logger.LogInformation("Received navigation state update");
                NavigationStateUpdated?.Invoke(this, update);
            });

            _navigationStateHub.Reconnecting += error =>
            {
                _logger.LogWarning("SignalR reconnecting: {Error}", error?.Message);
                _stateManager.IsConnected = false;
                return Task.CompletedTask;
            };

            _navigationStateHub.Reconnected += connectionId =>
            {
                _logger.LogInformation("SignalR reconnected: {ConnectionId}", connectionId);
                _stateManager.IsConnected = true;
                return Task.CompletedTask;
            };

            _navigationStateHub.Closed += error =>
            {
                _logger.LogWarning("SignalR closed: {Error}", error?.Message);
                _stateManager.IsConnected = false;
                return Task.CompletedTask;
            };

            // Video Generation Hub
            _videoGenerationHub = new HubConnectionBuilder()
                .WithUrl($"{_config.CoreApiUrl}/hubs/video-generation", options =>
                {
                    options.Headers["X-API-Key"] = _virtualKey;
                })
                .WithAutomaticReconnect()
                .Build();

            _videoGenerationHub.On<VideoGenerationStatusDto>("VideoGenerationProgress", status =>
            {
                _logger.LogInformation("Video generation progress: {TaskId} - {Status}", status.TaskId, status.Status);
                VideoGenerationStatusUpdated?.Invoke(this, status);
            });

            _videoGenerationHub.On<VideoGenerationStatusDto>("VideoGenerationCompleted", status =>
            {
                _logger.LogInformation("Video generation completed: {TaskId}", status.TaskId);
                VideoGenerationStatusUpdated?.Invoke(this, status);
            });

            _videoGenerationHub.On<VideoGenerationStatusDto>("VideoGenerationFailed", status =>
            {
                _logger.LogError("Video generation failed: {TaskId} - {Error}", status.TaskId, status.ErrorMessage);
                VideoGenerationStatusUpdated?.Invoke(this, status);
            });

            // Image Generation Hub
            _imageGenerationHub = new HubConnectionBuilder()
                .WithUrl($"{_config.CoreApiUrl}/hubs/image-generation", options =>
                {
                    options.Headers["X-API-Key"] = _virtualKey;
                })
                .WithAutomaticReconnect()
                .Build();

            _imageGenerationHub.On<ImageGenerationStatusDto>("ImageGenerationProgress", status =>
            {
                _logger.LogInformation("Image generation progress: {TaskId} - {Status}", status.TaskId, status.Status);
                ImageGenerationStatusUpdated?.Invoke(this, status);
            });

            _imageGenerationHub.On<ImageGenerationStatusDto>("ImageGenerationCompleted", status =>
            {
                _logger.LogInformation("Image generation completed: {TaskId}", status.TaskId);
                ImageGenerationStatusUpdated?.Invoke(this, status);
            });

            _imageGenerationHub.On<ImageGenerationStatusDto>("ImageGenerationFailed", status =>
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

            _stateManager.IsConnected = true;
            _logger.LogInformation("All SignalR connections established");
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("401"))
        {
            // This happens when the virtual key is invalid
            _logger.LogWarning("SignalR connection failed: Unauthorized. Virtual key may be invalid or disabled.");
            _stateManager.IsConnected = false;
            // Don't throw - allow the app to continue without SignalR
        }
        catch (HttpRequestException ex) when (ex.InnerException is SocketException)
        {
            // This is expected when the API is not running
            _logger.LogWarning("SignalR connection failed: API server is not available at {BaseUrl}", _config.CoreApiUrl);
            _stateManager.IsConnected = false;
            // Don't throw - allow the app to continue without SignalR
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error connecting to SignalR");
            _stateManager.IsConnected = false;
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
                var webUIKeySetting = await _adminApiService.GetSettingByKeyAsync("WebUI_VirtualKey");
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
            await _videoGenerationHub.InvokeAsync("JoinTaskGroup", taskId);
            _logger.LogInformation("Joined video generation group: {TaskId}", taskId);
        }
    }

    public async Task LeaveVideoGenerationGroupAsync(string taskId)
    {
        if (_videoGenerationHub?.State == HubConnectionState.Connected)
        {
            await _videoGenerationHub.InvokeAsync("LeaveTaskGroup", taskId);
            _logger.LogInformation("Left video generation group: {TaskId}", taskId);
        }
    }

    public async Task JoinImageGenerationGroupAsync(string taskId)
    {
        if (_imageGenerationHub?.State == HubConnectionState.Connected)
        {
            await _imageGenerationHub.InvokeAsync("JoinTaskGroup", taskId);
            _logger.LogInformation("Joined image generation group: {TaskId}", taskId);
        }
    }

    public async Task LeaveImageGenerationGroupAsync(string taskId)
    {
        if (_imageGenerationHub?.State == HubConnectionState.Connected)
        {
            await _imageGenerationHub.InvokeAsync("LeaveTaskGroup", taskId);
            _logger.LogInformation("Left image generation group: {TaskId}", taskId);
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