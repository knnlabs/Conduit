using Microsoft.Extensions.Logging;
using ConduitLLM.AdminClient.Client;
using ConduitLLM.AdminClient.SignalR;

namespace ConduitLLM.AdminClient.Services;

/// <summary>
/// Service for managing SignalR hub connections for real-time admin notifications.
/// </summary>
public class SignalRService : IAsyncDisposable
{
    private readonly ConduitAdminClientConfiguration _configuration;
    private readonly ILogger<SignalRService>? _logger;
    private readonly Dictionary<Type, BaseSignalRConnection> _connections = new();
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the SignalRService class.
    /// </summary>
    /// <param name="configuration">Client configuration.</param>
    /// <param name="logger">Optional logger instance.</param>
    public SignalRService(ConduitAdminClientConfiguration configuration, ILogger<SignalRService>? logger = null)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger;
    }

    /// <summary>
    /// Gets or creates an AdminNotificationHubClient for virtual key and configuration events.
    /// </summary>
    /// <returns>AdminNotificationHubClient instance.</returns>
    public AdminNotificationHubClient GetAdminNotificationHubClient()
    {
        return GetOrCreateConnection<AdminNotificationHubClient>(() => 
            new AdminNotificationHubClient(_configuration.AdminApiUrl, _configuration.MasterKey, 
                CreateLogger<AdminNotificationHubClient>()));
    }

    /// <summary>
    /// Gets or creates a NavigationStateHubClient for model discovery and provider health updates.
    /// </summary>
    /// <returns>NavigationStateHubClient instance.</returns>
    public NavigationStateHubClient GetNavigationStateHubClient()
    {
        return GetOrCreateConnection<NavigationStateHubClient>(() => 
            new NavigationStateHubClient(_configuration.AdminApiUrl, _configuration.MasterKey, 
                CreateLogger<NavigationStateHubClient>()));
    }

    /// <summary>
    /// Starts all active hub connections.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task StartAllConnectionsAsync(CancellationToken cancellationToken = default)
    {
        var startTasks = _connections.Values.Select(connection => connection.StartAsync(cancellationToken));
        await Task.WhenAll(startTasks);
        
        _logger?.LogInformation("Started {Count} SignalR connections", _connections.Count);
    }

    /// <summary>
    /// Stops all active hub connections.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task StopAllConnectionsAsync(CancellationToken cancellationToken = default)
    {
        var stopTasks = _connections.Values.Select(connection => connection.StopAsync(cancellationToken));
        await Task.WhenAll(stopTasks);
        
        _logger?.LogInformation("Stopped {Count} SignalR connections", _connections.Count);
    }

    /// <summary>
    /// Waits for all connections to be established.
    /// </summary>
    /// <param name="timeout">Maximum time to wait for all connections.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if all connections are established within timeout.</returns>
    public async Task<bool> WaitForAllConnectionsAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        using var timeoutCts = new CancellationTokenSource(timeout);
        using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            var waitTasks = _connections.Values.Select(connection => 
                connection.WaitForConnectionAsync(timeout, combinedCts.Token));
            
            var results = await Task.WhenAll(waitTasks);
            return results.All(r => r);
        }
        catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the connection status for all hub connections.
    /// </summary>
    /// <returns>Dictionary of connection types and their status.</returns>
    public Dictionary<string, string> GetConnectionStatus()
    {
        return _connections.ToDictionary(
            kvp => kvp.Key.Name,
            kvp => kvp.Value.State.ToString()
        );
    }

    /// <summary>
    /// Gets the connection states for admin hubs.
    /// </summary>
    /// <returns>Dictionary with hub names and their connection states.</returns>
    public Dictionary<string, Microsoft.AspNetCore.SignalR.Client.HubConnectionState> GetConnectionStates()
    {
        var states = new Dictionary<string, Microsoft.AspNetCore.SignalR.Client.HubConnectionState>();
        
        foreach (var kvp in _connections)
        {
            var hubName = kvp.Key.Name switch
            {
                nameof(NavigationStateHubClient) => "navigationState",
                nameof(AdminNotificationHubClient) => "adminNotifications",
                _ => kvp.Key.Name
            };
            
            states[hubName] = kvp.Value.State;
        }
        
        return states;
    }

    /// <summary>
    /// Checks if all connections are established.
    /// </summary>
    /// <returns>True if all connections are connected.</returns>
    public bool AreAllConnectionsEstablished()
    {
        return _connections.Values.All(connection => connection.IsConnected);
    }

    /// <summary>
    /// Gets or creates a connection of the specified type.
    /// </summary>
    /// <typeparam name="T">Connection type.</typeparam>
    /// <param name="factory">Factory function to create the connection.</param>
    /// <returns>Connection instance.</returns>
    private T GetOrCreateConnection<T>(Func<T> factory) where T : BaseSignalRConnection
    {
        if (_connections.TryGetValue(typeof(T), out var existingConnection))
        {
            return (T)existingConnection;
        }

        var newConnection = factory();
        _connections[typeof(T)] = newConnection;
        
        _logger?.LogDebug("Created new SignalR connection: {ConnectionType}", typeof(T).Name);
        
        return newConnection;
    }

    /// <summary>
    /// Disposes all SignalR connections.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            var disposeTasks = _connections.Values.Select(connection => connection.DisposeAsync().AsTask());
            await Task.WhenAll(disposeTasks);
            
            _connections.Clear();
            _disposed = true;
            
            _logger?.LogDebug("Disposed SignalRService and all connections");
        }
    }

    #region Convenience Methods

    /// <summary>
    /// Subscribes to events for a specific virtual key.
    /// </summary>
    /// <param name="virtualKeyId">Virtual key identifier to subscribe to.</param>
    public async Task SubscribeToVirtualKeyAsync(int virtualKeyId)
    {
        var adminHubClient = GetAdminNotificationHubClient();
        await adminHubClient.SubscribeToVirtualKey(virtualKeyId);
        
        _logger?.LogDebug("Subscribed to virtual key {VirtualKeyId}", virtualKeyId);
    }

    /// <summary>
    /// Unsubscribes from events for a specific virtual key.
    /// </summary>
    /// <param name="virtualKeyId">Virtual key identifier to unsubscribe from.</param>
    public async Task UnsubscribeFromVirtualKeyAsync(int virtualKeyId)
    {
        var adminHubClient = GetAdminNotificationHubClient();
        await adminHubClient.UnsubscribeFromVirtualKey(virtualKeyId);
        
        _logger?.LogDebug("Unsubscribed from virtual key {VirtualKeyId}", virtualKeyId);
    }

    /// <summary>
    /// Subscribes to events for a specific provider.
    /// </summary>
    /// <param name="providerName">Provider name to subscribe to.</param>
    public async Task SubscribeToProviderAsync(string providerName)
    {
        var adminHubClient = GetAdminNotificationHubClient();
        await adminHubClient.SubscribeToProvider(providerName);
        
        _logger?.LogDebug("Subscribed to provider {ProviderName}", providerName);
    }

    /// <summary>
    /// Unsubscribes from events for a specific provider.
    /// </summary>
    /// <param name="providerName">Provider name to unsubscribe from.</param>
    public async Task UnsubscribeFromProviderAsync(string providerName)
    {
        var adminHubClient = GetAdminNotificationHubClient();
        await adminHubClient.UnsubscribeFromProvider(providerName);
        
        _logger?.LogDebug("Unsubscribed from provider {ProviderName}", providerName);
    }

    #endregion

    /// <summary>
    /// Creates a logger for the specified type.
    /// </summary>
    /// <typeparam name="T">The type to create a logger for.</typeparam>
    /// <returns>A logger instance or null if no base logger is available.</returns>
    private ILogger<T>? CreateLogger<T>()
    {
        if (_logger == null) return null;
        
        // Simple logger wrapper that forwards to the base logger
        return new LoggerWrapper<T>(_logger);
    }

    /// <summary>
    /// Simple logger wrapper for type-specific logging.
    /// </summary>
    private class LoggerWrapper<T> : ILogger<T>
    {
        private readonly ILogger _baseLogger;
        
        public LoggerWrapper(ILogger baseLogger)
        {
            _baseLogger = baseLogger;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => _baseLogger.BeginScope(state);
        public bool IsEnabled(LogLevel logLevel) => _baseLogger.IsEnabled(logLevel);
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => _baseLogger.Log(logLevel, eventId, state, exception, formatter);
    }
}