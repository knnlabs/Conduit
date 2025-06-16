using System.Collections.Concurrent;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;

namespace ConduitLLM.WebUI.Services
{
    /// <summary>
    /// Service for managing streaming metrics in the WebUI.
    /// </summary>
    public interface IStreamingMetricsService
    {
        /// <summary>
        /// Stores metrics for a streaming request.
        /// </summary>
        void StoreMetrics(string requestId, StreamingMetrics metrics);

        /// <summary>
        /// Stores final performance metrics for a request.
        /// </summary>
        void StoreFinalMetrics(string requestId, PerformanceMetrics metrics);

        /// <summary>
        /// Gets the latest metrics for a request.
        /// </summary>
        StreamingMetrics? GetLatestMetrics(string requestId);

        /// <summary>
        /// Gets the final performance metrics for a request.
        /// </summary>
        PerformanceMetrics? GetFinalMetrics(string requestId);

        /// <summary>
        /// Cleans up metrics for a request.
        /// </summary>
        void CleanupMetrics(string requestId);
    }

    /// <summary>
    /// In-memory implementation of streaming metrics service.
    /// </summary>
    public class InMemoryStreamingMetricsService : IStreamingMetricsService
    {
        private readonly ConcurrentDictionary<string, StreamingMetrics> _streamingMetrics = new();
        private readonly ConcurrentDictionary<string, PerformanceMetrics> _finalMetrics = new();
        private readonly ConcurrentDictionary<string, DateTime> _lastAccessTime = new();
        private readonly TimeSpan _retentionPeriod;
        private readonly Timer _cleanupTimer;

        public InMemoryStreamingMetricsService(TimeSpan? retentionPeriod = null)
        {
            _retentionPeriod = retentionPeriod ?? TimeSpan.FromMinutes(5);
            
            // Start cleanup timer
            _cleanupTimer = new Timer(
                CleanupExpiredMetrics, 
                null, 
                TimeSpan.FromMinutes(1), 
                TimeSpan.FromMinutes(1));
        }

        public void StoreMetrics(string requestId, StreamingMetrics metrics)
        {
            _streamingMetrics.AddOrUpdate(requestId, metrics, (_, _) => metrics);
            _lastAccessTime.AddOrUpdate(requestId, DateTime.UtcNow, (_, _) => DateTime.UtcNow);
        }

        public void StoreFinalMetrics(string requestId, PerformanceMetrics metrics)
        {
            _finalMetrics.AddOrUpdate(requestId, metrics, (_, _) => metrics);
            _lastAccessTime.AddOrUpdate(requestId, DateTime.UtcNow, (_, _) => DateTime.UtcNow);
        }

        public StreamingMetrics? GetLatestMetrics(string requestId)
        {
            _lastAccessTime.AddOrUpdate(requestId, DateTime.UtcNow, (_, _) => DateTime.UtcNow);
            return _streamingMetrics.TryGetValue(requestId, out var metrics) ? metrics : null;
        }

        public PerformanceMetrics? GetFinalMetrics(string requestId)
        {
            _lastAccessTime.AddOrUpdate(requestId, DateTime.UtcNow, (_, _) => DateTime.UtcNow);
            return _finalMetrics.TryGetValue(requestId, out var metrics) ? metrics : null;
        }

        public void CleanupMetrics(string requestId)
        {
            _streamingMetrics.TryRemove(requestId, out _);
            _finalMetrics.TryRemove(requestId, out _);
            _lastAccessTime.TryRemove(requestId, out _);
        }

        private void CleanupExpiredMetrics(object? state)
        {
            var cutoffTime = DateTime.UtcNow - _retentionPeriod;
            var expiredRequests = _lastAccessTime
                .Where(kvp => kvp.Value < cutoffTime)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var requestId in expiredRequests)
            {
                CleanupMetrics(requestId);
            }
        }

        public void Dispose()
        {
            _cleanupTimer?.Dispose();
        }
    }

    /// <summary>
    /// Event args for streaming metrics updates.
    /// </summary>
    public class StreamingMetricsEventArgs : EventArgs
    {
        public string RequestId { get; }
        public StreamingMetrics? StreamingMetrics { get; }
        public PerformanceMetrics? FinalMetrics { get; }
        public bool IsFinal => FinalMetrics != null;

        public StreamingMetricsEventArgs(string requestId, StreamingMetrics metrics)
        {
            RequestId = requestId;
            StreamingMetrics = metrics;
        }

        public StreamingMetricsEventArgs(string requestId, PerformanceMetrics finalMetrics)
        {
            RequestId = requestId;
            FinalMetrics = finalMetrics;
        }
    }

    /// <summary>
    /// Observable streaming metrics service that notifies subscribers of updates.
    /// </summary>
    public class ObservableStreamingMetricsService : InMemoryStreamingMetricsService
    {
        public event EventHandler<StreamingMetricsEventArgs>? MetricsUpdated;

        public ObservableStreamingMetricsService(TimeSpan? retentionPeriod = null) 
            : base(retentionPeriod)
        {
        }

        public new void StoreMetrics(string requestId, StreamingMetrics metrics)
        {
            base.StoreMetrics(requestId, metrics);
            MetricsUpdated?.Invoke(this, new StreamingMetricsEventArgs(requestId, metrics));
        }

        public new void StoreFinalMetrics(string requestId, PerformanceMetrics metrics)
        {
            base.StoreFinalMetrics(requestId, metrics);
            MetricsUpdated?.Invoke(this, new StreamingMetricsEventArgs(requestId, metrics));
        }
    }
}