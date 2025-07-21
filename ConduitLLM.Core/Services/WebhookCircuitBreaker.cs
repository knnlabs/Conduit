using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Simple circuit breaker for webhook endpoints to prevent repeated failures
    /// </summary>
    public interface IWebhookCircuitBreaker
    {
        /// <summary>
        /// Checks if the circuit is open (failing) for a webhook URL
        /// </summary>
        bool IsOpen(string webhookUrl);
        
        /// <summary>
        /// Records a successful webhook delivery
        /// </summary>
        void RecordSuccess(string webhookUrl);
        
        /// <summary>
        /// Records a failed webhook delivery
        /// </summary>
        void RecordFailure(string webhookUrl);
        
        /// <summary>
        /// Gets circuit breaker statistics for monitoring
        /// </summary>
        CircuitBreakerStats GetStats(string webhookUrl);
    }
    
    /// <summary>
    /// Circuit breaker statistics
    /// </summary>
    public class CircuitBreakerStats
    {
        public int FailureCount { get; init; }
        public int SuccessCount { get; init; }
        public DateTime? LastFailureTime { get; init; }
        public DateTime? CircuitOpenedAt { get; init; }
        public bool IsOpen { get; init; }
    }
    
    /// <summary>
    /// In-memory implementation of webhook circuit breaker
    /// </summary>
    public class WebhookCircuitBreaker : IWebhookCircuitBreaker
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<WebhookCircuitBreaker> _logger;
        private readonly int _failureThreshold;
        private readonly TimeSpan _openDuration;
        private readonly TimeSpan _counterResetDuration;
        
        private const string FAILURE_COUNT_KEY = "webhook:cb:failures:";
        private const string SUCCESS_COUNT_KEY = "webhook:cb:success:";
        private const string CIRCUIT_OPEN_KEY = "webhook:cb:open:";
        private const string LAST_FAILURE_KEY = "webhook:cb:lastfail:";
        
        public WebhookCircuitBreaker(
            IMemoryCache cache,
            ILogger<WebhookCircuitBreaker> logger,
            int failureThreshold = 5,
            TimeSpan? openDuration = null,
            TimeSpan? counterResetDuration = null)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _failureThreshold = failureThreshold;
            _openDuration = openDuration ?? TimeSpan.FromMinutes(5);
            _counterResetDuration = counterResetDuration ?? TimeSpan.FromMinutes(15);
        }
        
        public bool IsOpen(string webhookUrl)
        {
            return _cache.TryGetValue($"{CIRCUIT_OPEN_KEY}{webhookUrl}", out _);
        }
        
        public void RecordSuccess(string webhookUrl)
        {
            // Reset failure count on success
            _cache.Remove($"{FAILURE_COUNT_KEY}{webhookUrl}");
            
            // Increment success count
            var successKey = $"{SUCCESS_COUNT_KEY}{webhookUrl}";
            var successCount = _cache.Get<int?>(successKey) ?? 0;
            _cache.Set(successKey, successCount + 1, _counterResetDuration);
            
            // Close circuit if it was open
            if (_cache.TryGetValue($"{CIRCUIT_OPEN_KEY}{webhookUrl}", out _))
            {
                _cache.Remove($"{CIRCUIT_OPEN_KEY}{webhookUrl}");
                _logger.LogInformation("Circuit breaker closed for webhook URL: {WebhookUrl}", webhookUrl);
            }
        }
        
        public void RecordFailure(string webhookUrl)
        {
            var failureKey = $"{FAILURE_COUNT_KEY}{webhookUrl}";
            var currentFailures = _cache.Get<int?>(failureKey) ?? 0;
            currentFailures++;
            
            // Update failure count and last failure time
            _cache.Set(failureKey, currentFailures, _counterResetDuration);
            _cache.Set($"{LAST_FAILURE_KEY}{webhookUrl}", DateTime.UtcNow, _counterResetDuration);
            
            // Open circuit if threshold reached
            if (currentFailures >= _failureThreshold && !IsOpen(webhookUrl))
            {
                _cache.Set($"{CIRCUIT_OPEN_KEY}{webhookUrl}", DateTime.UtcNow, _openDuration);
                _logger.LogWarning(
                    "Circuit breaker opened for webhook URL: {WebhookUrl} after {FailureCount} failures. " +
                    "Will retry in {OpenDuration} minutes.",
                    webhookUrl, currentFailures, _openDuration.TotalMinutes);
            }
        }
        
        public CircuitBreakerStats GetStats(string webhookUrl)
        {
            var failureCount = _cache.Get<int?>($"{FAILURE_COUNT_KEY}{webhookUrl}") ?? 0;
            var successCount = _cache.Get<int?>($"{SUCCESS_COUNT_KEY}{webhookUrl}") ?? 0;
            var lastFailure = _cache.Get<DateTime?>($"{LAST_FAILURE_KEY}{webhookUrl}");
            var circuitOpenedAt = _cache.Get<DateTime?>($"{CIRCUIT_OPEN_KEY}{webhookUrl}");
            
            return new CircuitBreakerStats
            {
                FailureCount = failureCount,
                SuccessCount = successCount,
                LastFailureTime = lastFailure,
                CircuitOpenedAt = circuitOpenedAt,
                IsOpen = circuitOpenedAt.HasValue
            };
        }
    }
}