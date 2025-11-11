using System;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Base class for background services that require leader election to ensure single-instance processing
    /// </summary>
    public abstract class LeaderElectedBackgroundService : BackgroundService
    {
        private readonly ILeaderElectionService _leaderElectionService;
        private readonly ILogger _logger;
        private readonly string _serviceName;
        private readonly TimeSpan _leaderCheckInterval;
        private bool _isLeader;
        private bool _wasLeader;

        /// <summary>
        /// Initializes a new instance of the LeaderElectedBackgroundService class
        /// </summary>
        /// <param name="leaderElectionService">Leader election service</param>
        /// <param name="logger">Logger instance</param>
        /// <param name="serviceName">Unique name for this service (used for leader election)</param>
        protected LeaderElectedBackgroundService(
            ILeaderElectionService leaderElectionService,
            ILogger logger,
            string? serviceName = null)
        {
            _leaderElectionService = leaderElectionService ?? throw new ArgumentNullException(nameof(leaderElectionService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceName = serviceName ?? GetType().Name;
            _leaderCheckInterval = TimeSpan.FromSeconds(10);
            _isLeader = false;
            _wasLeader = false;
        }

        /// <summary>
        /// Gets whether this instance is currently the leader
        /// </summary>
        protected bool IsLeader => _isLeader;

        /// <summary>
        /// Gets the unique service name used for leader election
        /// </summary>
        protected string ServiceName => _serviceName;

        /// <summary>
        /// Main execution loop with leader election
        /// </summary>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Starting leader-elected service {ServiceName}", _serviceName);

            // Initial delay to allow system to stabilize
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Check if we are the leader
                    _isLeader = await _leaderElectionService.IsLeaderAsync(_serviceName, stoppingToken);

                    if (!_isLeader)
                    {
                        // Try to acquire leadership
                        _isLeader = await _leaderElectionService.TryAcquireLeadershipAsync(_serviceName, stoppingToken);
                    }

                    if (_isLeader && !_wasLeader)
                    {
                        // Just became leader
                        _logger.LogInformation("Service {ServiceName} became leader on instance {InstanceId}", 
                            _serviceName, Environment.MachineName);
                        await OnBecameLeaderAsync(stoppingToken);
                        _wasLeader = true;
                    }
                    else if (!_isLeader && _wasLeader)
                    {
                        // Lost leadership
                        _logger.LogInformation("Service {ServiceName} lost leadership on instance {InstanceId}", 
                            _serviceName, Environment.MachineName);
                        await OnLostLeadershipAsync(stoppingToken);
                        _wasLeader = false;
                    }

                    if (_isLeader)
                    {
                        // Execute the service logic
                        await ExecuteLeaderWorkAsync(stoppingToken);
                    }
                    else
                    {
                        // Not leader, wait before checking again
                        _logger.LogTrace("Service {ServiceName} is not leader, waiting {Interval} before retry", 
                            _serviceName, _leaderCheckInterval);
                        await Task.Delay(_leaderCheckInterval, stoppingToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancellation is requested
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in leader-elected service {ServiceName}", _serviceName);
                    
                    // Wait before retrying
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                }
            }

            // Clean up leadership on shutdown
            if (_isLeader)
            {
                try
                {
                    await _leaderElectionService.ReleaseLeadershipAsync(_serviceName);
                    _logger.LogInformation("Service {ServiceName} released leadership on shutdown", _serviceName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error releasing leadership for service {ServiceName} on shutdown", _serviceName);
                }
            }
        }

        /// <summary>
        /// Called when this instance becomes the leader
        /// Override to perform initialization tasks when becoming leader
        /// </summary>
        protected virtual Task OnBecameLeaderAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Called when this instance loses leadership
        /// Override to perform cleanup tasks when losing leadership
        /// </summary>
        protected virtual Task OnLostLeadershipAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Executes the main work of the service when this instance is the leader
        /// Must be implemented by derived classes
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the asynchronous operation</returns>
        protected abstract Task ExecuteLeaderWorkAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Disposes resources used by the service
        /// </summary>
        public override async void Dispose()
        {
            if (_isLeader)
            {
                try
                {
                    await _leaderElectionService.ReleaseLeadershipAsync(_serviceName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error releasing leadership during disposal for service {ServiceName}", _serviceName);
                }
            }

            base.Dispose();
        }
    }
}