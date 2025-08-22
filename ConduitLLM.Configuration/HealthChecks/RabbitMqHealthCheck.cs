using Microsoft.Extensions.Diagnostics.HealthChecks;

using RabbitMQ.Client;

namespace ConduitLLM.Configuration.HealthChecks
{
    /// <summary>
    /// Health check for RabbitMQ connectivity.
    /// </summary>
    public class RabbitMqHealthCheck : IHealthCheck
    {
        private readonly RabbitMqConfiguration _configuration;

        /// <summary>
        /// Initializes a new instance of the <see cref="RabbitMqHealthCheck"/> class.
        /// </summary>
        /// <param name="configuration">The RabbitMQ configuration.</param>
        public RabbitMqHealthCheck(RabbitMqConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        /// <summary>
        /// Runs the health check, returning the status of the RabbitMQ connection.
        /// </summary>
        /// <param name="context">A context object associated with the current execution.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the health check.</param>
        /// <returns>A task that represents the asynchronous health check operation.</returns>
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                var factory = new ConnectionFactory
                {
                    HostName = _configuration.Host,
                    Port = _configuration.Port,
                    UserName = _configuration.Username,
                    Password = _configuration.Password,
                    VirtualHost = _configuration.VHost,
                    RequestedHeartbeat = TimeSpan.FromSeconds(_configuration.HeartbeatInterval),
                    NetworkRecoveryInterval = TimeSpan.FromSeconds(_configuration.NetworkRecoveryInterval),
                    AutomaticRecoveryEnabled = _configuration.AutomaticRecoveryEnabled
                };

                using var connection = await factory.CreateConnectionAsync();
                // RabbitMQ.Client v7.x uses CreateChannel() instead of CreateModel()
                using var channel = await connection.CreateChannelAsync();
                
                // Just verify we can create a channel successfully
                // Don't check for specific exchanges as they may not exist on first startup
                
                return await Task.FromResult(HealthCheckResult.Healthy(
                    $"RabbitMQ connection established to {_configuration.Host}:{_configuration.Port}"));
            }
            catch (Exception ex)
            {
                return await Task.FromResult(HealthCheckResult.Unhealthy(
                    $"RabbitMQ connection failed to {_configuration.Host}:{_configuration.Port}",
                    exception: ex));
            }
        }
    }
}