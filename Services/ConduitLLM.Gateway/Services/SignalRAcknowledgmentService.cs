using System.Collections.Concurrent;
using System.Text.Json;

using ConduitLLM.Configuration.Services;
using ConduitLLM.Gateway.Models;

using StackExchange.Redis;

namespace ConduitLLM.Gateway.Services
{
    /// <summary>
    /// Service that manages message acknowledgments for SignalR
    /// </summary>
    public interface ISignalRAcknowledgmentService
    {
        /// <summary>
        /// Registers a message for acknowledgment tracking
        /// </summary>
        Task<PendingAcknowledgment> RegisterMessageAsync(SignalRMessage message, string connectionId, string hubName, string methodName, TimeSpan? timeout = null);

        /// <summary>
        /// Acknowledges a message by its ID
        /// </summary>
        Task<bool> AcknowledgeMessageAsync(string messageId, string connectionId);

        /// <summary>
        /// Negatively acknowledges a message by its ID
        /// </summary>
        Task<bool> NackMessageAsync(string messageId, string connectionId, string? errorMessage = null);

        /// <summary>
        /// Gets the status of a message acknowledgment
        /// </summary>
        Task<AcknowledgmentStatus?> GetMessageStatusAsync(string messageId);

        /// <summary>
        /// Gets all pending acknowledgments for a connection
        /// </summary>
        Task<IEnumerable<PendingAcknowledgment>> GetPendingAcknowledgmentsAsync(string connectionId);

        /// <summary>
        /// Cleans up acknowledgments for a disconnected client
        /// </summary>
        Task CleanupConnectionAsync(string connectionId);
    }

    /// <summary>
    /// Implementation of SignalR acknowledgment service using Redis.
    /// Uses periodic timer scanning for timeouts instead of per-message Tasks to prevent memory leaks.
    /// </summary>
    public class SignalRAcknowledgmentService : ISignalRAcknowledgmentService, IHostedService, IDisposable
    {
        private readonly ILogger<SignalRAcknowledgmentService> _logger;
        private readonly IConfiguration _configuration;
        private readonly RedisConnectionFactory _redisConnectionFactory;

        private Timer? _cleanupTimer;
        private Timer? _timeoutScanTimer;
        private IDatabase? _redis;

        // Redis keys
        private readonly string _pendingAcknowledgmentsKey;
        private readonly string _connectionMessagesKeyPrefix;

        // Local timeout tracking - tracks messageId -> timeout time
        // This prevents creating one Task per message for timeout handling
        private readonly ConcurrentDictionary<string, DateTime> _pendingTimeouts = new();
        private readonly SemaphoreSlim _timeoutScanLock = new(1, 1);

        private readonly TimeSpan _defaultTimeout;
        private readonly TimeSpan _cleanupInterval;
        private readonly TimeSpan _timeoutScanInterval;
        private readonly int _maxRetryAttempts;

        public SignalRAcknowledgmentService(
            ILogger<SignalRAcknowledgmentService> logger,
            IConfiguration configuration,
            RedisConnectionFactory redisConnectionFactory)
        {
            _logger = logger;
            _configuration = configuration;
            _redisConnectionFactory = redisConnectionFactory;

            // Redis keys
            _pendingAcknowledgmentsKey = "signalr:acknowledgments";
            _connectionMessagesKeyPrefix = "signalr:conn_msgs";

            _defaultTimeout = TimeSpan.FromSeconds(configuration.GetValue<int>("SignalR:Acknowledgment:TimeoutSeconds", 30));
            _cleanupInterval = TimeSpan.FromMinutes(configuration.GetValue<int>("SignalR:Acknowledgment:CleanupIntervalMinutes", 5));
            _timeoutScanInterval = TimeSpan.FromSeconds(configuration.GetValue<int>("SignalR:Acknowledgment:TimeoutScanIntervalSeconds", 3));
            _maxRetryAttempts = configuration.GetValue<int>("SignalR:Acknowledgment:MaxRetryAttempts", 3);
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("SignalR Acknowledgment Service starting");

            try
            {
                var connection = await _redisConnectionFactory.GetConnectionAsync();
                _redis = connection.GetDatabase();

                _cleanupTimer = new Timer(
                    CleanupExpiredAcknowledgments,
                    null,
                    _cleanupInterval,
                    _cleanupInterval);

                // Start periodic timeout scanner - replaces per-message Task.Run
                _timeoutScanTimer = new Timer(
                    _ => _ = ScanForTimeoutsAsync(),
                    null,
                    _timeoutScanInterval,
                    _timeoutScanInterval);

                _logger.LogInformation(
                    "SignalR Acknowledgment Service started with Redis backend (timeout scan interval: {Interval}s)",
                    _timeoutScanInterval.TotalSeconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start SignalR Acknowledgment Service");
                throw;
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("SignalR Acknowledgment Service stopping");

            _cleanupTimer?.Change(Timeout.Infinite, 0);
            _timeoutScanTimer?.Change(Timeout.Infinite, 0);

            // Clear local timeout tracking - Redis will handle timeouts naturally via TTL
            _pendingTimeouts.Clear();

            return Task.CompletedTask;
        }

        public async Task<PendingAcknowledgment> RegisterMessageAsync(
            SignalRMessage message, 
            string connectionId, 
            string hubName, 
            string methodName, 
            TimeSpan? timeout = null)
        {
            if (_redis == null)
            {
                throw new InvalidOperationException("Redis not available for acknowledgment tracking");
            }
            
            var effectiveTimeout = timeout ?? _defaultTimeout;
            var timeoutAt = DateTime.UtcNow.Add(effectiveTimeout);

            var pending = new PendingAcknowledgment
            {
                Message = message,
                ConnectionId = connectionId,
                HubName = hubName,
                MethodName = methodName,
                TimeoutAt = timeoutAt,
                TimeoutTokenSource = new CancellationTokenSource()
            };

            try
            {
                // Store acknowledgment in Redis with TTL
                var pendingData = JsonSerializer.Serialize(pending);
                var key = $"{_pendingAcknowledgmentsKey}:{message.MessageId}";
                
                var wasSet = await _redis.StringSetAsync(key, pendingData, effectiveTimeout, false, When.NotExists, CommandFlags.None);
                if (!wasSet)
                {
                    _logger.LogWarning("Message {MessageId} already registered for acknowledgment", message.MessageId);
                    throw new InvalidOperationException($"Message {message.MessageId} already registered");
                }

                // Track message ID by connection
                var connectionKey = $"{_connectionMessagesKeyPrefix}:{connectionId}";
                await _redis.SetAddAsync(connectionKey, message.MessageId);
                await _redis.KeyExpireAsync(connectionKey, TimeSpan.FromHours(1)); // Cleanup connection tracking

                // Track timeout locally for periodic scanning - replaces per-message Task.Run
                // The periodic ScanForTimeoutsAsync will handle expired messages
                _pendingTimeouts.TryAdd(message.MessageId, timeoutAt);

                _logger.LogDebug(
                    "Registered message {MessageId} for acknowledgment on {HubName}.{MethodName} to {ConnectionId}, timeout at {TimeoutAt}",
                    message.MessageId, hubName, methodName, connectionId, timeoutAt);

                return pending;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register message {MessageId} for acknowledgment", message.MessageId);
                throw;
            }
        }

        public async Task<bool> AcknowledgeMessageAsync(string messageId, string connectionId)
        {
            if (_redis == null)
            {
                _logger.LogWarning("Redis not available, cannot acknowledge message {MessageId}", messageId);
                return false;
            }

            try
            {
                var key = $"{_pendingAcknowledgmentsKey}:{messageId}";
                var pendingData = await _redis.StringGetAsync(key);
                
                if (!pendingData.HasValue)
                {
                    _logger.LogWarning("Attempted to acknowledge unknown message {MessageId}", messageId);
                    return false;
                }

                var pending = JsonSerializer.Deserialize<PendingAcknowledgment>(pendingData.ToString());
                if (pending == null)
                {
                    _logger.LogWarning("Failed to deserialize pending acknowledgment for message {MessageId}", messageId);
                    return false;
                }

                if (pending.ConnectionId != connectionId)
                {
                    _logger.LogWarning(
                        "Connection {ConnectionId} attempted to acknowledge message {MessageId} sent to {OriginalConnectionId}",
                        connectionId, messageId, pending.ConnectionId);
                    return false;
                }

                // Update status and mark as acknowledged
                pending.Status = AcknowledgmentStatus.Acknowledged;
                pending.AcknowledgedAt = DateTime.UtcNow;
                pending.TimeoutTokenSource?.Cancel();
                pending.CompletionSource.TrySetResult(true);

                // Remove from Redis
                await _redis.KeyDeleteAsync(key);

                // Remove from connection tracking
                var connectionKey = $"{_connectionMessagesKeyPrefix}:{connectionId}";
                await _redis.SetRemoveAsync(connectionKey, messageId);

                // Remove from local timeout tracking
                _pendingTimeouts.TryRemove(messageId, out _);

                _logger.LogDebug(
                    "Message {MessageId} acknowledged by {ConnectionId}, RTT: {RoundTripTime}ms",
                    messageId, connectionId, pending.RoundTripTime?.TotalMilliseconds ?? 0);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to acknowledge message {MessageId}", messageId);
                return false;
            }
        }

        public async Task<bool> NackMessageAsync(string messageId, string connectionId, string? errorMessage = null)
        {
            if (_redis == null)
            {
                _logger.LogWarning("Redis not available, cannot NACK message {MessageId}", messageId);
                return false;
            }

            try
            {
                var key = $"{_pendingAcknowledgmentsKey}:{messageId}";
                var pendingData = await _redis.StringGetAsync(key);
                
                if (!pendingData.HasValue)
                {
                    _logger.LogWarning("Attempted to NACK unknown message {MessageId}", messageId);
                    return false;
                }

                var pending = JsonSerializer.Deserialize<PendingAcknowledgment>(pendingData.ToString());
                if (pending == null)
                {
                    _logger.LogWarning("Failed to deserialize pending acknowledgment for message {MessageId}", messageId);
                    return false;
                }

                if (pending.ConnectionId != connectionId)
                {
                    _logger.LogWarning(
                        "Connection {ConnectionId} attempted to NACK message {MessageId} sent to {OriginalConnectionId}",
                        connectionId, messageId, pending.ConnectionId);
                    return false;
                }

                // Update status and mark as NACK'd
                pending.Status = AcknowledgmentStatus.NegativelyAcknowledged;
                pending.ErrorMessage = errorMessage;
                pending.AcknowledgedAt = DateTime.UtcNow;
                pending.TimeoutTokenSource?.Cancel();
                pending.CompletionSource.TrySetResult(false);

                // Remove from Redis
                await _redis.KeyDeleteAsync(key);

                // Remove from connection tracking
                var connectionKey = $"{_connectionMessagesKeyPrefix}:{connectionId}";
                await _redis.SetRemoveAsync(connectionKey, messageId);

                // Remove from local timeout tracking
                _pendingTimeouts.TryRemove(messageId, out _);

                _logger.LogWarning(
                    "Message {MessageId} negatively acknowledged by {ConnectionId}: {ErrorMessage}",
                    messageId, connectionId, errorMessage ?? "No error message provided");

                // Should retry if under retry limit and message is critical
                if (pending.Message.IsCritical && pending.Message.RetryCount < _maxRetryAttempts)
                {
                    _logger.LogInformation(
                        "Queueing critical message {MessageId} for retry (attempt {RetryCount}/{MaxRetries})",
                        messageId, pending.Message.RetryCount + 1, _maxRetryAttempts);
                    // Message will be picked up by the message queue service for retry
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to NACK message {MessageId}", messageId);
                return false;
            }
        }

        public async Task<AcknowledgmentStatus?> GetMessageStatusAsync(string messageId)
        {
            if (_redis == null)
            {
                return null;
            }

            try
            {
                var key = $"{_pendingAcknowledgmentsKey}:{messageId}";
                var pendingData = await _redis.StringGetAsync(key);
                
                if (!pendingData.HasValue)
                {
                    return null;
                }

                var pending = JsonSerializer.Deserialize<PendingAcknowledgment>(pendingData.ToString());
                return pending?.Status;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get status for message {MessageId}", messageId);
                return null;
            }
        }

        public async Task<IEnumerable<PendingAcknowledgment>> GetPendingAcknowledgmentsAsync(string connectionId)
        {
            if (_redis == null)
            {
                return Enumerable.Empty<PendingAcknowledgment>();
            }

            try
            {
                var connectionKey = $"{_connectionMessagesKeyPrefix}:{connectionId}";
                var messageIds = await _redis.SetMembersAsync(connectionKey);
                
                if (messageIds.Length == 0)
                {
                    return Enumerable.Empty<PendingAcknowledgment>();
                }

                var pendingAcks = new List<PendingAcknowledgment>();
                
                foreach (var messageId in messageIds)
                {
                    try
                    {
                        var key = $"{_pendingAcknowledgmentsKey}:{messageId}";
                        var pendingData = await _redis.StringGetAsync(key);
                        
                        if (pendingData.HasValue)
                        {
                            var pending = JsonSerializer.Deserialize<PendingAcknowledgment>(pendingData.ToString());
                            if (pending != null && pending.Status == AcknowledgmentStatus.Pending)
                            {
                                pendingAcks.Add(pending);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to deserialize pending acknowledgment for message {MessageId}", messageId);
                    }
                }

                return pendingAcks;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get pending acknowledgments for connection {ConnectionId}", connectionId);
                return Enumerable.Empty<PendingAcknowledgment>();
            }
        }

        public async Task CleanupConnectionAsync(string connectionId)
        {
            _logger.LogInformation("Cleaning up acknowledgments for disconnected connection {ConnectionId}", connectionId);

            if (_redis == null)
            {
                return;
            }

            try
            {
                var connectionKey = $"{_connectionMessagesKeyPrefix}:{connectionId}";
                var messageIds = await _redis.SetMembersAsync(connectionKey);

                if (messageIds.Length == 0)
                {
                    return;
                }

                foreach (var messageId in messageIds)
                {
                    // Remove from local timeout tracking
                    _pendingTimeouts.TryRemove(messageId.ToString(), out _);

                    try
                    {
                        var key = $"{_pendingAcknowledgmentsKey}:{messageId}";
                        var pendingData = await _redis.StringGetAsync(key);

                        if (pendingData.HasValue)
                        {
                            var pending = JsonSerializer.Deserialize<PendingAcknowledgment>(pendingData.ToString());
                            if (pending != null && pending.Status == AcknowledgmentStatus.Pending)
                            {
                                pending.Status = AcknowledgmentStatus.Failed;
                                pending.ErrorMessage = "Connection disconnected";
                                pending.TimeoutTokenSource?.Cancel();
                                pending.CompletionSource.TrySetResult(false);

                                // Remove from Redis
                                await _redis.KeyDeleteAsync(key);

                                _logger.LogWarning(
                                    "Message {MessageId} failed due to connection {ConnectionId} disconnect",
                                    messageId, connectionId);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error cleaning up message {MessageId} for connection {ConnectionId}", messageId, connectionId);
                    }
                }

                // Remove the connection tracking set
                await _redis.KeyDeleteAsync(connectionKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cleanup acknowledgments for connection {ConnectionId}", connectionId);
            }
        }

        private async Task HandleTimeoutAsync(string messageId)
        {
            // Ensure removed from local tracking (may already be removed by ScanForTimeoutsAsync)
            _pendingTimeouts.TryRemove(messageId, out _);

            if (_redis == null)
            {
                return;
            }

            try
            {
                var key = $"{_pendingAcknowledgmentsKey}:{messageId}";
                var pendingData = await _redis.StringGetAsync(key);

                if (!pendingData.HasValue)
                {
                    return; // Already processed or expired
                }

                var pending = JsonSerializer.Deserialize<PendingAcknowledgment>(pendingData.ToString());
                if (pending == null || pending.Status != AcknowledgmentStatus.Pending)
                {
                    return;
                }

                pending.Status = AcknowledgmentStatus.TimedOut;
                pending.CompletionSource.TrySetResult(false);

                // Remove from Redis
                await _redis.KeyDeleteAsync(key);

                // Remove from connection tracking
                var connectionKey = $"{_connectionMessagesKeyPrefix}:{pending.ConnectionId}";
                await _redis.SetRemoveAsync(connectionKey, messageId);

                _logger.LogWarning(
                    "Message {MessageId} timed out after {Timeout}ms on {HubName}.{MethodName} to {ConnectionId}",
                    messageId,
                    (DateTime.UtcNow - pending.SentAt).TotalMilliseconds,
                    pending.HubName,
                    pending.MethodName,
                    pending.ConnectionId);

                // Should retry if under retry limit and message is critical
                if (pending.Message.IsCritical && pending.Message.RetryCount < _maxRetryAttempts)
                {
                    _logger.LogInformation(
                        "Queueing critical message {MessageId} for retry after timeout (attempt {RetryCount}/{MaxRetries})",
                        messageId, pending.Message.RetryCount + 1, _maxRetryAttempts);
                    // Message will be picked up by the message queue service for retry
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling timeout for message {MessageId}", messageId);
            }
        }

        private void CleanupExpiredAcknowledgments(object? state)
        {
            // Redis TTL automatically handles cleanup of expired acknowledgments
            // This cleanup is mainly handled by Redis expiration, so minimal work needed here

            try
            {
                _logger.LogTrace("Acknowledgment cleanup timer executed - Redis handles TTL automatically");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during acknowledgment cleanup");
            }
        }

        /// <summary>
        /// Periodically scans for timed-out messages and handles them in batches.
        /// This replaces per-message Task.Run to prevent memory leaks under high load.
        /// </summary>
        private async Task ScanForTimeoutsAsync()
        {
            if (!await _timeoutScanLock.WaitAsync(0))
            {
                // Another scan is in progress
                return;
            }

            try
            {
                var now = DateTime.UtcNow;

                // Find all expired messages
                var timedOutIds = _pendingTimeouts
                    .Where(kvp => kvp.Value <= now)
                    .Select(kvp => kvp.Key)
                    .ToList();

                if (timedOutIds.Count > 0)
                {
                    _logger.LogDebug("Timeout scan found {Count} expired messages", timedOutIds.Count);
                }

                foreach (var messageId in timedOutIds)
                {
                    // Remove from local tracking first
                    _pendingTimeouts.TryRemove(messageId, out _);

                    try
                    {
                        await HandleTimeoutAsync(messageId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error handling timeout for message {MessageId}", messageId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during timeout scan");
            }
            finally
            {
                _timeoutScanLock.Release();
            }
        }

        public void Dispose()
        {
            _cleanupTimer?.Dispose();
            _timeoutScanTimer?.Dispose();
            _timeoutScanLock?.Dispose();
            _pendingTimeouts.Clear();
        }
    }
}