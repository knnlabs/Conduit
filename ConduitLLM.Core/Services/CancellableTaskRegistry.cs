using System;
using System.Collections.Concurrent;
using System.Threading;
using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Thread-safe registry for managing cancellable async tasks.
    /// </summary>
    public class CancellableTaskRegistry : ICancellableTaskRegistry, IDisposable
    {
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _registry = new();
        private readonly ILogger<CancellableTaskRegistry> _logger;
        private bool _disposed;

        public CancellableTaskRegistry(ILogger<CancellableTaskRegistry> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public void RegisterTask(string taskId, CancellationTokenSource cts)
        {
            if (string.IsNullOrWhiteSpace(taskId))
                throw new ArgumentException("Task ID cannot be null or whitespace.", nameof(taskId));
            
            if (cts == null)
                throw new ArgumentNullException(nameof(cts));

            if (_registry.TryAdd(taskId, cts))
            {
                _logger.LogDebug("Registered cancellable task {TaskId}", taskId);
                
                // Automatically unregister when the token is cancelled
                cts.Token.Register(() => UnregisterTask(taskId));
            }
            else
            {
                _logger.LogWarning("Task {TaskId} is already registered", taskId);
            }
        }

        /// <inheritdoc/>
        public bool TryCancel(string taskId)
        {
            if (string.IsNullOrWhiteSpace(taskId))
                return false;

            if (_registry.TryGetValue(taskId, out var cts))
            {
                try
                {
                    if (!cts.IsCancellationRequested)
                    {
                        cts.Cancel();
                        _logger.LogInformation("Cancelled task {TaskId}", taskId);
                        return true;
                    }
                    else
                    {
                        _logger.LogDebug("Task {TaskId} was already cancelled", taskId);
                        return false;
                    }
                }
                catch (ObjectDisposedException)
                {
                    _logger.LogWarning("Cancellation token source for task {TaskId} was already disposed", taskId);
                    UnregisterTask(taskId);
                    return false;
                }
            }

            _logger.LogDebug("Task {TaskId} not found in registry", taskId);
            return false;
        }

        /// <inheritdoc/>
        public void UnregisterTask(string taskId)
        {
            if (string.IsNullOrWhiteSpace(taskId))
                return;

            if (_registry.TryRemove(taskId, out var cts))
            {
                _logger.LogDebug("Unregistered task {TaskId}", taskId);
                
                // Dispose the CancellationTokenSource if not already disposed
                try
                {
                    cts.Dispose();
                }
                catch (ObjectDisposedException)
                {
                    // Already disposed, ignore
                }
            }
        }

        /// <inheritdoc/>
        public bool TryGetCancellationToken(string taskId, out CancellationToken? cancellationToken)
        {
            cancellationToken = null;
            
            if (string.IsNullOrWhiteSpace(taskId))
                return false;

            if (_registry.TryGetValue(taskId, out var cts))
            {
                try
                {
                    cancellationToken = cts.Token;
                    return true;
                }
                catch (ObjectDisposedException)
                {
                    _logger.LogWarning("Cancellation token source for task {TaskId} was disposed", taskId);
                    UnregisterTask(taskId);
                    return false;
                }
            }

            return false;
        }

        /// <inheritdoc/>
        public void CancelAll()
        {
            _logger.LogInformation("Cancelling all {Count} registered tasks", _registry.Count);
            
            foreach (var kvp in _registry)
            {
                try
                {
                    if (!kvp.Value.IsCancellationRequested)
                    {
                        kvp.Value.Cancel();
                    }
                }
                catch (ObjectDisposedException)
                {
                    // Ignore disposed tokens
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error cancelling task {TaskId}", kvp.Key);
                }
            }
            
            // Clear the registry after cancelling all
            _registry.Clear();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _logger.LogDebug("Disposing CancellableTaskRegistry");
            
            // Cancel and dispose all registered tasks
            foreach (var kvp in _registry)
            {
                try
                {
                    kvp.Value.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error disposing cancellation token source for task {TaskId}", kvp.Key);
                }
            }
            
            _registry.Clear();
            _disposed = true;
        }
    }
}