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
        private readonly IDbContextFactory<ConduitDbContext> _contextFactory;
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
            IDbContextFactory<ConduitDbContext> contextFactory,
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
                
                // Analyze pool health based on connection acquisition time
                var data = new Dictionary<string, object>
                {
                    ["maxPoolSize"] = poolStats.MaxPoolSize,
                    ["minPoolSize"] = poolStats.MinPoolSize,
                    ["connectionAcquisitionTimeMs"] = stopwatch.ElapsedMilliseconds,
                    ["database"] = connection.Database,
                    ["dataSource"] = connection.DataSource
                };

                // Health determination based on connection acquisition time:
                // - <50ms: Healthy (pool has available connections)
                // - 50-200ms: Degraded (pool under pressure)
                // - >200ms: Unhealthy (pool likely exhausted)
                // - Timeout/Exception: Unhealthy (pool exhausted or database down)
                
                if (stopwatch.ElapsedMilliseconds > 200)
                {
                    _logger.LogError("Critical connection acquisition time: {ElapsedMilliseconds}ms (pool likely exhausted)", 
                        stopwatch.ElapsedMilliseconds);
                    
                    return HealthCheckResult.Unhealthy(
                        $"Connection pool exhausted: {stopwatch.ElapsedMilliseconds}ms acquisition time", 
                        null, 
                        data);
                }
                else if (stopwatch.ElapsedMilliseconds > CONNECTION_ACQUISITION_TIMEOUT_MS)
                {
                    data["warning"] = $"Connection acquisition time ({stopwatch.ElapsedMilliseconds}ms) indicates pool pressure";
                    
                    _logger.LogWarning("Slow connection acquisition detected: {ElapsedMilliseconds}ms", 
                        stopwatch.ElapsedMilliseconds);
                    
                    return HealthCheckResult.Degraded(
                        $"Connection pool under pressure: {stopwatch.ElapsedMilliseconds}ms acquisition time", 
                        null, 
                        data);
                }
                
                return HealthCheckResult.Healthy(
                    $"Connection pool healthy: {stopwatch.ElapsedMilliseconds}ms acquisition time",
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
            
            // Note: We don't query pg_stat_activity here because:
            // 1. It's expensive to run frequently (every 10-60s per instance)
            // 2. With many instances, it creates significant database load
            // 3. Connection acquisition timing is a better health indicator
            //
            // Instead, we rely on:
            // - Connection acquisition time (already measured in CheckHealthAsync)
            // - The ability to open a connection (if we can't, pool is exhausted)
            // - Simple query execution success
            //
            // For actual connection monitoring, use PostgreSQL metrics directly
            // or dedicated monitoring tools, not health checks.
            
            return new ConnectionPoolStats
            {
                Active = -1,  // Unknown without expensive queries
                Idle = -1,    // Unknown without expensive queries
                MaxPoolSize = maxPoolSize,
                MinPoolSize = minPoolSize,
                UsagePercent = -1  // Will be calculated based on acquisition time
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