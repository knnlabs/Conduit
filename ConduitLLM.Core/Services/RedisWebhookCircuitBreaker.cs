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
    public class RedisWebhookCircuitBreaker : IWebhookCircuitBreaker
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<RedisWebhookCircuitBreaker> _logger;
        private readonly int _failureThreshold;
        private readonly TimeSpan _openDuration;
        private readonly TimeSpan _halfOpenTestInterval;
        
        private const string CIRCUIT_STATE_KEY = "webhook:circuit:{0}:state";
        private const string FAILURE_COUNT_KEY = "webhook:circuit:{0}:failures";
        private const string SUCCESS_COUNT_KEY = "webhook:circuit:{0}:success";
        private const string LAST_FAILURE_KEY = "webhook:circuit:{0}:lastfail";
        private const string CIRCUIT_OPENED_KEY = "webhook:circuit:{0}:opened";
        
        public RedisWebhookCircuitBreaker(
            IConnectionMultiplexer redis,
            ILogger<RedisWebhookCircuitBreaker> logger,
            int failureThreshold = 5,
            TimeSpan? openDuration = null,
            TimeSpan? halfOpenTestInterval = null)
        {
            _redis = redis ?? throw new ArgumentNullException(nameof(redis));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _failureThreshold = failureThreshold;
            _openDuration = openDuration ?? TimeSpan.FromMinutes(5);
            _halfOpenTestInterval = halfOpenTestInterval ?? TimeSpan.FromSeconds(30);
        }
        
        public bool IsOpen(string webhookUrl)
        {
            try
            {
                var db = _redis.GetDatabase();
                var stateKey = string.Format(CIRCUIT_STATE_KEY, GetUrlHash(webhookUrl));
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
                                _logger.LogInformation(
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
                _logger.LogError(ex, "Error checking circuit state for webhook: {WebhookUrl}", webhookUrl);
                // In case of Redis failure, assume circuit is closed to avoid blocking webhooks
                return false;
            }
        }
        
        public void RecordSuccess(string webhookUrl)
        {
            try
            {
                var db = _redis.GetDatabase();
                var urlHash = GetUrlHash(webhookUrl);
                
                var transaction = db.CreateTransaction();
                
                // Reset failure count
                transaction.KeyDeleteAsync(string.Format(FAILURE_COUNT_KEY, urlHash));
                
                // Increment success count
                var successKey = string.Format(SUCCESS_COUNT_KEY, urlHash);
                transaction.StringIncrementAsync(successKey);
                transaction.KeyExpireAsync(successKey, TimeSpan.FromHours(1));
                
                // Close circuit if it was open or half-open
                var stateKey = string.Format(CIRCUIT_STATE_KEY, urlHash);
                var currentState = db.StringGet(stateKey);
                
                if (currentState.HasValue)
                {
                    var circuitState = JsonSerializer.Deserialize<CircuitState>(currentState.ToString());
                    if (circuitState != null && (circuitState.State == "Open" || circuitState.State == "HalfOpen"))
                    {
                        // Close the circuit
                        transaction.KeyDeleteAsync(stateKey);
                        transaction.KeyDeleteAsync(string.Format(CIRCUIT_OPENED_KEY, urlHash));
                        
                        _logger.LogInformation(
                            "Circuit breaker closed for webhook: {WebhookUrl} after successful delivery",
                            webhookUrl);
                    }
                }
                
                transaction.Execute();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording success for webhook: {WebhookUrl}", webhookUrl);
            }
        }
        
        public void RecordFailure(string webhookUrl)
        {
            try
            {
                var db = _redis.GetDatabase();
                var urlHash = GetUrlHash(webhookUrl);
                
                // Check current state
                var stateKey = string.Format(CIRCUIT_STATE_KEY, urlHash);
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
                var failureKey = string.Format(FAILURE_COUNT_KEY, urlHash);
                var failureCount = db.StringIncrement(failureKey);
                
                // Set expiry on failure counter
                db.KeyExpire(failureKey, TimeSpan.FromMinutes(15));
                
                // Update last failure time
                var lastFailureKey = string.Format(LAST_FAILURE_KEY, urlHash);
                db.StringSet(lastFailureKey, DateTime.UtcNow.ToString("O"), TimeSpan.FromHours(1));
                
                // Check if we should open the circuit
                if (failureCount >= _failureThreshold)
                {
                    OpenCircuit(db, webhookUrl, urlHash, (int)failureCount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording failure for webhook: {WebhookUrl}", webhookUrl);
            }
        }
        
        public CircuitBreakerStats GetStats(string webhookUrl)
        {
            try
            {
                var db = _redis.GetDatabase();
                var urlHash = GetUrlHash(webhookUrl);
                
                var batch = db.CreateBatch();
                var failureTask = batch.StringGetAsync(string.Format(FAILURE_COUNT_KEY, urlHash));
                var successTask = batch.StringGetAsync(string.Format(SUCCESS_COUNT_KEY, urlHash));
                var lastFailureTask = batch.StringGetAsync(string.Format(LAST_FAILURE_KEY, urlHash));
                var stateTask = batch.StringGetAsync(string.Format(CIRCUIT_STATE_KEY, urlHash));
                
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
                _logger.LogError(ex, "Error getting circuit stats for webhook: {WebhookUrl}", webhookUrl);
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
                
                var stateKey = string.Format(CIRCUIT_STATE_KEY, urlHash);
                var stateJson = JsonSerializer.Serialize(circuitState);
                
                db.StringSet(stateKey, stateJson, _openDuration.Add(TimeSpan.FromMinutes(5)));
                
                // Also set a simple flag for quick checks
                var openedKey = string.Format(CIRCUIT_OPENED_KEY, urlHash);
                db.StringSet(openedKey, DateTime.UtcNow.ToString("O"), _openDuration);
                
                _logger.LogWarning(
                    "Circuit breaker opened for webhook: {WebhookUrl} after {FailureCount} failures. " +
                    "Will attempt recovery in {OpenDuration} minutes.",
                    webhookUrl, failureCount, _openDuration.TotalMinutes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error opening circuit for webhook: {WebhookUrl}", webhookUrl);
            }
        }
        
        private string GetUrlHash(string webhookUrl)
        {
            // Create a consistent hash for the URL to use as Redis key component
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(webhookUrl));
            return Convert.ToBase64String(hashBytes).Replace("/", "-").Replace("+", "_").Substring(0, 16);
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