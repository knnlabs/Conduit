using System;
using System.Threading.Tasks;

namespace ConduitLLM.Configuration.Interfaces
{
    /// <summary>
    /// Circuit breaker for Redis operations to prevent cascading failures
    /// </summary>
    public interface IRedisCircuitBreaker
    {
        /// <summary>
        /// Gets whether the circuit is currently open (failing)
        /// </summary>
        bool IsOpen { get; }

        /// <summary>
        /// Gets whether the circuit is in half-open state (testing recovery)
        /// </summary>
        bool IsHalfOpen { get; }

        /// <summary>
        /// Gets the current state of the circuit breaker
        /// </summary>
        CircuitState State { get; }

        /// <summary>
        /// Gets statistics about the circuit breaker
        /// </summary>
        CircuitBreakerStatistics Statistics { get; }

        /// <summary>
        /// Executes an operation through the circuit breaker
        /// </summary>
        /// <typeparam name="T">Return type of the operation</typeparam>
        /// <param name="operation">The Redis operation to execute</param>
        /// <returns>The result of the operation</returns>
        /// <exception cref="RedisCircuitBreakerOpenException">Thrown when circuit is open</exception>
        Task<T> ExecuteAsync<T>(Func<Task<T>> operation);

        /// <summary>
        /// Executes an operation through the circuit breaker without a return value
        /// </summary>
        /// <param name="operation">The Redis operation to execute</param>
        /// <exception cref="RedisCircuitBreakerOpenException">Thrown when circuit is open</exception>
        Task ExecuteAsync(Func<Task> operation);

        /// <summary>
        /// Manually trips the circuit breaker
        /// </summary>
        /// <param name="reason">Reason for manually tripping the circuit</param>
        void Trip(string reason);

        /// <summary>
        /// Manually resets the circuit breaker to closed state
        /// </summary>
        void Reset();

        /// <summary>
        /// Tests if Redis is available without affecting circuit state
        /// </summary>
        /// <returns>True if Redis is available, false otherwise</returns>
        Task<bool> TestConnectionAsync();
    }

    /// <summary>
    /// Represents the state of the circuit breaker
    /// </summary>
    public enum CircuitState
    {
        /// <summary>
        /// Circuit is closed, operations are allowed
        /// </summary>
        Closed,

        /// <summary>
        /// Circuit is open, operations are blocked
        /// </summary>
        Open,

        /// <summary>
        /// Circuit is half-open, testing if service has recovered
        /// </summary>
        HalfOpen
    }

    /// <summary>
    /// Statistics about the circuit breaker
    /// </summary>
    public class CircuitBreakerStatistics
    {
        /// <summary>
        /// Current state of the circuit
        /// </summary>
        public CircuitState State { get; init; }

        /// <summary>
        /// Number of consecutive failures
        /// </summary>
        public int ConsecutiveFailures { get; init; }

        /// <summary>
        /// Total number of failures since last reset
        /// </summary>
        public long TotalFailures { get; init; }

        /// <summary>
        /// Total number of successes since last reset
        /// </summary>
        public long TotalSuccesses { get; init; }

        /// <summary>
        /// Timestamp when circuit was opened
        /// </summary>
        public DateTime? CircuitOpenedAt { get; init; }

        /// <summary>
        /// Timestamp of last failure
        /// </summary>
        public DateTime? LastFailureAt { get; init; }

        /// <summary>
        /// Timestamp of last successful operation
        /// </summary>
        public DateTime? LastSuccessAt { get; init; }

        /// <summary>
        /// Reason for last circuit trip
        /// </summary>
        public string? LastTripReason { get; init; }

        /// <summary>
        /// Number of operations rejected while circuit was open
        /// </summary>
        public long RejectedRequests { get; init; }

        /// <summary>
        /// Estimated time until circuit will attempt recovery
        /// </summary>
        public TimeSpan? TimeUntilHalfOpen { get; init; }
    }

    /// <summary>
    /// Exception thrown when attempting to execute an operation through an open circuit
    /// </summary>
    public class RedisCircuitBreakerOpenException : Exception
    {
        /// <summary>
        /// Gets the current state of the circuit breaker
        /// </summary>
        public CircuitState State { get; }

        /// <summary>
        /// Gets the estimated time until the circuit will attempt recovery
        /// </summary>
        public TimeSpan? RetryAfter { get; }

        public RedisCircuitBreakerOpenException(CircuitState state, TimeSpan? retryAfter = null)
            : base("Redis circuit breaker is open. Service is temporarily unavailable.")
        {
            State = state;
            RetryAfter = retryAfter;
        }

        public RedisCircuitBreakerOpenException(string message, CircuitState state, TimeSpan? retryAfter = null)
            : base(message)
        {
            State = state;
            RetryAfter = retryAfter;
        }
    }
}