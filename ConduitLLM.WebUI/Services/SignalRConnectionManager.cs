using Microsoft.JSInterop;
using System.Text.Json;

namespace ConduitLLM.WebUI.Services;

/// <summary>
/// Centralized SignalR connection manager that provides a single source of truth
/// for all SignalR connections and their states throughout the application.
/// </summary>
public class SignalRConnectionManager : IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<SignalRConnectionManager> _logger;
    private IJSObjectReference? _signalRService;
    private readonly Dictionary<string, HubConnectionInfo> _connections = new();
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    
    /// <summary>
    /// Event fired when any hub connection state changes
    /// </summary>
    public event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;
    
    /// <summary>
    /// Event fired when connection metrics are updated
    /// </summary>
    public event EventHandler<ConnectionMetricsEventArgs>? MetricsUpdated;
    
    public SignalRConnectionManager(IJSRuntime jsRuntime, ILogger<SignalRConnectionManager> logger)
    {
        _jsRuntime = jsRuntime;
        _logger = logger;
    }
    
    /// <summary>
    /// Initialize the SignalR connection manager
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            _signalRService = await _jsRuntime.InvokeAsync<IJSObjectReference>("ConduitSignalRService.getInstance");
            _logger.LogInformation("SignalR connection manager initialized");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize SignalR connection manager");
            throw;
        }
    }
    
    /// <summary>
    /// Set the virtual key for authentication
    /// </summary>
    public async Task SetVirtualKeyAsync(string virtualKey)
    {
        if (_signalRService == null)
            throw new InvalidOperationException("SignalR service not initialized");
            
        await _signalRService.InvokeVoidAsync("setVirtualKey", virtualKey);
        _logger.LogInformation("Virtual key set for SignalR connections");
    }
    
    /// <summary>
    /// Set the master key for admin authentication
    /// </summary>
    public async Task SetMasterKeyAsync(string masterKey)
    {
        if (_signalRService == null)
            throw new InvalidOperationException("SignalR service not initialized");
            
        await _signalRService.InvokeVoidAsync("setMasterKey", masterKey);
        _logger.LogInformation("Master key set for admin SignalR connections");
    }
    
    /// <summary>
    /// Connect to a SignalR hub
    /// </summary>
    public async Task<HubConnectionInfo> ConnectToHubAsync(string hubName, string? authKey = null, HubConnectionOptions? options = null)
    {
        await _connectionLock.WaitAsync();
        try
        {
            if (_signalRService == null)
                throw new InvalidOperationException("SignalR service not initialized");
            
            // Check if already connected
            if (_connections.TryGetValue(hubName, out var existingConnection) && existingConnection.IsConnected)
            {
                _logger.LogInformation("Hub {HubName} is already connected", hubName);
                return existingConnection;
            }
            
            // Convert options to JS object
            var jsOptions = options != null ? new
            {
                maxReconnectAttempts = options.MaxReconnectAttempts,
                baseReconnectDelay = options.BaseReconnectDelay,
                maxReconnectDelay = options.MaxReconnectDelay,
                enableMessageQueuing = options.EnableMessageQueuing,
                enableAutoReconnect = options.EnableAutoReconnect
            } : null;
            
            // Connect to hub
            await _signalRService.InvokeAsync<IJSObjectReference>("connectToHub", hubName, authKey, jsOptions);
            
            // Create connection info
            var connectionInfo = new HubConnectionInfo
            {
                HubName = hubName,
                State = ConnectionState.Connected,
                ConnectedAt = DateTime.UtcNow,
                IsConnected = true
            };
            
            _connections[hubName] = connectionInfo;
            
            // Register for state changes
            await RegisterHubEventListenersAsync(hubName);
            
            _logger.LogInformation("Connected to hub {HubName}", hubName);
            
            // Notify listeners
            ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs
            {
                HubName = hubName,
                CurrentState = ConnectionState.Connected,
                PreviousState = ConnectionState.Disconnected
            });
            
            return connectionInfo;
        }
        finally
        {
            _connectionLock.Release();
        }
    }
    
    /// <summary>
    /// Disconnect from a SignalR hub
    /// </summary>
    public async Task DisconnectFromHubAsync(string hubName)
    {
        await _connectionLock.WaitAsync();
        try
        {
            if (_signalRService == null)
                return;
            
            await _signalRService.InvokeVoidAsync("disconnectFromHub", hubName);
            
            if (_connections.TryGetValue(hubName, out var connection))
            {
                connection.State = ConnectionState.Disconnected;
                connection.IsConnected = false;
                connection.DisconnectedAt = DateTime.UtcNow;
            }
            
            _logger.LogInformation("Disconnected from hub {HubName}", hubName);
            
            // Notify listeners
            ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs
            {
                HubName = hubName,
                CurrentState = ConnectionState.Disconnected,
                PreviousState = ConnectionState.Connected
            });
        }
        finally
        {
            _connectionLock.Release();
        }
    }
    
    /// <summary>
    /// Get connection info for a specific hub
    /// </summary>
    public HubConnectionInfo? GetConnectionInfo(string hubName)
    {
        return _connections.TryGetValue(hubName, out var info) ? info : null;
    }
    
    /// <summary>
    /// Get all active connections
    /// </summary>
    public IReadOnlyDictionary<string, HubConnectionInfo> GetAllConnections()
    {
        return _connections.AsReadOnly();
    }
    
    /// <summary>
    /// Get connection state for a specific hub
    /// </summary>
    public async Task<ConnectionState> GetConnectionStateAsync(string hubName)
    {
        if (_signalRService == null)
            return ConnectionState.Disconnected;
            
        try
        {
            var state = await _signalRService.InvokeAsync<string>("getConnectionState", hubName);
            return ParseConnectionState(state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get connection state for hub {HubName}", hubName);
            return ConnectionState.Disconnected;
        }
    }
    
    /// <summary>
    /// Get metrics for a specific hub
    /// </summary>
    public async Task<HubMetrics?> GetHubMetricsAsync(string hubName)
    {
        if (_signalRService == null)
            return null;
            
        try
        {
            var metricsJson = await _signalRService.InvokeAsync<JsonElement>("getMetrics", hubName);
            return ParseMetrics(hubName, metricsJson);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get metrics for hub {HubName}", hubName);
            return null;
        }
    }
    
    /// <summary>
    /// Invoke a method on a hub
    /// </summary>
    public async Task<TResult?> InvokeAsync<TResult>(string hubName, string methodName, params object?[] args)
    {
        if (_signalRService == null)
            throw new InvalidOperationException("SignalR service not initialized");
            
        return await _signalRService.InvokeAsync<TResult>("invoke", hubName, methodName, args);
    }
    
    /// <summary>
    /// Send a message to a hub (fire and forget)
    /// </summary>
    public async Task SendAsync(string hubName, string methodName, params object?[] args)
    {
        if (_signalRService == null)
            throw new InvalidOperationException("SignalR service not initialized");
            
        await _signalRService.InvokeVoidAsync("send", hubName, methodName, args);
    }
    
    /// <summary>
    /// Register an event handler on a hub
    /// </summary>
    public async Task OnAsync(string hubName, string eventName, object handler)
    {
        if (_signalRService == null)
            throw new InvalidOperationException("SignalR service not initialized");
            
        await _signalRService.InvokeVoidAsync("on", hubName, eventName, handler);
    }
    
    /// <summary>
    /// Remove an event handler from a hub
    /// </summary>
    public async Task OffAsync(string hubName, string eventName, object handler)
    {
        if (_signalRService == null)
            throw new InvalidOperationException("SignalR service not initialized");
            
        await _signalRService.InvokeVoidAsync("off", hubName, eventName, handler);
    }
    
    /// <summary>
    /// Enable debug mode for detailed logging
    /// </summary>
    public async Task SetDebugModeAsync(bool enabled)
    {
        if (_signalRService == null)
            throw new InvalidOperationException("SignalR service not initialized");
            
        await _signalRService.InvokeVoidAsync("setDebugMode", enabled);
    }
    
    private async Task RegisterHubEventListenersAsync(string hubName)
    {
        // This would be implemented to listen to the JavaScript events
        // For now, we rely on components using this service to handle their own events
        await Task.CompletedTask;
    }
    
    private ConnectionState ParseConnectionState(string state)
    {
        return state?.ToLower() switch
        {
            "connected" => ConnectionState.Connected,
            "connecting" => ConnectionState.Connecting,
            "reconnecting" => ConnectionState.Reconnecting,
            "failed" => ConnectionState.Failed,
            _ => ConnectionState.Disconnected
        };
    }
    
    private HubMetrics ParseMetrics(string hubName, JsonElement metricsJson)
    {
        var metrics = new HubMetrics { HubName = hubName };
        
        // Parse connection time
        if (metricsJson.TryGetProperty("connectionTime", out var connTime))
        {
            if (connTime.TryGetProperty("average", out var avg))
                metrics.AverageConnectionTime = avg.GetDouble();
            if (connTime.TryGetProperty("last", out var last))
                metrics.LastConnectionTime = last.GetDouble();
        }
        
        // Parse invoke metrics
        if (metricsJson.TryGetProperty("invoke.ping", out var ping))
        {
            if (ping.TryGetProperty("average", out var avgPing))
                metrics.AverageLatency = avgPing.GetDouble();
            if (ping.TryGetProperty("last", out var lastPing))
                metrics.LastLatency = lastPing.GetDouble();
            if (ping.TryGetProperty("count", out var count))
                metrics.MessageCount = count.GetInt32();
        }
        
        return metrics;
    }
    
    public async ValueTask DisposeAsync()
    {
        try
        {
            // Disconnect from all hubs
            foreach (var hubName in _connections.Keys.ToList())
            {
                await DisconnectFromHubAsync(hubName);
            }
            
            _connections.Clear();
            
            if (_signalRService != null)
            {
                await _signalRService.DisposeAsync();
            }
            
            _connectionLock?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing SignalR connection manager");
        }
    }
}

/// <summary>
/// Hub connection information
/// </summary>
public class HubConnectionInfo
{
    public string HubName { get; set; } = string.Empty;
    public ConnectionState State { get; set; }
    public bool IsConnected { get; set; }
    public DateTime? ConnectedAt { get; set; }
    public DateTime? DisconnectedAt { get; set; }
    public int ReconnectAttempts { get; set; }
    public string? LastError { get; set; }
}

/// <summary>
/// Hub connection options
/// </summary>
public class HubConnectionOptions
{
    public int MaxReconnectAttempts { get; set; } = 10;
    public int BaseReconnectDelay { get; set; } = 5000;
    public int MaxReconnectDelay { get; set; } = 30000;
    public bool EnableMessageQueuing { get; set; } = true;
    public bool EnableAutoReconnect { get; set; } = true;
}

/// <summary>
/// Hub performance metrics
/// </summary>
public class HubMetrics
{
    public string HubName { get; set; } = string.Empty;
    public double AverageLatency { get; set; }
    public double LastLatency { get; set; }
    public double AverageConnectionTime { get; set; }
    public double LastConnectionTime { get; set; }
    public int MessageCount { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Connection state changed event args
/// </summary>
public class ConnectionStateChangedEventArgs : EventArgs
{
    public string HubName { get; set; } = string.Empty;
    public ConnectionState CurrentState { get; set; }
    public ConnectionState PreviousState { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Connection metrics event args
/// </summary>
public class ConnectionMetricsEventArgs : EventArgs
{
    public string HubName { get; set; } = string.Empty;
    public HubMetrics Metrics { get; set; } = new();
}