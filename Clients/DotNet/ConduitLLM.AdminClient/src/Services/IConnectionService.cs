using Microsoft.AspNetCore.SignalR.Client;
using ConduitLLM.AdminClient.Models;

namespace ConduitLLM.AdminClient.Services;

/// <summary>
/// Interface for managing SignalR connections and health checks for admin operations.
/// </summary>
public interface IConnectionService
{
    /// <summary>
    /// Event raised when a connection state changes.
    /// </summary>
    event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;
    
    /// <summary>
    /// Connects all SignalR hubs.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task that completes when all connections are established.</returns>
    Task ConnectAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Connects a specific SignalR hub.
    /// </summary>
    /// <param name="hubType">The hub type to connect.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if connection was successful.</returns>
    Task<bool> ConnectHubAsync(AdminHubType hubType, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Disconnects all SignalR hubs.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task that completes when all connections are closed.</returns>
    Task DisconnectAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Disconnects a specific SignalR hub.
    /// </summary>
    /// <param name="hubType">The hub type to disconnect.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task that completes when the connection is closed.</returns>
    Task DisconnectHubAsync(AdminHubType hubType, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets the current connection status of all hubs.
    /// </summary>
    /// <returns>Dictionary of hub names and their connection states.</returns>
    Dictionary<string, HubConnectionState> GetStatus();
    
    /// <summary>
    /// Gets detailed connection health information for all hubs.
    /// </summary>
    /// <returns>List of connection health information.</returns>
    List<ConnectionHealth> GetDetailedStatus();
    
    /// <summary>
    /// Gets the connection health for a specific hub.
    /// </summary>
    /// <param name="hubType">The hub type.</param>
    /// <returns>Connection health information.</returns>
    Task<ConnectionHealth> GetConnectionHealthAsync(AdminHubType hubType);
    
    /// <summary>
    /// Gets the connection status for a specific hub.
    /// </summary>
    /// <param name="hubType">The hub type to check.</param>
    /// <returns>The connection state of the hub.</returns>
    HubConnectionState GetHubStatus(AdminHubType hubType);
    
    /// <summary>
    /// Checks if all connections are established.
    /// </summary>
    /// <returns>True if all connections are established.</returns>
    bool IsConnected();
    
    /// <summary>
    /// Checks if all connections are fully established and ready.
    /// </summary>
    /// <returns>True if all connections are fully connected.</returns>
    bool IsFullyConnected();
    
    /// <summary>
    /// Checks if a specific hub is connected.
    /// </summary>
    /// <param name="hubType">The hub type to check.</param>
    /// <returns>True if the hub is connected.</returns>
    Task<bool> IsConnectedAsync(AdminHubType hubType);
    
    /// <summary>
    /// Waits for all connections to be established.
    /// </summary>
    /// <param name="timeoutMs">Timeout in milliseconds (default: 30000).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if all connections are established within timeout.</returns>
    Task<bool> WaitForConnectionAsync(int timeoutMs = 30000, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Reconnects all SignalR hubs.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task that completes when reconnection is complete.</returns>
    Task ReconnectAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Updates SignalR configuration dynamically.
    /// </summary>
    /// <param name="config">New configuration to apply.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task that completes when configuration is updated.</returns>
    Task UpdateConfigurationAsync(SignalRConfiguration config, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Performs a lightweight health check to verify the API is reachable.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the API is reachable.</returns>
    Task<bool> PingAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Performs a lightweight health check with a custom timeout.
    /// </summary>
    /// <param name="timeoutMs">Timeout in milliseconds.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the API is reachable within the timeout.</returns>
    Task<bool> PingWithTimeoutAsync(int timeoutMs, CancellationToken cancellationToken = default);
}