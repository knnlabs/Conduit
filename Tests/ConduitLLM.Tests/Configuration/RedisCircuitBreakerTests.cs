using System;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Options;
using ConduitLLM.Configuration.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Configuration
{
    /// <summary>
    /// Simplified tests focusing on circuit breaker behavior
    /// </summary>
    public class RedisCircuitBreakerTests
    {
        [Fact]
        public void CircuitBreaker_ManualControl_Works()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<RedisCircuitBreaker>>();
            var options = new RedisCircuitBreakerOptions
            {
                FailureThreshold = 3,
                OpenDurationSeconds = 5,
                EnableManualControl = true
            };
            
            // Create a mock Redis factory that returns null (simulating no Redis)
            var mockRedisFactory = new RedisConnectionFactory(
                Microsoft.Extensions.Options.Options.Create(new ConduitLLM.Configuration.Options.CacheOptions()),
                Mock.Of<ILogger<RedisConnectionFactory>>());
            
            var circuitBreaker = new RedisCircuitBreaker(
                mockLogger.Object,
                Microsoft.Extensions.Options.Options.Create(options),
                mockRedisFactory);

            // Test manual trip
            circuitBreaker.Trip("Manual test");
            Assert.True(circuitBreaker.IsOpen);
            Assert.Equal(CircuitState.Open, circuitBreaker.State);

            // Test manual reset
            circuitBreaker.Reset();
            Assert.False(circuitBreaker.IsOpen);
            Assert.Equal(CircuitState.Closed, circuitBreaker.State);
        }

        [Fact]
        public async Task CircuitBreaker_RejectsRequestsWhenOpen()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<RedisCircuitBreaker>>();
            var options = new RedisCircuitBreakerOptions
            {
                FailureThreshold = 3,
                OpenDurationSeconds = 5,
                EnableManualControl = true
            };
            
            var mockRedisFactory = new RedisConnectionFactory(
                Microsoft.Extensions.Options.Options.Create(new ConduitLLM.Configuration.Options.CacheOptions()),
                Mock.Of<ILogger<RedisConnectionFactory>>());
            
            var circuitBreaker = new RedisCircuitBreaker(
                mockLogger.Object,
                Microsoft.Extensions.Options.Options.Create(options),
                mockRedisFactory);

            // Manually trip the circuit
            circuitBreaker.Trip("Test trip");
            Assert.True(circuitBreaker.IsOpen);

            // Act & Assert - Should throw RedisCircuitBreakerOpenException
            await Assert.ThrowsAsync<RedisCircuitBreakerOpenException>(async () =>
            {
                await circuitBreaker.ExecuteAsync(async () =>
                {
                    await Task.CompletedTask;
                    return "Should not execute";
                });
            });

            // Verify rejection was recorded
            var stats = circuitBreaker.Statistics;
            Assert.True(stats.RejectedRequests > 0);
        }
    }
}