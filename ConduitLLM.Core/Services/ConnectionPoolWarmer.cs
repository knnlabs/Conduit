using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Background service that warms up the database connection pool on startup.
    /// This ensures connections are pre-established for better performance.
    /// </summary>
    public class ConnectionPoolWarmer : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ConnectionPoolWarmer> _logger;
        private readonly int _connectionsToWarm;
        private readonly string? _serviceType;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConnectionPoolWarmer"/> class.
        /// </summary>
        /// <param name="serviceProvider">Service provider for creating scoped services.</param>
        /// <param name="logger">Logger instance.</param>
        /// <param name="serviceType">Service type (CoreAPI, AdminAPI) to determine warm connection count.</param>
        public ConnectionPoolWarmer(
            IServiceProvider serviceProvider,
            ILogger<ConnectionPoolWarmer> logger,
            string? serviceType = null)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceType = serviceType;
            
            // Determine number of connections to warm based on service type
            _connectionsToWarm = DetermineConnectionsToWarm(serviceType);
        }

        /// <summary>
        /// Starts the connection pool warming process.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting database connection pool warming for {ServiceType}...", 
                _serviceType ?? "Default");
            
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                // Create parallel connections to warm the pool
                var tasks = Enumerable.Range(0, _connectionsToWarm).Select(async i =>
                {
                    using var scope = _serviceProvider.CreateScope();
                    var dbContextFactory = scope.ServiceProvider.GetService<IDbContextFactory<ConduitLLM.Configuration.ConduitDbContext>>();
                    
                    if (dbContextFactory != null)
                    {
                        using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
                        
                        // Execute a simple query to ensure the connection is fully established
                        await dbContext.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken);
                        
                        _logger.LogDebug("Warmed connection {ConnectionNumber}/{TotalConnections}", 
                            i + 1, _connectionsToWarm);
                    }
                });
                
                await Task.WhenAll(tasks);
                
                stopwatch.Stop();
                
                _logger.LogInformation(
                    "Connection pool warmed successfully with {ConnectionCount} connections in {ElapsedMilliseconds}ms", 
                    _connectionsToWarm, 
                    stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                
                _logger.LogWarning(ex, 
                    "Failed to warm connection pool after {ElapsedMilliseconds}ms. " +
                    "This is not critical - connections will be established on demand.", 
                    stopwatch.ElapsedMilliseconds);
            }
        }

        /// <summary>
        /// Stops the service (no-op for connection warmer).
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A completed task.</returns>
        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogDebug("Connection pool warmer stopped");
            return Task.CompletedTask;
        }
        
        private int DetermineConnectionsToWarm(string? serviceType)
        {
            switch (serviceType?.ToUpperInvariant())
            {
                case "COREAPI":
                    // Warm 10 connections for Core API (high traffic)
                    return 10;
                case "ADMINAPI":
                    // Warm 5 connections for Admin API (medium traffic)
                    return 5;
                case "WEBUI":
                    // WebUI doesn't use database directly
                    return 0;
                default:
                    // Default to 5 connections
                    return 5;
            }
        }
    }
}