using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using System.Net;

namespace ConduitLLM.CoreClient.SignalR;

/// <summary>
/// Base class for SignalR hub connections with automatic reconnection and error handling.
/// </summary>
public abstract class BaseSignalRConnection : IAsyncDisposable
{
    protected HubConnection? _connection;
    protected readonly string _virtualKey;
    protected readonly string _baseUrl;
    protected readonly ILogger? _logger;
    protected readonly TaskCompletionSource<bool> _connectionReadyTcs = new();
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the BaseSignalRConnection class.
    /// </summary>
    /// <param name="baseUrl">Base URL of the Conduit Core API.</param>
    /// <param name="virtualKey">Virtual key for authentication.</param>
    /// <param name="logger">Optional logger instance.</param>
    protected BaseSignalRConnection(string baseUrl, string virtualKey, ILogger? logger = null)
    {
        _baseUrl = baseUrl?.TrimEnd('/') ?? throw new ArgumentNullException(nameof(baseUrl));
        _virtualKey = virtualKey ?? throw new ArgumentNullException(nameof(virtualKey));
        _logger = logger;
    }

    /// <summary>
    /// Gets the hub path for this connection type.
    /// </summary>
    protected abstract string HubPath { get; }

    /// <summary>
    /// Gets whether the connection is established and ready for use.
    /// </summary>
    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    /// <summary>
    /// Gets the current connection state.
    /// </summary>
    public HubConnectionState State => _connection?.State ?? HubConnectionState.Disconnected;

    /// <summary>
    /// Event raised when the connection is established.
    /// </summary>
    public event Func<Task>? Connected;

    /// <summary>
    /// Event raised when the connection is closed.
    /// </summary>
    public event Func<Exception?, Task>? Disconnected;

    /// <summary>
    /// Event raised when attempting to reconnect.
    /// </summary>
    public event Func<Exception?, Task>? Reconnecting;

    /// <summary>
    /// Event raised when reconnection is successful.
    /// </summary>
    public event Func<string?, Task>? Reconnected;

    /// <summary>
    /// Establishes the SignalR connection.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The established hub connection.</returns>
    protected virtual async Task<HubConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (_connection == null)
        {
            var hubUrl = $"{_baseUrl}{HubPath}";
            
            _connection = new HubConnectionBuilder()
                .WithUrl(hubUrl, options =>
                {
                    // Add authentication via query parameter
                    options.Headers.Add("Authorization", $"Bearer {_virtualKey}");
                    
                    // Configure transport options
                    options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets 
                                       | Microsoft.AspNetCore.Http.Connections.HttpTransportType.ServerSentEvents;
                    
                    // Configure timeouts
                    options.CloseTimeout = TimeSpan.FromSeconds(30);
                    
                    // Add user agent
                    options.Headers.Add("User-Agent", "ConduitLLM.CoreClient/1.0.0");
                })
                .WithAutomaticReconnect(new[] { 
                    TimeSpan.Zero, 
                    TimeSpan.FromSeconds(2), 
                    TimeSpan.FromSeconds(10), 
                    TimeSpan.FromSeconds(30) 
                })
                .ConfigureLogging(logging =>
                {
                    if (_logger != null)
                    {
                        logging.SetMinimumLevel(LogLevel.Information);
                    }
                })
                .Build();

            // Set up event handlers
            _connection.Closed += OnConnectionClosed;
            _connection.Reconnecting += OnReconnecting;
            _connection.Reconnected += OnReconnected;

            // Configure hub-specific handlers
            ConfigureHubHandlers(_connection);

            try
            {
                await _connection.StartAsync(cancellationToken);
                _logger?.LogInformation("SignalR connection established to {HubPath}", HubPath);
                
                _connectionReadyTcs.TrySetResult(true);
                
                if (Connected != null)
                {
                    await Connected.Invoke();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to establish SignalR connection to {HubPath}", HubPath);
                _connectionReadyTcs.TrySetException(ex);
                throw;
            }
        }

        return _connection;
    }

    /// <summary>
    /// Configures hub-specific event handlers. Override in derived classes.
    /// </summary>
    /// <param name="connection">The hub connection to configure.</param>
    protected abstract void ConfigureHubHandlers(HubConnection connection);

    /// <summary>
    /// Waits for the connection to be established.
    /// </summary>
    /// <param name="timeout">Maximum time to wait for connection.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if connection is established within timeout.</returns>
    public async Task<bool> WaitForConnectionAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        using var timeoutCts = new CancellationTokenSource(timeout);
        using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            await _connectionReadyTcs.Task.ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
        {
            return false;
        }
    }

    /// <summary>
    /// Starts the SignalR connection.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await GetConnectionAsync(cancellationToken);
    }

    /// <summary>
    /// Stops the SignalR connection.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_connection != null)
        {
            try
            {
                await _connection.StopAsync(cancellationToken);
                _logger?.LogInformation("SignalR connection stopped for {HubPath}", HubPath);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error stopping SignalR connection for {HubPath}", HubPath);
            }
        }
    }

    /// <summary>
    /// Invokes a hub method with retry logic.
    /// </summary>
    /// <param name="methodName">Method name to invoke.</param>
    /// <param name="args">Method arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    protected async Task InvokeAsync(string methodName, object?[] args, CancellationToken cancellationToken = default)
    {
        var connection = await GetConnectionAsync(cancellationToken);
        
        const int maxRetries = 3;
        var retryDelay = TimeSpan.FromSeconds(1);
        
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                await connection.InvokeAsync(methodName, args, cancellationToken);
                return;
            }
            catch (Exception ex) when (attempt < maxRetries && IsRetryableException(ex))
            {
                _logger?.LogWarning(ex, "Hub method {MethodName} failed, attempt {Attempt}/{MaxRetries}", 
                    methodName, attempt, maxRetries);
                
                await Task.Delay(retryDelay * attempt, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Invokes a hub method with return value and retry logic.
    /// </summary>
    /// <typeparam name="T">Return type.</typeparam>
    /// <param name="methodName">Method name to invoke.</param>
    /// <param name="args">Method arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Method result.</returns>
    protected async Task<T> InvokeAsync<T>(string methodName, object?[] args, CancellationToken cancellationToken = default)
    {
        var connection = await GetConnectionAsync(cancellationToken);
        
        const int maxRetries = 3;
        var retryDelay = TimeSpan.FromSeconds(1);
        
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                return await connection.InvokeAsync<T>(methodName, args, cancellationToken);
            }
            catch (Exception ex) when (attempt < maxRetries && IsRetryableException(ex))
            {
                _logger?.LogWarning(ex, "Hub method {MethodName} failed, attempt {Attempt}/{MaxRetries}", 
                    methodName, attempt, maxRetries);
                
                await Task.Delay(retryDelay * attempt, cancellationToken);
            }
        }
        
        // This should never be reached due to the retry logic, but needed for compilation
        throw new InvalidOperationException($"Failed to invoke {methodName} after {maxRetries} attempts");
    }

    /// <summary>
    /// Determines if an exception is retryable.
    /// </summary>
    /// <param name="exception">Exception to check.</param>
    /// <returns>True if the exception is retryable.</returns>
    private static bool IsRetryableException(Exception exception)
    {
        return exception switch
        {
            HttpRequestException httpEx when httpEx.Data.Contains("StatusCode") => 
                ((HttpStatusCode)httpEx.Data["StatusCode"]!) is 
                    HttpStatusCode.InternalServerError or 
                    HttpStatusCode.BadGateway or 
                    HttpStatusCode.ServiceUnavailable or 
                    HttpStatusCode.GatewayTimeout,
            TaskCanceledException => false, // Don't retry cancellation
            OperationCanceledException => false, // Don't retry cancellation
            _ => true // Retry other exceptions
        };
    }

    private async Task OnConnectionClosed(Exception? exception)
    {
        _logger?.LogWarning(exception, "SignalR connection closed for {HubPath}", HubPath);
        
        if (Disconnected != null)
        {
            await Disconnected.Invoke(exception);
        }
    }

    private async Task OnReconnecting(Exception? exception)
    {
        _logger?.LogInformation("SignalR reconnecting for {HubPath}: {Exception}", HubPath, exception?.Message);
        
        if (Reconnecting != null)
        {
            await Reconnecting.Invoke(exception);
        }
    }

    private async Task OnReconnected(string? connectionId)
    {
        _logger?.LogInformation("SignalR reconnected for {HubPath} with connection ID: {ConnectionId}", HubPath, connectionId);
        
        if (Reconnected != null)
        {
            await Reconnected.Invoke(connectionId);
        }
    }

    /// <summary>
    /// Disposes the SignalR connection.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            if (_connection != null)
            {
                try
                {
                    await _connection.DisposeAsync();
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Error disposing SignalR connection for {HubPath}", HubPath);
                }
            }
            
            _disposed = true;
        }
    }
}