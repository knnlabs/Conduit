using System;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Wraps an existing IHostedService with leader election capability
    /// </summary>
    public class LeaderElectedServiceWrapper : LeaderElectedBackgroundService
    {
        private readonly IHostedService _innerService;
        private readonly ILogger<LeaderElectedServiceWrapper> _logger;
        private CancellationTokenSource? _innerServiceCts;

        public LeaderElectedServiceWrapper(
            IHostedService innerService,
            ILeaderElectionService leaderElectionService,
            ILogger<LeaderElectedServiceWrapper> logger,
            string serviceName)
            : base(leaderElectionService, logger, serviceName)
        {
            _innerService = innerService ?? throw new ArgumentNullException(nameof(innerService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override async Task OnBecameLeaderAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting inner service {ServiceName} as leader", ServiceName);
            
            // Create a new cancellation token source for the inner service
            _innerServiceCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            
            // Start the inner service
            await _innerService.StartAsync(_innerServiceCts.Token);
        }

        protected override async Task OnLostLeadershipAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping inner service {ServiceName} as leadership was lost", ServiceName);
            
            // Stop the inner service
            if (_innerServiceCts != null)
            {
                _innerServiceCts.Cancel();
                
                try
                {
                    // Give the service time to stop gracefully
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                    await _innerService.StopAsync(cts.Token);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error stopping inner service {ServiceName}", ServiceName);
                }
                finally
                {
                    _innerServiceCts.Dispose();
                    _innerServiceCts = null;
                }
            }
        }

        protected override async Task ExecuteLeaderWorkAsync(CancellationToken cancellationToken)
        {
            // The inner service is already running in its own execution context
            // Just wait for a short period before checking leadership again
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
        }

        public override async void Dispose()
        {
            // Stop the inner service if it's running
            if (_innerServiceCts != null)
            {
                _innerServiceCts.Cancel();
                
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    await _innerService.StopAsync(cts.Token);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error stopping inner service during disposal");
                }
                finally
                {
                    _innerServiceCts.Dispose();
                }
            }

            // Dispose the inner service if it's disposable
            if (_innerService is IDisposable disposable)
            {
                disposable.Dispose();
            }

            base.Dispose();
        }
    }
}