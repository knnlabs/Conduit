using MassTransit;
using Microsoft.Extensions.Options;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Configuration.Options;

namespace ConduitLLM.Http.Services
{
    /// <summary>
    /// Distributed scheduler service for media lifecycle management.
    /// Uses Redis-based leader election to ensure only one scheduler runs across multiple instances.
    /// </summary>
    public class DistributedMediaSchedulerService : BackgroundService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IDistributedLockService _lockService;
        private readonly MediaLifecycleOptions _options;
        private readonly IConfiguration _configuration;
        private readonly ILogger<DistributedMediaSchedulerService> _logger;
        private readonly string _instanceId;

        public DistributedMediaSchedulerService(
            IServiceScopeFactory serviceScopeFactory,
            IDistributedLockService lockService,
            IOptions<MediaLifecycleOptions> options,
            IConfiguration configuration,
            ILogger<DistributedMediaSchedulerService> logger)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _lockService = lockService;
            _options = options.Value;
            _configuration = configuration;
            _logger = logger;
            _instanceId = Guid.NewGuid().ToString("N")[..8]; // Short instance ID for logging
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Check if this instance should run the scheduler
            var schedulerMode = _options.SchedulerMode;
            var currentRole = _configuration["ServiceRole"] ?? "CoreApi";
            
            if (!ShouldRunScheduler(schedulerMode, currentRole))
            {
                _logger.LogInformation(
                    "Media scheduler disabled for instance {InstanceId} - Mode: {Mode}, Role: {Role}",
                    _instanceId, schedulerMode, currentRole);
                return;
            }

            _logger.LogInformation(
                "Media scheduler service starting on instance {InstanceId} - Mode: {Mode}, Role: {Role}",
                _instanceId, schedulerMode, currentRole);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await AttemptSchedulerExecution(stoppingToken);
                    
                    // Wait for the configured interval before next attempt
                    await Task.Delay(
                        TimeSpan.FromMinutes(_options.ScheduleIntervalMinutes), 
                        stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Normal shutdown
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Unexpected error in scheduler service on instance {InstanceId}",
                        _instanceId);
                    
                    // Wait before retrying to avoid tight error loops
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                }
            }

            _logger.LogInformation(
                "Media scheduler service stopping on instance {InstanceId}",
                _instanceId);
        }

        private async Task AttemptSchedulerExecution(CancellationToken stoppingToken)
        {
            // TODO: Implement Redis Sentinel or Cluster for high availability
            // TODO: Add secondary lock mechanism using database advisory locks
            // TODO: Implement heartbeat mechanism to detect zombie locks
            // TODO: Add metrics to detect split-brain scenarios
            
            var lockKey = "media:scheduler:leader";
            var lockDuration = TimeSpan.FromMinutes(5);
            
            // Try to acquire the leader lock
            using var lockHandle = await _lockService.AcquireLockAsync(
                lockKey, 
                lockDuration,
                stoppingToken);
            
            if (lockHandle == null)
            {
                _logger.LogDebug(
                    "Instance {InstanceId} could not acquire scheduler lock - another instance is leader",
                    _instanceId);
                return;
            }

            _logger.LogInformation(
                "Instance {InstanceId} acquired scheduler leadership for {Duration}",
                _instanceId, lockDuration);

            try
            {
                await RunScheduledCleanupAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error during scheduled cleanup on instance {InstanceId}",
                    _instanceId);
                
                // TODO: Implement dead letter queue for failing schedules
                throw;
            }
            finally
            {
                _logger.LogInformation(
                    "Instance {InstanceId} releasing scheduler leadership",
                    _instanceId);
            }
        }

        private async Task RunScheduledCleanupAsync(CancellationToken stoppingToken)
        {
            // TODO: Implement lease renewal during long operations
            // TODO: Implement checkpointing to resume after crash
            
            _logger.LogInformation(
                "Instance {InstanceId} starting scheduled media cleanup run",
                _instanceId);

            // Create a scope to resolve scoped services
            using var scope = _serviceScopeFactory.CreateScope();
            var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

            // Publish the schedule event that will trigger retention checks
            var scheduleEvent = new MediaCleanupScheduleRequested(
                DateTime.UtcNow,
                _instanceId)
            {
                IsDryRun = _options.DryRunMode
            };

            await publishEndpoint.Publish(scheduleEvent, stoppingToken);

            _logger.LogInformation(
                "Instance {InstanceId} published cleanup schedule event (DryRun: {DryRun})",
                _instanceId, _options.DryRunMode);
        }

        private bool ShouldRunScheduler(string schedulerMode, string currentRole)
        {
            return schedulerMode?.ToLowerInvariant() switch
            {
                "disabled" => false,
                "adminapi" => currentRole.Equals("AdminApi", StringComparison.OrdinalIgnoreCase),
                "coreapi" => currentRole.Equals("CoreApi", StringComparison.OrdinalIgnoreCase),
                "any" => true,
                _ => false // Default to disabled for safety
            };
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Media scheduler service received stop signal on instance {InstanceId}",
                _instanceId);
            
            await base.StopAsync(cancellationToken);
        }
    }
}