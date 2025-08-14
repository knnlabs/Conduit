using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Http.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Http.Services
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
    /// Implementation of SignalR acknowledgment service
    /// </summary>
    public class SignalRAcknowledgmentService : ISignalRAcknowledgmentService, IHostedService, IDisposable
    {
        private readonly ILogger<SignalRAcknowledgmentService> _logger;
        private readonly IConfiguration _configuration;
        private readonly ConcurrentDictionary<string, PendingAcknowledgment> _pendingAcknowledgments = new();
        private readonly ConcurrentDictionary<string, ConcurrentBag<string>> _connectionMessageIds = new();
        private Timer? _cleanupTimer;
        private readonly TimeSpan _defaultTimeout;
        private readonly TimeSpan _cleanupInterval;
        private readonly int _maxRetryAttempts;

        public SignalRAcknowledgmentService(
            ILogger<SignalRAcknowledgmentService> logger,
            IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;

            _defaultTimeout = TimeSpan.FromSeconds(configuration.GetValue<int>("SignalR:Acknowledgment:TimeoutSeconds", 30));
            _cleanupInterval = TimeSpan.FromMinutes(configuration.GetValue<int>("SignalR:Acknowledgment:CleanupIntervalMinutes", 5));
            _maxRetryAttempts = configuration.GetValue<int>("SignalR:Acknowledgment:MaxRetryAttempts", 3);
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("SignalR Acknowledgment Service starting");
            
            _cleanupTimer = new Timer(
                CleanupExpiredAcknowledgments,
                null,
                _cleanupInterval,
                _cleanupInterval);

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("SignalR Acknowledgment Service stopping");
            
            _cleanupTimer?.Change(Timeout.Infinite, 0);

            // Cancel all pending acknowledgments
            foreach (var pending in _pendingAcknowledgments.Values)
            {
                pending.TimeoutTokenSource?.Cancel();
                pending.CompletionSource.TrySetCanceled();
            }

            return Task.CompletedTask;
        }

        public Task<PendingAcknowledgment> RegisterMessageAsync(
            SignalRMessage message, 
            string connectionId, 
            string hubName, 
            string methodName, 
            TimeSpan? timeout = null)
        {
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

            if (!_pendingAcknowledgments.TryAdd(message.MessageId, pending))
            {
                _logger.LogWarning("Message {MessageId} already registered for acknowledgment", message.MessageId);
                throw new InvalidOperationException($"Message {message.MessageId} already registered");
            }

            // Track message ID by connection
            _connectionMessageIds.AddOrUpdate(
                connectionId,
                new ConcurrentBag<string> { message.MessageId },
                (_, bag) => { bag.Add(message.MessageId); return bag; });

            // Schedule timeout handling
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(effectiveTimeout, pending.TimeoutTokenSource.Token);
                    await HandleTimeoutAsync(message.MessageId);
                }
                catch (TaskCanceledException)
                {
                    // Expected when acknowledgment is received before timeout
                }
            });

            _logger.LogDebug(
                "Registered message {MessageId} for acknowledgment on {HubName}.{MethodName} to {ConnectionId}, timeout at {TimeoutAt}",
                message.MessageId, hubName, methodName, connectionId, timeoutAt);

            return Task.FromResult(pending);
        }

        public Task<bool> AcknowledgeMessageAsync(string messageId, string connectionId)
        {
            if (!_pendingAcknowledgments.TryGetValue(messageId, out var pending))
            {
                _logger.LogWarning("Attempted to acknowledge unknown message {MessageId}", messageId);
                return Task.FromResult(false);
            }

            if (pending.ConnectionId != connectionId)
            {
                _logger.LogWarning(
                    "Connection {ConnectionId} attempted to acknowledge message {MessageId} sent to {OriginalConnectionId}",
                    connectionId, messageId, pending.ConnectionId);
                return Task.FromResult(false);
            }

            pending.Status = AcknowledgmentStatus.Acknowledged;
            pending.AcknowledgedAt = DateTime.UtcNow;
            pending.TimeoutTokenSource?.Cancel();
            pending.CompletionSource.TrySetResult(true);

            _logger.LogDebug(
                "Message {MessageId} acknowledged by {ConnectionId}, RTT: {RoundTripTime}ms",
                messageId, connectionId, pending.RoundTripTime?.TotalMilliseconds ?? 0);

            // Clean up after a delay
            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromMinutes(1));
                _pendingAcknowledgments.TryRemove(messageId, out _);
                RemoveMessageIdFromConnection(connectionId, messageId);
            });

            return Task.FromResult(true);
        }

        public Task<bool> NackMessageAsync(string messageId, string connectionId, string? errorMessage = null)
        {
            if (!_pendingAcknowledgments.TryGetValue(messageId, out var pending))
            {
                _logger.LogWarning("Attempted to NACK unknown message {MessageId}", messageId);
                return Task.FromResult(false);
            }

            if (pending.ConnectionId != connectionId)
            {
                _logger.LogWarning(
                    "Connection {ConnectionId} attempted to NACK message {MessageId} sent to {OriginalConnectionId}",
                    connectionId, messageId, pending.ConnectionId);
                return Task.FromResult(false);
            }

            pending.Status = AcknowledgmentStatus.NegativelyAcknowledged;
            pending.ErrorMessage = errorMessage;
            pending.AcknowledgedAt = DateTime.UtcNow;
            pending.TimeoutTokenSource?.Cancel();
            pending.CompletionSource.TrySetResult(false);

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

            return Task.FromResult(true);
        }

        public Task<AcknowledgmentStatus?> GetMessageStatusAsync(string messageId)
        {
            if (_pendingAcknowledgments.TryGetValue(messageId, out var pending))
            {
                return Task.FromResult<AcknowledgmentStatus?>(pending.Status);
            }
            return Task.FromResult<AcknowledgmentStatus?>(null);
        }

        public Task<IEnumerable<PendingAcknowledgment>> GetPendingAcknowledgmentsAsync(string connectionId)
        {
            var messageIds = _connectionMessageIds.TryGetValue(connectionId, out var bag) 
                ? bag.ToList() 
                : new List<string>();

            var pendingAcks = messageIds
                .Select(id => _pendingAcknowledgments.TryGetValue(id, out var pending) ? pending : null)
                .Where(p => p != null && p.Status == AcknowledgmentStatus.Pending)
                .Cast<PendingAcknowledgment>();

            return Task.FromResult(pendingAcks);
        }

        public async Task CleanupConnectionAsync(string connectionId)
        {
            _logger.LogInformation("Cleaning up acknowledgments for disconnected connection {ConnectionId}", connectionId);

            if (!_connectionMessageIds.TryRemove(connectionId, out var messageIds))
            {
                return;
            }

            foreach (var messageId in messageIds)
            {
                if (_pendingAcknowledgments.TryGetValue(messageId, out var pending) && 
                    pending.Status == AcknowledgmentStatus.Pending)
                {
                    pending.Status = AcknowledgmentStatus.Failed;
                    pending.ErrorMessage = "Connection disconnected";
                    pending.TimeoutTokenSource?.Cancel();
                    pending.CompletionSource.TrySetResult(false);

                    _logger.LogWarning(
                        "Message {MessageId} failed due to connection {ConnectionId} disconnect",
                        messageId, connectionId);
                }
            }

            await Task.CompletedTask;
        }

        private async Task HandleTimeoutAsync(string messageId)
        {
            if (!_pendingAcknowledgments.TryGetValue(messageId, out var pending))
            {
                return;
            }

            if (pending.Status != AcknowledgmentStatus.Pending)
            {
                return;
            }

            pending.Status = AcknowledgmentStatus.TimedOut;
            pending.CompletionSource.TrySetResult(false);

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

            await Task.CompletedTask;
        }

        private void CleanupExpiredAcknowledgments(object? state)
        {
            try
            {
                var cutoffTime = DateTime.UtcNow.AddHours(-1);
                var expiredKeys = _pendingAcknowledgments
                    .Where(kvp => kvp.Value.SentAt < cutoffTime && 
                                  kvp.Value.Status != AcknowledgmentStatus.Pending)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in expiredKeys)
                {
                    if (_pendingAcknowledgments.TryRemove(key, out var pending))
                    {
                        RemoveMessageIdFromConnection(pending.ConnectionId, key);
                        pending.TimeoutTokenSource?.Dispose();
                    }
                }

                if (expiredKeys.Count() > 0)
                {
                    _logger.LogDebug("Cleaned up {Count} expired acknowledgments", expiredKeys.Count);
                }

                // Also check for messages that have expired
                var expiredMessages = _pendingAcknowledgments
                    .Where(kvp => kvp.Value.Message.IsExpired && 
                                  kvp.Value.Status == AcknowledgmentStatus.Pending)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in expiredMessages)
                {
                    if (_pendingAcknowledgments.TryGetValue(key, out var pending))
                    {
                        pending.Status = AcknowledgmentStatus.Expired;
                        pending.TimeoutTokenSource?.Cancel();
                        pending.CompletionSource.TrySetResult(false);
                        _logger.LogWarning("Message {MessageId} expired before delivery", key);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during acknowledgment cleanup");
            }
        }

        private void RemoveMessageIdFromConnection(string connectionId, string messageId)
        {
            if (_connectionMessageIds.TryGetValue(connectionId, out var bag))
            {
                var newBag = new ConcurrentBag<string>(bag.Where(id => id != messageId));
                _connectionMessageIds.TryUpdate(connectionId, newBag, bag);
            }
        }

        public void Dispose()
        {
            _cleanupTimer?.Dispose();
            foreach (var pending in _pendingAcknowledgments.Values)
            {
                pending.TimeoutTokenSource?.Dispose();
            }
        }
    }
}