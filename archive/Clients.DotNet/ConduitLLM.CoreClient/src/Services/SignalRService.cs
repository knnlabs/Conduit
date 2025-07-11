using Microsoft.Extensions.Logging;
using ConduitLLM.CoreClient.Client;
using ConduitLLM.CoreClient.SignalR;

namespace ConduitLLM.CoreClient.Services;

/// <summary>
/// Service for managing SignalR hub connections for real-time notifications.
/// </summary>
public class SignalRService : IAsyncDisposable
{
    private readonly ConduitCoreClientConfiguration _configuration;
    private readonly ILogger<SignalRService>? _logger;
    private readonly Dictionary<Type, BaseSignalRConnection> _connections = new();
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the SignalRService class.
    /// </summary>
    /// <param name="configuration">Client configuration.</param>
    /// <param name="logger">Optional logger instance.</param>
    public SignalRService(ConduitCoreClientConfiguration configuration, ILogger<SignalRService>? logger = null)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger;
    }

    /// <summary>
    /// Gets or creates a TaskHubClient for task progress notifications.
    /// </summary>
    /// <returns>TaskHubClient instance.</returns>
    public TaskHubClient GetTaskHubClient()
    {
        return GetOrCreateConnection<TaskHubClient>(() => 
            new TaskHubClient(_configuration.BaseUrl, _configuration.VirtualKey, 
                CreateLogger<TaskHubClient>()));
    }

    /// <summary>
    /// Gets or creates a VideoGenerationHubClient for video generation notifications.
    /// </summary>
    /// <returns>VideoGenerationHubClient instance.</returns>
    public VideoGenerationHubClient GetVideoGenerationHubClient()
    {
        return GetOrCreateConnection<VideoGenerationHubClient>(() => 
            new VideoGenerationHubClient(_configuration.BaseUrl, _configuration.VirtualKey, 
                CreateLogger<VideoGenerationHubClient>()));
    }

    /// <summary>
    /// Gets or creates an ImageGenerationHubClient for image generation notifications.
    /// </summary>
    /// <returns>ImageGenerationHubClient instance.</returns>
    public ImageGenerationHubClient GetImageGenerationHubClient()
    {
        return GetOrCreateConnection<ImageGenerationHubClient>(() => 
            new ImageGenerationHubClient(_configuration.BaseUrl, _configuration.VirtualKey, 
                CreateLogger<ImageGenerationHubClient>()));
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
    /// Subscribes to a task across all relevant hubs.
    /// </summary>
    /// <param name="taskId">Task identifier to subscribe to.</param>
    /// <param name="taskType">Optional task type for targeted subscriptions.</param>
    public async Task SubscribeToTaskAsync(string taskId, string? taskType = null)
    {
        var taskHubClient = GetTaskHubClient();
        await taskHubClient.SubscribeToTask(taskId);

        // Subscribe to specialized hubs based on task type
        if (taskType?.Contains("video", StringComparison.OrdinalIgnoreCase) == true)
        {
            var videoHubClient = GetVideoGenerationHubClient();
            await videoHubClient.SubscribeToTask(taskId);
        }
        else if (taskType?.Contains("image", StringComparison.OrdinalIgnoreCase) == true)
        {
            var imageHubClient = GetImageGenerationHubClient();
            await imageHubClient.SubscribeToTask(taskId);
        }

        _logger?.LogDebug("Subscribed to task {TaskId} with type {TaskType}", taskId, taskType);
    }

    /// <summary>
    /// Unsubscribes from a task across all relevant hubs.
    /// </summary>
    /// <param name="taskId">Task identifier to unsubscribe from.</param>
    /// <param name="taskType">Optional task type for targeted unsubscriptions.</param>
    public async Task UnsubscribeFromTaskAsync(string taskId, string? taskType = null)
    {
        var taskHubClient = GetTaskHubClient();
        await taskHubClient.UnsubscribeFromTask(taskId);

        // Unsubscribe from specialized hubs based on task type
        if (taskType?.Contains("video", StringComparison.OrdinalIgnoreCase) == true)
        {
            var videoHubClient = GetVideoGenerationHubClient();
            await videoHubClient.UnsubscribeFromTask(taskId);
        }
        else if (taskType?.Contains("image", StringComparison.OrdinalIgnoreCase) == true)
        {
            var imageHubClient = GetImageGenerationHubClient();
            await imageHubClient.UnsubscribeFromTask(taskId);
        }

        _logger?.LogDebug("Unsubscribed from task {TaskId} with type {TaskType}", taskId, taskType);
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