using ConduitLLM.Core.Constants;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text.Json;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Redis-based distributed circuit breaker for webhook endpoints
    /// Ensures circuit state is shared across all instances
    /// 
    /// For detailed architecture and configuration information, see:
    /// - Architecture: docs/architecture/webhook-delivery-system.md
    /// - Operations: docs/operations/webhook-monitoring.md
    /// </summary>
    public class RedisWebhookCircuitBreaker : RedisWebhookServiceBase, IWebhookCircuitBreaker
    {
        private readonly int _failureThreshold;
        private readonly TimeSpan _openDuration;
        private readonly TimeSpan _halfOpenTestInterval;


        public RedisWebhookCircuitBreaker(
            IConnectionMultiplexer redis,
            ILogger<RedisWebhookCircuitBreaker> logger,
            int failureThreshold = 5,
            TimeSpan? openDuration = null,
            TimeSpan? halfOpenTestInterval = null)
            : base(redis, logger)
        {
            _failureThreshold = failureThreshold;
            _openDuration = openDuration ?? TimeSpan.FromMinutes(5);
            _halfOpenTestInterval = halfOpenTestInterval ?? TimeSpan.FromSeconds(30);
        }
        
        public bool IsOpen(string webhookUrl)
        {
            try
            {
                var db = Redis.GetDatabase();
                var stateKey = RedisKeys.WebhookCircuit.State(GetUrlHash(webhookUrl));
                var state = db.StringGet(stateKey);
                
                if (state.HasValue)
                {
                    var circuitState = JsonSerializer.Deserialize<CircuitState>(state.ToString());
                    if (circuitState != null)
                    {
                        // Check if circuit should transition from Open to Half-Open
                        if (circuitState.State == "Open" && 
                            DateTime.UtcNow > circuitState.OpenedAt.Add(_openDuration))
                        {
                            // Try to transition to half-open (only one instance should succeed)
                            var transaction = db.CreateTransaction();
                            transaction.AddCondition(Condition.StringEqual(stateKey, state));
                            
                            circuitState.State = "HalfOpen";
                            circuitState.HalfOpenTestAt = DateTime.UtcNow;
                            var newState = JsonSerializer.Serialize(circuitState);
                            transaction.StringSetAsync(stateKey, newState, _openDuration);
                            
                            if (transaction.Execute())
                            {
                                Logger.LogInformation(
                                    "Circuit breaker transitioned to half-open for webhook: {WebhookUrl}",
                                    webhookUrl);
                                return false; // Allow one test request
                            }
                        }
                        
                        return circuitState.State == "Open";
                    }
                }
                
                return false;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error checking circuit state for webhook: {WebhookUrl}", webhookUrl);
                // In case of Redis failure, assume circuit is closed to avoid blocking webhooks
                return false;
            }
        }
        
        public void RecordSuccess(string webhookUrl)
        {
            try
            {
                var db = Redis.GetDatabase();
                var urlHash = GetUrlHash(webhookUrl);
                
                var transaction = db.CreateTransaction();
                
                // Reset failure count
                transaction.KeyDeleteAsync(RedisKeys.WebhookCircuit.Failures(urlHash));
                
                // Increment success count
                var successKey = RedisKeys.WebhookCircuit.Successes(urlHash);
                transaction.StringIncrementAsync(successKey);
                transaction.KeyExpireAsync(successKey, TimeSpan.FromHours(1));
                
                // Close circuit if it was open or half-open
                var stateKey = RedisKeys.WebhookCircuit.State(urlHash);
                var currentState = db.StringGet(stateKey);
                
                if (currentState.HasValue)
                {
                    var circuitState = JsonSerializer.Deserialize<CircuitState>(currentState.ToString());
                    if (circuitState != null && (circuitState.State == "Open" || circuitState.State == "HalfOpen"))
                    {
                        // Close the circuit
                        transaction.KeyDeleteAsync(stateKey);
                        transaction.KeyDeleteAsync(RedisKeys.WebhookCircuit.Opened(urlHash));
                        
                        Logger.LogInformation(
                            "Circuit breaker closed for webhook: {WebhookUrl} after successful delivery",
                            webhookUrl);
                    }
                }
                
                transaction.Execute();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error recording success for webhook: {WebhookUrl}", webhookUrl);
            }
        }
        
        public void RecordFailure(string webhookUrl)
        {
            try
            {
                var db = Redis.GetDatabase();
                var urlHash = GetUrlHash(webhookUrl);
                
                // Check current state
                var stateKey = RedisKeys.WebhookCircuit.State(urlHash);
                var currentState = db.StringGet(stateKey);
                
                if (currentState.HasValue)
                {
                    var circuitState = JsonSerializer.Deserialize<CircuitState>(currentState.ToString());
                    if (circuitState != null && circuitState.State == "HalfOpen")
                    {
                        // Failed in half-open state, immediately open circuit again
                        OpenCircuit(db, webhookUrl, urlHash, _failureThreshold);
                        return;
                    }
                }
                
                // Increment failure count atomically
                var failureKey = RedisKeys.WebhookCircuit.Failures(urlHash);
                var failureCount = db.StringIncrement(failureKey);
                
                // Set expiry on failure counter
                db.KeyExpire(failureKey, TimeSpan.FromMinutes(15));
                
                // Update last failure time
                var lastFailureKey = RedisKeys.WebhookCircuit.LastFailure(urlHash);
                db.StringSet(lastFailureKey, DateTime.UtcNow.ToString("O"), TimeSpan.FromHours(1));
                
                // Check if we should open the circuit
                if (failureCount >= _failureThreshold)
                {
                    OpenCircuit(db, webhookUrl, urlHash, (int)failureCount);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error recording failure for webhook: {WebhookUrl}", webhookUrl);
            }
        }
        
        public CircuitBreakerStats GetStats(string webhookUrl)
        {
            try
            {
                var db = Redis.GetDatabase();
                var urlHash = GetUrlHash(webhookUrl);
                
                var batch = db.CreateBatch();
                var failureTask = batch.StringGetAsync(RedisKeys.WebhookCircuit.Failures(urlHash));
                var successTask = batch.StringGetAsync(RedisKeys.WebhookCircuit.Successes(urlHash));
                var lastFailureTask = batch.StringGetAsync(RedisKeys.WebhookCircuit.LastFailure(urlHash));
                var stateTask = batch.StringGetAsync(RedisKeys.WebhookCircuit.State(urlHash));
                
                batch.Execute();
                
                var failureCount = failureTask.Result.HasValue ? (int)failureTask.Result : 0;
                var successCount = successTask.Result.HasValue ? (int)successTask.Result : 0;
                DateTime? lastFailureTime = null;
                DateTime? circuitOpenedAt = null;
                bool isOpen = false;
                
                if (lastFailureTask.Result.HasValue && 
                    DateTime.TryParse(lastFailureTask.Result!, out var lastFailure))
                {
                    lastFailureTime = lastFailure;
                }
                
                if (stateTask.Result.HasValue)
                {
                    var circuitState = JsonSerializer.Deserialize<CircuitState>(stateTask.Result.ToString());
                    if (circuitState != null)
                    {
                        isOpen = circuitState.State == "Open";
                        circuitOpenedAt = circuitState.OpenedAt;
                    }
                }
                
                return new CircuitBreakerStats
                {
                    FailureCount = failureCount,
                    SuccessCount = successCount,
                    LastFailureTime = lastFailureTime,
                    CircuitOpenedAt = circuitOpenedAt,
                    IsOpen = isOpen
                };
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error getting circuit stats for webhook: {WebhookUrl}", webhookUrl);
                return new CircuitBreakerStats();
            }
        }
        
        private void OpenCircuit(IDatabase db, string webhookUrl, string urlHash, int failureCount)
        {
            try
            {
                var circuitState = new CircuitState
                {
                    State = "Open",
                    OpenedAt = DateTime.UtcNow,
                    FailureCount = failureCount,
                    WebhookUrl = webhookUrl
                };
                
                var stateKey = RedisKeys.WebhookCircuit.State(urlHash);
                var stateJson = JsonSerializer.Serialize(circuitState);
                
                db.StringSet(stateKey, stateJson, _openDuration.Add(TimeSpan.FromMinutes(5)));
                
                // Also set a simple flag for quick checks
                var openedKey = RedisKeys.WebhookCircuit.Opened(urlHash);
                db.StringSet(openedKey, DateTime.UtcNow.ToString("O"), _openDuration);
                
                Logger.LogWarning(
                    "Circuit breaker opened for webhook: {WebhookUrl} after {FailureCount} failures. " +
                    "Will attempt recovery in {OpenDuration} minutes.",
                    webhookUrl, failureCount, _openDuration.TotalMinutes);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error opening circuit for webhook: {WebhookUrl}", webhookUrl);
            }
        }
        
        private class CircuitState
        {
            public string State { get; set; } = "Closed"; // Closed, Open, HalfOpen
            public DateTime OpenedAt { get; set; }
            public DateTime? HalfOpenTestAt { get; set; }
            public int FailureCount { get; set; }
            public string WebhookUrl { get; set; } = string.Empty;
        }
    }
}