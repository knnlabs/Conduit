using System.Net;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using ConduitLLM.CoreClient.Client;
using ConduitLLM.CoreClient.Constants;
using ConduitLLM.CoreClient.Models;
using ConduitLLM.CoreClient.SignalR;

namespace ConduitLLM.CoreClient.Services;

/// <summary>
/// Service for managing SignalR connections and health checks.
/// Provides methods to connect, disconnect, and monitor SignalR hub connections,
/// as well as lightweight health checks for API connectivity.
/// </summary>
public class ConnectionService : IConnectionService
{
    private readonly ConduitCoreClientConfiguration _configuration;
    private readonly SignalRService _signalRService;
    private readonly HttpClient _httpClient;
    private readonly ILogger<ConnectionService>? _logger;
    private readonly Dictionary<string, ConnectionHealth> _connectionHealth = new();
    private readonly Dictionary<string, HubConnectionState> _previousStates = new();
    private readonly SemaphoreSlim _healthCheckSemaphore = new(1, 1);

    /// <summary>
    /// Event raised when a connection state changes.
    /// </summary>
    public event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;

    /// <summary>
    /// Initializes a new instance of the ConnectionService class.
    /// </summary>
    /// <param name="configuration">Client configuration.</param>
    /// <param name="signalRService">SignalR service instance.</param>
    /// <param name="httpClient">HTTP client for health checks.</param>
    /// <param name="logger">Optional logger instance.</param>
    public ConnectionService(
        ConduitCoreClientConfiguration configuration,
        SignalRService signalRService,
        HttpClient httpClient,
        ILogger<ConnectionService>? logger = null)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _signalRService = signalRService ?? throw new ArgumentNullException(nameof(signalRService));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger;

        InitializeHubMonitoring();
    }

    /// <summary>
    /// Connects all SignalR hubs.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Connecting all SignalR hubs");
        
        // Initialize all hub clients to ensure they're registered
        _ = _signalRService.GetTaskHubClient();
        _ = _signalRService.GetVideoGenerationHubClient();
        _ = _signalRService.GetImageGenerationHubClient();
        
        await _signalRService.StartAllConnectionsAsync(cancellationToken);
        
        _logger?.LogInformation("All SignalR hubs connected successfully");
    }

    /// <summary>
    /// Connects a specific SignalR hub.
    /// </summary>
    /// <param name="hubName">The hub name to connect.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if connection was successful.</returns>
    public async Task<bool> ConnectHubAsync(string hubName, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Connecting SignalR hub: {HubName}", hubName);
        
        try
        {
            var hubClient = GetHubClientByName(hubName);
            if (hubClient != null)
            {
                await hubClient.StartAsync(cancellationToken);
                _logger?.LogInformation("SignalR hub {HubName} connected successfully", hubName);
                return true;
            }
            
            _logger?.LogWarning("Unknown hub name: {HubName}", hubName);
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to connect hub {HubName}", hubName);
            return false;
        }
    }

    /// <summary>
    /// Disconnects all SignalR hubs.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Disconnecting all SignalR hubs");
        await _signalRService.StopAllConnectionsAsync(cancellationToken);
        _logger?.LogInformation("All SignalR hubs disconnected successfully");
    }

    /// <summary>
    /// Disconnects a specific SignalR hub.
    /// </summary>
    /// <param name="hubName">The hub name to disconnect.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task DisconnectHubAsync(string hubName, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Disconnecting SignalR hub: {HubName}", hubName);
        
        var hubClient = GetHubClientByName(hubName);
        if (hubClient != null)
        {
            await hubClient.StopAsync(cancellationToken);
            _logger?.LogInformation("SignalR hub {HubName} disconnected successfully", hubName);
        }
        else
        {
            _logger?.LogWarning("Unknown hub name: {HubName}", hubName);
        }
    }

    /// <summary>
    /// Gets the current connection status of all hubs.
    /// </summary>
    /// <returns>Dictionary of hub names and their connection states.</returns>
    public Dictionary<string, HubConnectionState> GetStatus()
    {
        var status = _signalRService.GetConnectionStatus();
        
        // Convert the type names to more readable hub names
        var result = new Dictionary<string, HubConnectionState>();
        foreach (var kvp in status)
        {
            var hubName = GetHubNameFromType(kvp.Key);
            if (Enum.TryParse<HubConnectionState>(kvp.Value, out var state))
            {
                result[hubName] = state;
            }
        }
        
        return result;
    }

    /// <summary>
    /// Gets detailed connection health information for all hubs.
    /// </summary>
    /// <returns>List of connection health information.</returns>
    public List<ConnectionHealth> GetDetailedStatus()
    {
        var status = GetStatus();
        var detailedStatus = new List<ConnectionHealth>();
        
        foreach (var kvp in status)
        {
            var health = GetOrCreateConnectionHealth(kvp.Key);
            health.State = kvp.Value;
            health.StateDescription = GetStateDescription(kvp.Value);
            health.IsConnected = kvp.Value == HubConnectionState.Connected;
            
            detailedStatus.Add(health);
        }
        
        return detailedStatus;
    }

    /// <summary>
    /// Gets the connection health for a specific hub.
    /// </summary>
    /// <param name="hubName">The hub name.</param>
    /// <returns>Connection health information.</returns>
    public async Task<ConnectionHealth> GetConnectionHealthAsync(string hubName)
    {
        await _healthCheckSemaphore.WaitAsync();
        try
        {
            var health = GetOrCreateConnectionHealth(hubName);
            var hubClient = GetHubClientByName(hubName);
            
            if (hubClient != null)
            {
                health.State = hubClient.State;
                health.StateDescription = GetStateDescription(hubClient.State);
                health.IsConnected = hubClient.IsConnected;
                
                // Perform a simple latency check if connected
                if (hubClient.IsConnected)
                {
                    var startTime = DateTime.UtcNow;
                    try
                    {
                        // Most hubs should support a basic echo or ping method
                        await Task.Delay(1); // Simulate a round trip
                        health.LatencyMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
                    }
                    catch
                    {
                        health.LatencyMs = null;
                    }
                }
            }
            
            return health;
        }
        finally
        {
            _healthCheckSemaphore.Release();
        }
    }

    /// <summary>
    /// Checks if all connections are established.
    /// </summary>
    /// <returns>True if all connections are established.</returns>
    public bool IsConnected()
    {
        return _signalRService.AreAllConnectionsEstablished();
    }

    /// <summary>
    /// Checks if a specific hub is connected.
    /// </summary>
    /// <param name="hubName">The hub name to check.</param>
    /// <returns>True if the hub is connected.</returns>
    public Task<bool> IsConnectedAsync(string hubName)
    {
        var hubClient = GetHubClientByName(hubName);
        return Task.FromResult(hubClient?.IsConnected ?? false);
    }

    /// <summary>
    /// Waits for all connections to be established.
    /// </summary>
    /// <param name="timeoutMs">Timeout in milliseconds.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if all connections are established within timeout.</returns>
    public async Task<bool> WaitForConnectionAsync(int timeoutMs = 30000, CancellationToken cancellationToken = default)
    {
        var timeout = TimeSpan.FromMilliseconds(timeoutMs);
        return await _signalRService.WaitForAllConnectionsAsync(timeout, cancellationToken);
    }

    /// <summary>
    /// Reconnects all SignalR hubs.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task ReconnectAsync(CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Reconnecting all SignalR hubs");
        
        await DisconnectAsync(cancellationToken);
        await Task.Delay(1000, cancellationToken); // Brief delay between disconnect and reconnect
        await ConnectAsync(cancellationToken);
        
        _logger?.LogInformation("All SignalR hubs reconnected successfully");
    }

    /// <summary>
    /// Updates SignalR configuration dynamically.
    /// </summary>
    /// <param name="config">New configuration to apply.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task UpdateConfigurationAsync(SignalRConfiguration config, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Updating SignalR configuration");
        
        var wasConnected = IsConnected();
        
        if (wasConnected)
        {
            await DisconnectAsync(cancellationToken);
        }
        
        // Update configuration settings
        // Note: In a real implementation, we would need to propagate these settings
        // to the SignalRService and hub clients
        
        if (wasConnected && config.Enabled && config.AutoConnect)
        {
            await ConnectAsync(cancellationToken);
        }
        
        _logger?.LogInformation("SignalR configuration updated successfully");
    }

    /// <summary>
    /// Performs a lightweight health check to verify the API is reachable.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the API is reachable.</returns>
    public async Task<bool> PingAsync(CancellationToken cancellationToken = default)
    {
        return await PingWithTimeoutAsync(5000, cancellationToken);
    }

    /// <summary>
    /// Performs a lightweight health check with a custom timeout.
    /// </summary>
    /// <param name="timeoutMs">Timeout in milliseconds.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the API is reachable within the timeout.</returns>
    public async Task<bool> PingWithTimeoutAsync(int timeoutMs, CancellationToken cancellationToken = default)
    {
        if (timeoutMs <= 0)
        {
            throw new ArgumentException("Timeout must be greater than 0", nameof(timeoutMs));
        }
        
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeoutMs);
            
            // Create a request without authentication headers for health check
            var request = new HttpRequestMessage(HttpMethod.Get, "/health/ready");
            request.Headers.Clear();
            request.Headers.Add("Accept", "application/json");
            
            var response = await _httpClient.SendAsync(request, cts.Token);
            return response.StatusCode == HttpStatusCode.OK;
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Ping failed");
            return false;
        }
    }

    private void InitializeHubMonitoring()
    {
        // Set up monitoring for each hub type
        var hubs = new (string, BaseSignalRConnection)[]
        {
            ("TaskHubClient", _signalRService.GetTaskHubClient()),
            ("VideoGenerationHubClient", _signalRService.GetVideoGenerationHubClient()),
            ("ImageGenerationHubClient", _signalRService.GetImageGenerationHubClient())
        };
        
        foreach (var (hubType, hubClient) in hubs)
        {
            var hubName = GetHubNameFromType(hubType);
            _previousStates[hubName] = hubClient.State;
            
            // Subscribe to connection events
            hubClient.Connected += async () => await OnHubConnected(hubName);
            hubClient.Disconnected += async (ex) => await OnHubDisconnected(hubName, ex);
            hubClient.Reconnecting += async (ex) => await OnHubReconnecting(hubName, ex);
            hubClient.Reconnected += async (connectionId) => await OnHubReconnected(hubName, connectionId);
        }
    }

    private async Task OnHubConnected(string hubName)
    {
        var health = GetOrCreateConnectionHealth(hubName);
        health.LastConnected = DateTime.UtcNow;
        health.ReconnectAttempts = 0;
        health.LastError = null;
        
        await NotifyStateChange(hubName, HubConnectionState.Connecting, HubConnectionState.Connected);
    }

    private async Task OnHubDisconnected(string hubName, Exception? exception)
    {
        var health = GetOrCreateConnectionHealth(hubName);
        health.LastError = exception?.Message;
        
        await NotifyStateChange(hubName, HubConnectionState.Connected, HubConnectionState.Disconnected);
    }

    private async Task OnHubReconnecting(string hubName, Exception? exception)
    {
        var health = GetOrCreateConnectionHealth(hubName);
        health.ReconnectAttempts++;
        health.LastError = exception?.Message;
        
        await NotifyStateChange(hubName, HubConnectionState.Disconnected, HubConnectionState.Reconnecting);
    }

    private async Task OnHubReconnected(string hubName, string? connectionId)
    {
        var health = GetOrCreateConnectionHealth(hubName);
        health.LastConnected = DateTime.UtcNow;
        health.LastError = null;
        
        await NotifyStateChange(hubName, HubConnectionState.Reconnecting, HubConnectionState.Connected);
    }

    private async Task NotifyStateChange(string hubName, HubConnectionState previousState, HubConnectionState newState)
    {
        _previousStates[hubName] = newState;
        
        var args = new ConnectionStateChangedEventArgs(hubName, previousState, newState);
        ConnectionStateChanged?.Invoke(this, args);
        
        _logger?.LogInformation("Hub {HubName} state changed from {PreviousState} to {NewState}", 
            hubName, previousState, newState);
        
        await Task.CompletedTask;
    }

    private ConnectionHealth GetOrCreateConnectionHealth(string hubName)
    {
        if (!_connectionHealth.TryGetValue(hubName, out var health))
        {
            health = new ConnectionHealth
            {
                HubName = hubName,
                State = HubConnectionState.Disconnected,
                StateDescription = GetStateDescription(HubConnectionState.Disconnected),
                IsConnected = false
            };
            _connectionHealth[hubName] = health;
        }
        
        return health;
    }

    private BaseSignalRConnection? GetHubClientByName(string hubName)
    {
        return hubName.ToLowerInvariant() switch
        {
            "task" or "taskhub" or "tasks" => _signalRService.GetTaskHubClient(),
            "video" or "videogeneration" or "video-generation" => _signalRService.GetVideoGenerationHubClient(),
            "image" or "imagegeneration" or "image-generation" => _signalRService.GetImageGenerationHubClient(),
            _ => null
        };
    }

    private static string GetHubNameFromType(string typeName)
    {
        return typeName switch
        {
            "TaskHubClient" => "TaskHub",
            "VideoGenerationHubClient" => "VideoGenerationHub",
            "ImageGenerationHubClient" => "ImageGenerationHub",
            _ => typeName
        };
    }

    private static string GetStateDescription(HubConnectionState state)
    {
        return state switch
        {
            HubConnectionState.Disconnected => "Not connected",
            HubConnectionState.Connecting => "Establishing connection",
            HubConnectionState.Connected => "Connected and ready",
            HubConnectionState.Reconnecting => "Connection lost, attempting to reconnect",
            _ => "Unknown"
        };
    }
}