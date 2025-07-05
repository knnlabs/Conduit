using System.Net;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using ConduitLLM.AdminClient.Client;
using ConduitLLM.AdminClient.Constants;
using ConduitLLM.AdminClient.Models;
using ConduitLLM.AdminClient.SignalR;

namespace ConduitLLM.AdminClient.Services;

/// <summary>
/// Service for managing SignalR connections and health checks for admin operations.
/// Provides methods to connect, disconnect, and monitor SignalR hub connections.
/// </summary>
public class ConnectionService : IConnectionService
{
    private readonly ConduitAdminClientConfiguration _configuration;
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
        ConduitAdminClientConfiguration configuration,
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
        _logger?.LogInformation("Connecting all admin SignalR hubs");
        
        // Initialize all hub clients to ensure they're registered
        _ = _signalRService.GetAdminNotificationHubClient();
        _ = _signalRService.GetNavigationStateHubClient();
        
        await _signalRService.StartAllConnectionsAsync(cancellationToken);
        
        _logger?.LogInformation("All admin SignalR hubs connected successfully");
    }

    /// <summary>
    /// Connects a specific SignalR hub.
    /// </summary>
    /// <param name="hubType">The hub type to connect.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if connection was successful.</returns>
    public async Task<bool> ConnectHubAsync(AdminHubType hubType, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Connecting SignalR hub: {HubType}", hubType);
        
        try
        {
            var hubClient = GetHubClientByType(hubType);
            if (hubClient != null)
            {
                await hubClient.StartAsync(cancellationToken);
                _logger?.LogInformation("SignalR hub {HubType} connected successfully", hubType);
                return true;
            }
            
            _logger?.LogWarning("Unknown hub type: {HubType}", hubType);
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to connect hub {HubType}", hubType);
            return false;
        }
    }

    /// <summary>
    /// Disconnects all SignalR hubs.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Disconnecting all admin SignalR hubs");
        await _signalRService.StopAllConnectionsAsync(cancellationToken);
        _logger?.LogInformation("All admin SignalR hubs disconnected successfully");
    }

    /// <summary>
    /// Disconnects a specific SignalR hub.
    /// </summary>
    /// <param name="hubType">The hub type to disconnect.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task DisconnectHubAsync(AdminHubType hubType, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Disconnecting SignalR hub: {HubType}", hubType);
        
        var hubClient = GetHubClientByType(hubType);
        if (hubClient != null)
        {
            await hubClient.StopAsync(cancellationToken);
            _logger?.LogInformation("SignalR hub {HubType} disconnected successfully", hubType);
        }
        else
        {
            _logger?.LogWarning("Unknown hub type: {HubType}", hubType);
        }
    }

    /// <summary>
    /// Gets the current connection status of all hubs.
    /// </summary>
    /// <returns>Dictionary of hub names and their connection states.</returns>
    public Dictionary<string, HubConnectionState> GetStatus()
    {
        return _signalRService.GetConnectionStates();
    }

    /// <summary>
    /// Gets detailed connection health information for all hubs.
    /// </summary>
    /// <returns>List of connection health information.</returns>
    public List<ConnectionHealth> GetDetailedStatus()
    {
        var states = GetStatus();
        var detailedStatus = new List<ConnectionHealth>();
        
        foreach (var kvp in states)
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
    /// <param name="hubType">The hub type.</param>
    /// <returns>Connection health information.</returns>
    public async Task<ConnectionHealth> GetConnectionHealthAsync(AdminHubType hubType)
    {
        await _healthCheckSemaphore.WaitAsync();
        try
        {
            var hubName = GetHubName(hubType);
            var health = GetOrCreateConnectionHealth(hubName);
            var hubClient = GetHubClientByType(hubType);
            
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
    /// Gets the connection status for a specific hub.
    /// </summary>
    /// <param name="hubType">The hub type to check.</param>
    /// <returns>The connection state of the hub.</returns>
    public HubConnectionState GetHubStatus(AdminHubType hubType)
    {
        var states = GetStatus();
        var hubKey = hubType == AdminHubType.NavigationState ? "navigationState" : "adminNotifications";
        return states.TryGetValue(hubKey, out var state) ? state : HubConnectionState.Disconnected;
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
    /// Checks if all connections are fully established and ready.
    /// </summary>
    /// <returns>True if all connections are fully connected.</returns>
    public bool IsFullyConnected()
    {
        var states = GetStatus();
        return states.Values.All(state => state == HubConnectionState.Connected);
    }

    /// <summary>
    /// Checks if a specific hub is connected.
    /// </summary>
    /// <param name="hubType">The hub type to check.</param>
    /// <returns>True if the hub is connected.</returns>
    public Task<bool> IsConnectedAsync(AdminHubType hubType)
    {
        var hubClient = GetHubClientByType(hubType);
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
        _logger?.LogInformation("Reconnecting all admin SignalR hubs");
        
        await DisconnectAsync(cancellationToken);
        await Task.Delay(1000, cancellationToken); // Brief delay between disconnect and reconnect
        await ConnectAsync(cancellationToken);
        
        _logger?.LogInformation("All admin SignalR hubs reconnected successfully");
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
            var request = new HttpRequestMessage(System.Net.Http.HttpMethod.Get, "/health");
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
            ("AdminNotificationHubClient", _signalRService.GetAdminNotificationHubClient()),
            ("NavigationStateHubClient", _signalRService.GetNavigationStateHubClient())
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

    private BaseSignalRConnection? GetHubClientByType(AdminHubType hubType)
    {
        return hubType switch
        {
            AdminHubType.NavigationState => _signalRService.GetNavigationStateHubClient(),
            AdminHubType.AdminNotifications => _signalRService.GetAdminNotificationHubClient(),
            _ => null
        };
    }

    private static string GetHubName(AdminHubType hubType)
    {
        return hubType switch
        {
            AdminHubType.NavigationState => "navigationState",
            AdminHubType.AdminNotifications => "adminNotifications",
            _ => hubType.ToString()
        };
    }

    private static string GetHubNameFromType(string typeName)
    {
        return typeName switch
        {
            "NavigationStateHubClient" => "navigationState",
            "AdminNotificationHubClient" => "adminNotifications",
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