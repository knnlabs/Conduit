using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Configuration.HealthChecks
{
    /// <summary>
    /// Health check that combines Redis connectivity with circuit breaker state
    /// </summary>
    public class RedisCircuitBreakerHealthCheck : IHealthCheck
    {
        private readonly IRedisCircuitBreaker _circuitBreaker;
        private readonly ILogger<RedisCircuitBreakerHealthCheck> _logger;

        public RedisCircuitBreakerHealthCheck(
            IRedisCircuitBreaker circuitBreaker,
            ILogger<RedisCircuitBreakerHealthCheck> logger)
        {
            _circuitBreaker = circuitBreaker ?? throw new ArgumentNullException(nameof(circuitBreaker));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var stats = _circuitBreaker.Statistics;
                var isHealthy = await _circuitBreaker.TestConnectionAsync();

                var healthData = new Dictionary<string, object>
                {
                    ["circuit_state"] = stats.State.ToString(),
                    ["circuit_open"] = _circuitBreaker.IsOpen,
                    ["total_failures"] = stats.TotalFailures,
                    ["total_successes"] = stats.TotalSuccesses,
                    ["rejected_requests"] = stats.RejectedRequests,
                    ["redis_available"] = isHealthy
                };

                if (stats.CircuitOpenedAt.HasValue)
                {
                    healthData["circuit_opened_at"] = stats.CircuitOpenedAt.Value.ToString("O");
                }

                if (stats.TimeUntilHalfOpen.HasValue)
                {
                    healthData["seconds_until_recovery"] = stats.TimeUntilHalfOpen.Value.TotalSeconds;
                }

                if (!string.IsNullOrEmpty(stats.LastTripReason))
                {
                    healthData["last_trip_reason"] = stats.LastTripReason;
                }

                // Determine overall health status
                if (_circuitBreaker.IsOpen)
                {
                    return HealthCheckResult.Unhealthy(
                        $"Redis circuit breaker is open. State: {stats.State}",
                        data: healthData);
                }

                if (_circuitBreaker.IsHalfOpen)
                {
                    return HealthCheckResult.Degraded(
                        "Redis circuit breaker is testing recovery (half-open)",
                        data: healthData);
                }

                if (!isHealthy)
                {
                    return HealthCheckResult.Degraded(
                        "Redis connection test failed but circuit is still closed",
                        data: healthData);
                }

                return HealthCheckResult.Healthy(
                    "Redis is healthy and circuit breaker is closed",
                    data: healthData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking Redis circuit breaker health");
                return HealthCheckResult.Unhealthy("Circuit breaker health check failed", ex);
            }
        }
    }
}