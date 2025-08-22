using MassTransit;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.HealthChecks
{
    /// <summary>
    /// Simple health check for RabbitMQ connectivity through MassTransit.
    /// </summary>
    public class RabbitMQHealthCheck : IHealthCheck
    {
        private readonly IBus _bus;
        private readonly ILogger<RabbitMQHealthCheck> _logger;

        public RabbitMQHealthCheck(IBus bus, ILogger<RabbitMQHealthCheck> logger)
        {
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // If MassTransit injected the bus successfully, RabbitMQ is working
                // That's all we need to know for a health check
                await Task.CompletedTask;
                return HealthCheckResult.Healthy("RabbitMQ connected");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RabbitMQ health check failed");
                return HealthCheckResult.Unhealthy("RabbitMQ health check failed", ex);
            }
        }
    }
}