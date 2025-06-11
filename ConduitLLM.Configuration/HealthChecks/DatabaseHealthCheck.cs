using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ConduitLLM.Configuration.HealthChecks
{
    /// <summary>
    /// Health check for database connectivity.
    /// </summary>
    public class DatabaseHealthCheck : IHealthCheck
    {
        private readonly string _connectionString;
        private readonly ILogger<DatabaseHealthCheck> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DatabaseHealthCheck"/> class.
        /// </summary>
        /// <param name="connectionString">Database connection string.</param>
        /// <param name="logger">Logger instance.</param>
        public DatabaseHealthCheck(string connectionString, ILogger<DatabaseHealthCheck> logger)
        {
            _connectionString = connectionString;
            _logger = logger;
        }

        /// <summary>
        /// Performs the health check.
        /// </summary>
        /// <param name="context">Health check context.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Health check result.</returns>
        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Create connection with timeout in connection string
                var builder = new NpgsqlConnectionStringBuilder(_connectionString)
                {
                    Timeout = 5 // Set connection timeout to 5 seconds
                };
                
                using var connection = new NpgsqlConnection(builder.ConnectionString);
                
                await connection.OpenAsync(cancellationToken);
                
                // Execute a simple query to verify the connection is working
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT 1";
                
                var result = await command.ExecuteScalarAsync(cancellationToken);
                
                if (result != null && result.ToString() == "1")
                {
                    return HealthCheckResult.Healthy("Database connection is healthy", new Dictionary<string, object>
                    {
                        ["database"] = connection.Database,
                        ["dataSource"] = connection.DataSource,
                        ["serverVersion"] = connection.ServerVersion
                    });
                }
                
                return HealthCheckResult.Unhealthy("Database query returned unexpected result");
            }
            catch (NpgsqlException ex)
            {
                _logger.LogError(ex, "Database connection failed");
                
                var data = new Dictionary<string, object>
                {
                    ["error"] = ex.Message,
                    ["errorCode"] = ex.ErrorCode
                };
                
                // Determine if this is a critical error or just degraded
                if (ex.IsTransient)
                {
                    return HealthCheckResult.Degraded("Database connection degraded", ex, data);
                }
                
                return HealthCheckResult.Unhealthy("Database connection failed", ex, data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during database health check");
                return HealthCheckResult.Unhealthy("Database health check failed", ex);
            }
        }
    }
}