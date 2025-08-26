using System;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using StackExchange.Redis;
using PollyCircuitState = Polly.CircuitBreaker.CircuitState;

namespace ConduitLLM.Configuration.Services
{
    /// <summary>
    /// Implementation of Redis circuit breaker using Polly
    /// </summary>
    public class RedisCircuitBreaker : IRedisCircuitBreaker
    {
        private readonly ILogger<RedisCircuitBreaker> _logger;
        private readonly RedisCircuitBreakerOptions _options;
        private readonly RedisConnectionFactory _redisConnectionFactory;
        private readonly IAsyncPolicy _circuitBreaker;
        private readonly SemaphoreSlim _testConnectionSemaphore;
        private DateTime _lastHealthCheck = DateTime.MinValue;
        private PollyCircuitState _currentPollyState = PollyCircuitState.Closed;
        
        // Statistics tracking
        private long _totalFailures = 0;
        private long _totalSuccesses = 0;
        private long _rejectedRequests = 0;
        private DateTime? _lastFailureAt = null;
        private DateTime? _lastSuccessAt = null;
        private string? _lastTripReason = null;
        private DateTime? _circuitOpenedAt = null;
        private int _halfOpenSuccesses = 0;
        private int _halfOpenAttempts = 0;

        public RedisCircuitBreaker(
            ILogger<RedisCircuitBreaker> logger,
            IOptions<RedisCircuitBreakerOptions> options,
            RedisConnectionFactory redisConnectionFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _redisConnectionFactory = redisConnectionFactory ?? throw new ArgumentNullException(nameof(redisConnectionFactory));
            _testConnectionSemaphore = new SemaphoreSlim(1, 1);

            // Create the circuit breaker policy with proper typing
            _circuitBreaker = Policy
                .Handle<Exception>(ex => ShouldHandleException(ex))
                .AdvancedCircuitBreakerAsync(
                    failureThreshold: 0.5, // 50% failure rate
                    samplingDuration: TimeSpan.FromSeconds(60), // Over a 60 second window
                    minimumThroughput: _options.FailureThreshold, // Minimum number of actions
                    durationOfBreak: _options.GetOpenDuration(),
                    onBreak: OnCircuitBreak,
                    onReset: OnCircuitReset,
                    onHalfOpen: OnCircuitHalfOpen);

            if (_options.ResetOnStartup)
            {
                _logger.LogInformation("Redis circuit breaker initialized and reset on startup");
            }
        }

        /// <inheritdoc />
        public bool IsOpen => _currentPollyState == PollyCircuitState.Open || _currentPollyState == PollyCircuitState.Isolated;

        /// <inheritdoc />
        public bool IsHalfOpen => _currentPollyState == PollyCircuitState.HalfOpen;

        /// <inheritdoc />
        public ConduitLLM.Configuration.Interfaces.CircuitState State => _currentPollyState switch
        {
            PollyCircuitState.Closed => ConduitLLM.Configuration.Interfaces.CircuitState.Closed,
            PollyCircuitState.Open => ConduitLLM.Configuration.Interfaces.CircuitState.Open,
            PollyCircuitState.HalfOpen => ConduitLLM.Configuration.Interfaces.CircuitState.HalfOpen,
            PollyCircuitState.Isolated => ConduitLLM.Configuration.Interfaces.CircuitState.Open, // Treat isolated as open
            _ => ConduitLLM.Configuration.Interfaces.CircuitState.Closed
        };

        /// <inheritdoc />
        public CircuitBreakerStatistics Statistics => new()
        {
            State = State,
            ConsecutiveFailures = 0, // Polly doesn't expose this directly
            TotalFailures = Interlocked.Read(ref _totalFailures),
            TotalSuccesses = Interlocked.Read(ref _totalSuccesses),
            CircuitOpenedAt = _circuitOpenedAt,
            LastFailureAt = _lastFailureAt,
            LastSuccessAt = _lastSuccessAt,
            LastTripReason = _lastTripReason,
            RejectedRequests = Interlocked.Read(ref _rejectedRequests),
            TimeUntilHalfOpen = CalculateTimeUntilHalfOpen()
        };

        /// <inheritdoc />
        public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            // Check for manual trip first
            if (_options.EnableManualControl && _currentPollyState == PollyCircuitState.Open)
            {
                OnRequestRejected();
                var retryAfter = CalculateTimeUntilHalfOpen();
                throw new RedisCircuitBreakerOpenException(
                    _lastTripReason ?? "Redis circuit breaker is manually tripped.",
                    State,
                    retryAfter);
            }

            try
            {
                return await _circuitBreaker.ExecuteAsync(async () =>
                {
                    // Wrap the operation with a timeout
                    using var cts = new CancellationTokenSource(_options.GetOperationTimeout());
                    var task = operation();
                    
                    if (task == await Task.WhenAny(task, Task.Delay(_options.GetOperationTimeout(), cts.Token)))
                    {
                        var result = await task;
                        OnOperationSuccess();
                        return result;
                    }
                    else
                    {
                        throw new TimeoutException($"Redis operation timed out after {_options.OperationTimeoutMilliseconds}ms");
                    }
                });
            }
            catch (IsolatedCircuitException)
            {
                OnRequestRejected();
                throw new RedisCircuitBreakerOpenException(
                    "Redis circuit breaker is isolated (manually tripped).",
                    State);
            }
            catch (BrokenCircuitException)
            {
                OnRequestRejected();
                var retryAfter = CalculateTimeUntilHalfOpen();
                throw new RedisCircuitBreakerOpenException(
                    "Redis circuit breaker is open. Too many failures detected.",
                    State,
                    retryAfter);
            }
            catch (Exception ex)
            {
                OnOperationFailure(ex);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task ExecuteAsync(Func<Task> operation)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            // Check for manual trip first (same as in ExecuteAsync<T>)
            if (_options.EnableManualControl && _currentPollyState == PollyCircuitState.Open)
            {
                OnRequestRejected();
                var retryAfter = CalculateTimeUntilHalfOpen();
                throw new RedisCircuitBreakerOpenException(
                    _lastTripReason ?? "Redis circuit breaker is manually tripped.",
                    State,
                    retryAfter);
            }

            await ExecuteAsync(async () =>
            {
                await operation();
                return true;
            });
        }

        /// <inheritdoc />
        public void Trip(string reason)
        {
            if (!_options.EnableManualControl)
            {
                _logger.LogWarning("Manual circuit breaker control is disabled. Trip request ignored.");
                return;
            }

            _lastTripReason = $"Manual: {reason}";
            // Polly v8 doesn't have Isolate for IAsyncPolicy, simulate by setting state
            _currentPollyState = PollyCircuitState.Open;
            _circuitOpenedAt = DateTime.UtcNow;
            
            _logger.LogWarning("Redis circuit breaker manually tripped. Reason: {Reason}", reason);
        }

        /// <inheritdoc />
        public void Reset()
        {
            if (!_options.EnableManualControl)
            {
                _logger.LogWarning("Manual circuit breaker control is disabled. Reset request ignored.");
                return;
            }

            // Polly v8 doesn't have Reset for IAsyncPolicy, simulate by setting state
            _currentPollyState = PollyCircuitState.Closed;
            _halfOpenSuccesses = 0;
            _halfOpenAttempts = 0;
            _circuitOpenedAt = null;
            
            _logger.LogInformation("Redis circuit breaker manually reset to closed state");
        }

        /// <inheritdoc />
        public async Task<bool> TestConnectionAsync()
        {
            // Prevent concurrent health checks
            if (!await _testConnectionSemaphore.WaitAsync(0))
            {
                return !IsOpen; // Return current state if already checking
            }

            try
            {
                // Rate limit health checks
                if (DateTime.UtcNow - _lastHealthCheck < _options.GetHealthCheckInterval())
                {
                    return !IsOpen;
                }

                _lastHealthCheck = DateTime.UtcNow;

                // Test Redis connection without affecting circuit state
                try
                {
                    var redis = await _redisConnectionFactory.GetConnectionAsync();
                    var db = redis.GetDatabase();
                    
                    // Simple ping test
                    var testKey = $"circuit:test:{Guid.NewGuid():N}";
                    await db.StringSetAsync(testKey, "test", TimeSpan.FromSeconds(1));
                    await db.KeyDeleteAsync(testKey);
                    
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogDebug("Redis health check failed: {Error}", ex.Message);
                    return false;
                }
            }
            finally
            {
                _testConnectionSemaphore.Release();
            }
        }

        private void OnCircuitBreak(Exception exception, TimeSpan duration)
        {
            _currentPollyState = PollyCircuitState.Open;
            _circuitOpenedAt = DateTime.UtcNow;
            
            var reason = exception?.Message ?? "Unknown error";
            _lastTripReason = reason;
            
            _logger.LogError(
                "Redis circuit breaker opened due to failures. Duration: {Duration}s. Reason: {Reason}",
                duration.TotalSeconds,
                reason);

            // Reset half-open counters
            _halfOpenSuccesses = 0;
            _halfOpenAttempts = 0;
        }

        private void OnCircuitReset()
        {
            _currentPollyState = PollyCircuitState.Closed;
            _logger.LogInformation("Redis circuit breaker reset to closed state. Service recovered.");
            _circuitOpenedAt = null;
            _halfOpenSuccesses = 0;
            _halfOpenAttempts = 0;
        }

        private void OnCircuitHalfOpen()
        {
            _currentPollyState = PollyCircuitState.HalfOpen;
            _logger.LogInformation("Redis circuit breaker entering half-open state. Testing recovery...");
            _halfOpenSuccesses = 0;
            _halfOpenAttempts = 0;
        }

        private void OnOperationSuccess()
        {
            Interlocked.Increment(ref _totalSuccesses);
            _lastSuccessAt = DateTime.UtcNow;

            if (IsHalfOpen)
            {
                _halfOpenSuccesses++;
                _halfOpenAttempts++;
                
                _logger.LogDebug(
                    "Half-open success {Successes}/{Required} (Attempts: {Attempts}/{Max})",
                    _halfOpenSuccesses,
                    _options.HalfOpenSuccessesRequired,
                    _halfOpenAttempts,
                    _options.HalfOpenMaxAttempts);

                // Check if we should close the circuit
                if (_halfOpenSuccesses >= _options.HalfOpenSuccessesRequired)
                {
                    _logger.LogInformation("Circuit breaker closing after successful recovery test");
                }
            }
        }

        private void OnOperationFailure(Exception ex)
        {
            Interlocked.Increment(ref _totalFailures);
            _lastFailureAt = DateTime.UtcNow;

            if (IsHalfOpen)
            {
                _halfOpenAttempts++;
                
                _logger.LogWarning(
                    "Half-open failure. Attempts: {Attempts}/{Max}",
                    _halfOpenAttempts,
                    _options.HalfOpenMaxAttempts);
            }
        }

        private void OnRequestRejected()
        {
            Interlocked.Increment(ref _rejectedRequests);
        }

        private TimeSpan? CalculateTimeUntilHalfOpen()
        {
            if (!IsOpen || _circuitOpenedAt == null)
                return null;

            var elapsed = DateTime.UtcNow - _circuitOpenedAt.Value;
            var remaining = _options.GetOpenDuration() - elapsed;
            
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }

        private bool IsRedisRelated(Exception ex)
        {
            // Check if the exception is Redis-related
            return ex.Message.Contains("Redis", StringComparison.OrdinalIgnoreCase) ||
                   ex.Message.Contains("Connection", StringComparison.OrdinalIgnoreCase) ||
                   ex.InnerException != null && IsRedisRelated(ex.InnerException);
        }

        private bool ShouldHandleException(Exception ex)
        {
            // Handle specific Redis exceptions
            if (ex is RedisException ||
                ex is RedisConnectionException ||
                ex is RedisTimeoutException ||
                ex is TimeoutException)
            {
                return true;
            }

            // Handle generic exceptions that appear Redis-related
            return IsRedisRelated(ex);
        }
    }
}