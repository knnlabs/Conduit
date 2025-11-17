using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Base class for background services that need to access scoped services.
    /// This properly handles the lifetime mismatch between singleton background services
    /// and scoped dependencies like DbContext.
    /// </summary>
    public abstract class ScopedBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger _logger;
        private readonly string _serviceName;

        protected ScopedBackgroundService(
            IServiceScopeFactory serviceScopeFactory,
            ILogger logger,
            string? serviceName = null)
        {
            _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceName = serviceName ?? GetType().Name;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("{ServiceName} started", _serviceName);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceScopeFactory.CreateScope();
                    await ExecuteScopedAsync(scope.ServiceProvider, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in {ServiceName}", _serviceName);
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }

                await Task.Delay(GetDelay(), stoppingToken);
            }

            _logger.LogInformation("{ServiceName} stopped", _serviceName);
        }

        /// <summary>
        /// Override this method to perform work with scoped services.
        /// A new scope is created for each execution.
        /// </summary>
        protected abstract Task ExecuteScopedAsync(IServiceProvider scopedServiceProvider, CancellationToken stoppingToken);

        /// <summary>
        /// Override to control the delay between executions.
        /// Default is 60 seconds.
        /// </summary>
        protected virtual TimeSpan GetDelay() => TimeSpan.FromSeconds(60);
    }
}