using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.WebUI.Services
{
    /// <summary>
    /// Background service that monitors the health of registered providers
    /// </summary>
    /// <remarks>
    /// Provider health monitoring is now managed by the Admin API
    /// This is a placeholder implementation that has been disabled during migration
    /// </remarks>
    public class ProviderHealthMonitorService : BackgroundService
    {
        private readonly ILogger<ProviderHealthMonitorService> _logger;

        /// <summary>
        /// Initializes a new instance of the ProviderHealthMonitorService class
        /// </summary>
        /// <param name="logger">Logger for monitoring activities</param>
        public ProviderHealthMonitorService(
            ILogger<ProviderHealthMonitorService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Background execution loop
        /// </summary>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Provider Health Monitor Service is running as a stub during migration");
            _logger.LogInformation("Provider health monitoring is now managed by the Admin API");

            // Background service needs to keep running
            while (!stoppingToken.IsCancellationRequested)
            {
                // Sleep for 24 hours
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
        }

        /// <summary>
        /// Called when the service is starting
        /// </summary>
        public override Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Provider Health Monitor Service is starting.");
            return base.StartAsync(cancellationToken);
        }

        /// <summary>
        /// Called when the service is stopping
        /// </summary>
        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Provider Health Monitor Service is stopping.");
            return base.StopAsync(cancellationToken);
        }
    }
}
