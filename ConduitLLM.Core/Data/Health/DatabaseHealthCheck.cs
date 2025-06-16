using System;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Data.Constants;
using ConduitLLM.Core.Data.Interfaces;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Data.Health
{
    /// <summary>
    /// Health check for database connectivity.
    /// </summary>
    public class DatabaseHealthCheck : IHealthCheck
    {
        private readonly IDatabaseConnectionFactory _connectionFactory;
        private readonly ILogger<DatabaseHealthCheck> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DatabaseHealthCheck"/> class.
        /// </summary>
        /// <param name="connectionFactory">The database connection factory.</param>
        /// <param name="logger">The logger.</param>
        public DatabaseHealthCheck(
            IDatabaseConnectionFactory connectionFactory,
            ILogger<DatabaseHealthCheck> logger)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Checking database health for provider: {Provider}", _connectionFactory.ProviderName);

                // Create and open a connection to test database connectivity
                using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

                // Perform a simple query to verify the connection works
                using var command = connection.CreateCommand();
                command.CommandText = _connectionFactory.ProviderName.ToLowerInvariant() switch
                {
                    var p when p == DatabaseConstants.POSTGRES_PROVIDER => "SELECT 1",
                    var p when p == DatabaseConstants.SQLITE_PROVIDER => "SELECT 1",
                    _ => throw new NotSupportedException($"Unsupported provider for health check: {_connectionFactory.ProviderName}")
                };

                await command.ExecuteScalarAsync(cancellationToken);

                return HealthCheckResult.Healthy($"Database connection is healthy. Provider: {_connectionFactory.ProviderName}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database health check failed for provider: {Provider}", _connectionFactory.ProviderName);

                return HealthCheckResult.Unhealthy(
                    $"Database connection failed. Provider: {_connectionFactory.ProviderName}",
                    ex);
            }
        }
    }
}
