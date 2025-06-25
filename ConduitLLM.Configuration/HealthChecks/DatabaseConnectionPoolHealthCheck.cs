using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

using Npgsql;

namespace ConduitLLM.Configuration.HealthChecks
{
    /// <summary>
    /// Health check for monitoring database connection pool usage and performance.
    /// </summary>
    public class DatabaseConnectionPoolHealthCheck : IHealthCheck
    {
        private readonly IDbContextFactory<ConfigurationDbContext> _contextFactory;
        private readonly ILogger<DatabaseConnectionPoolHealthCheck> _logger;
        private const double WARNING_THRESHOLD_PERCENT = 80.0;
        private const double CRITICAL_THRESHOLD_PERCENT = 90.0;
        private const int CONNECTION_ACQUISITION_TIMEOUT_MS = 50;

        /// <summary>
        /// Initializes a new instance of the <see cref="DatabaseConnectionPoolHealthCheck"/> class.
        /// </summary>
        /// <param name="contextFactory">Database context factory.</param>
        /// <param name="logger">Logger instance.</param>
        public DatabaseConnectionPoolHealthCheck(
            IDbContextFactory<ConfigurationDbContext> contextFactory,
            ILogger<DatabaseConnectionPoolHealthCheck> logger)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Performs the health check on the database connection pool.
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
                using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
                var connection = dbContext.Database.GetDbConnection() as NpgsqlConnection;
                
                if (connection == null)
                {
                    return HealthCheckResult.Healthy("Not using PostgreSQL - connection pool monitoring not applicable");
                }

                // Measure connection acquisition time
                var stopwatch = Stopwatch.StartNew();
                await connection.OpenAsync(cancellationToken);
                stopwatch.Stop();
                
                // Get connection pool statistics
                var poolStats = GetConnectionPoolStats(connection);
                
                // Execute a simple query to verify the connection is working
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT 1";
                await command.ExecuteScalarAsync(cancellationToken);
                
                // Close connection to return it to the pool
                await connection.CloseAsync();
                
                // Analyze pool health
                var data = new Dictionary<string, object>
                {
                    ["activeConnections"] = poolStats.Active,
                    ["idleConnections"] = poolStats.Idle,
                    ["maxPoolSize"] = poolStats.MaxPoolSize,
                    ["minPoolSize"] = poolStats.MinPoolSize,
                    ["usagePercent"] = poolStats.UsagePercent,
                    ["connectionAcquisitionTimeMs"] = stopwatch.ElapsedMilliseconds,
                    ["database"] = connection.Database,
                    ["dataSource"] = connection.DataSource
                };

                // Check connection acquisition time
                if (stopwatch.ElapsedMilliseconds > CONNECTION_ACQUISITION_TIMEOUT_MS)
                {
                    data["warning"] = $"Connection acquisition time ({stopwatch.ElapsedMilliseconds}ms) exceeded threshold ({CONNECTION_ACQUISITION_TIMEOUT_MS}ms)";
                    
                    _logger.LogWarning("Slow connection acquisition detected: {ElapsedMilliseconds}ms", 
                        stopwatch.ElapsedMilliseconds);
                }

                // Determine health status based on pool usage
                if (poolStats.UsagePercent >= CRITICAL_THRESHOLD_PERCENT)
                {
                    _logger.LogError("Critical connection pool usage: {UsagePercent}% ({Active}/{MaxPoolSize})",
                        poolStats.UsagePercent, poolStats.Active, poolStats.MaxPoolSize);
                    
                    return HealthCheckResult.Unhealthy(
                        $"Connection pool critical: {poolStats.UsagePercent:F1}% usage", 
                        null, 
                        data);
                }
                else if (poolStats.UsagePercent >= WARNING_THRESHOLD_PERCENT)
                {
                    _logger.LogWarning("High connection pool usage: {UsagePercent}% ({Active}/{MaxPoolSize})",
                        poolStats.UsagePercent, poolStats.Active, poolStats.MaxPoolSize);
                    
                    return HealthCheckResult.Degraded(
                        $"Connection pool usage high: {poolStats.UsagePercent:F1}%", 
                        null, 
                        data);
                }
                
                return HealthCheckResult.Healthy(
                    $"Connection pool healthy: {poolStats.Active}/{poolStats.MaxPoolSize} connections in use",
                    data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database connection pool health check failed");
                return HealthCheckResult.Unhealthy("Database connection pool health check failed", ex);
            }
        }
        
        private ConnectionPoolStats GetConnectionPoolStats(NpgsqlConnection connection)
        {
            // Get connection pool settings from connection string
            var maxPoolSize = GetMaxPoolSizeFromConnectionString(connection.ConnectionString);
            var minPoolSize = GetMinPoolSizeFromConnectionString(connection.ConnectionString);
            
            // Query PostgreSQL for current connection statistics
            // This gives us actual database-level connection counts
            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT 
                        COUNT(*) FILTER (WHERE state = 'active') as active_count,
                        COUNT(*) FILTER (WHERE state = 'idle') as idle_count,
                        COUNT(*) as total_count
                    FROM pg_stat_activity
                    WHERE datname = current_database()
                      AND pid != pg_backend_pid()";
                
                using var reader = command.ExecuteReader();
                if (reader.Read())
                {
                    var activeCount = reader.GetInt64(0);
                    var idleCount = reader.GetInt64(1);
                    var totalCount = reader.GetInt64(2);
                    
                    return new ConnectionPoolStats
                    {
                        Active = (int)activeCount,
                        Idle = (int)idleCount,
                        MaxPoolSize = maxPoolSize,
                        MinPoolSize = minPoolSize,
                        UsagePercent = maxPoolSize > 0 ? 
                            ((double)totalCount / maxPoolSize) * 100 : 0
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to query connection statistics from pg_stat_activity");
            }
            
            // Fallback if statistics are not available
            return new ConnectionPoolStats
            {
                Active = 0,
                Idle = 0,
                MaxPoolSize = maxPoolSize,
                MinPoolSize = minPoolSize,
                UsagePercent = 0
            };
        }
        
        private int GetMaxPoolSizeFromConnectionString(string connectionString)
        {
            try
            {
                var builder = new NpgsqlConnectionStringBuilder(connectionString);
                return builder.MaxPoolSize;
            }
            catch
            {
                return 100; // Default Npgsql max pool size
            }
        }
        
        private int GetMinPoolSizeFromConnectionString(string connectionString)
        {
            try
            {
                var builder = new NpgsqlConnectionStringBuilder(connectionString);
                return builder.MinPoolSize;
            }
            catch
            {
                return 1; // Default Npgsql min pool size
            }
        }
        
        private class ConnectionPoolStats
        {
            public int Active { get; set; }
            public int Idle { get; set; }
            public int MaxPoolSize { get; set; }
            public int MinPoolSize { get; set; }
            public double UsagePercent { get; set; }
        }
    }
}