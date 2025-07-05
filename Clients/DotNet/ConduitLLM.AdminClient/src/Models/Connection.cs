using Microsoft.AspNetCore.SignalR.Client;

namespace ConduitLLM.AdminClient.Models;

/// <summary>
/// Represents the health status of a SignalR connection.
/// </summary>
public class ConnectionHealth
{
    /// <summary>
    /// Gets or sets the hub name.
    /// </summary>
    public string HubName { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the connection state.
    /// </summary>
    public HubConnectionState State { get; set; }
    
    /// <summary>
    /// Gets or sets a human-readable description of the connection state.
    /// </summary>
    public string StateDescription { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets whether the connection is currently established.
    /// </summary>
    public bool IsConnected { get; set; }
    
    /// <summary>
    /// Gets or sets the timestamp of the last successful connection.
    /// </summary>
    public DateTime? LastConnected { get; set; }
    
    /// <summary>
    /// Gets or sets the estimated latency in milliseconds.
    /// </summary>
    public double? LatencyMs { get; set; }
    
    /// <summary>
    /// Gets or sets the number of reconnection attempts.
    /// </summary>
    public int ReconnectAttempts { get; set; }
    
    /// <summary>
    /// Gets or sets the last error message if any.
    /// </summary>
    public string? LastError { get; set; }
}

/// <summary>
/// Event arguments for connection state changes.
/// </summary>
public class ConnectionStateChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the hub name that changed state.
    /// </summary>
    public string HubName { get; }
    
    /// <summary>
    /// Gets the previous connection state.
    /// </summary>
    public HubConnectionState PreviousState { get; }
    
    /// <summary>
    /// Gets the new connection state.
    /// </summary>
    public HubConnectionState NewState { get; }
    
    /// <summary>
    /// Gets the timestamp of the state change.
    /// </summary>
    public DateTime Timestamp { get; }
    
    /// <summary>
    /// Initializes a new instance of the ConnectionStateChangedEventArgs class.
    /// </summary>
    /// <param name="hubName">The hub name.</param>
    /// <param name="previousState">The previous state.</param>
    /// <param name="newState">The new state.</param>
    public ConnectionStateChangedEventArgs(string hubName, HubConnectionState previousState, HubConnectionState newState)
    {
        HubName = hubName;
        PreviousState = previousState;
        NewState = newState;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Configuration for dynamic SignalR updates.
/// </summary>
public class SignalRConfiguration
{
    /// <summary>
    /// Gets or sets whether SignalR is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;
    
    /// <summary>
    /// Gets or sets whether to automatically connect on client initialization.
    /// </summary>
    public bool AutoConnect { get; set; } = true;
    
    /// <summary>
    /// Gets or sets the reconnect interval in milliseconds.
    /// </summary>
    public int ReconnectIntervalMs { get; set; } = 5000;
    
    /// <summary>
    /// Gets or sets the connection timeout in milliseconds.
    /// </summary>
    public int ConnectionTimeoutMs { get; set; } = 30000;
}

/// <summary>
/// Hub type enumeration for admin clients.
/// </summary>
public enum AdminHubType
{
    /// <summary>
    /// Navigation state hub for model discovery and provider health.
    /// </summary>
    NavigationState,
    
    /// <summary>
    /// Admin notifications hub for virtual key events and configuration changes.
    /// </summary>
    AdminNotifications
}