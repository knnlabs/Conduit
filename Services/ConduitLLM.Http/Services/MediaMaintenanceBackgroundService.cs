using ConduitLLM.Core.Interfaces;
using Microsoft.Extensions.Options;
using ConduitLLM.Core.Services;

namespace ConduitLLM.Http.Services
{
    /// <summary>
    /// Background service that performs scheduled media lifecycle maintenance tasks.
    /// </summary>
    public class MediaMaintenanceBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<MediaMaintenanceBackgroundService> _logger;
        private readonly MediaManagementOptions _options;
        private readonly Timer _dailyTimer;
        private readonly Timer _weeklyTimer;
        private readonly Timer _monthlyTimer;

        /// <summary>
        /// Initializes a new instance of the MediaMaintenanceBackgroundService class.
        /// </summary>
        /// <param name="serviceScopeFactory">The service scope factory for creating scoped services.</param>
        /// <param name="logger">The logger instance.</param>
        /// <param name="options">Media management configuration options.</param>
        public MediaMaintenanceBackgroundService(
            IServiceScopeFactory serviceScopeFactory,
            ILogger<MediaMaintenanceBackgroundService> logger,
            IOptions<MediaManagementOptions> options)
        {
            _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? new MediaManagementOptions();

            // Initialize timers (will be started in ExecuteAsync)
            _dailyTimer = new Timer(OnDailyTimer, null, Timeout.Infinite, Timeout.Infinite);
            _weeklyTimer = new Timer(OnWeeklyTimer, null, Timeout.Infinite, Timeout.Infinite);
            _monthlyTimer = new Timer(OnMonthlyTimer, null, Timeout.Infinite, Timeout.Infinite);
        }

        /// <summary>
        /// Executes the background service.
        /// </summary>
        /// <param name="stoppingToken">The cancellation token to stop the service.</param>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Media maintenance background service starting");

            if (!_options.EnableAutoCleanup)
            {
                _logger.LogWarning("Media auto cleanup is disabled. Media maintenance service will not run.");
                return;
            }

            // Calculate time until next scheduled runs
            var now = DateTime.UtcNow;
            var nextDaily = GetNextScheduledTime(now, TimeSpan.FromHours(2)); // Run at 2 AM UTC daily
            var nextWeekly = GetNextScheduledTime(now, TimeSpan.FromHours(3), DayOfWeek.Sunday); // Run at 3 AM UTC on Sundays
            var nextMonthly = GetNextScheduledTime(now, TimeSpan.FromHours(4), dayOfMonth: 1); // Run at 4 AM UTC on the 1st of each month

            // Schedule timers
            _dailyTimer.Change(nextDaily - now, TimeSpan.FromDays(1));
            _weeklyTimer.Change(nextWeekly - now, TimeSpan.FromDays(7));
            _monthlyTimer.Change(nextMonthly - now, TimeSpan.FromDays(30)); // Approximate, will adjust each run

            _logger.LogInformation(
                "Media maintenance scheduled - Daily: {Daily}, Weekly: {Weekly}, Monthly: {Monthly}",
                nextDaily, nextWeekly, nextMonthly);

            // Keep the service running
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        /// <summary>
        /// Handles daily maintenance tasks.
        /// </summary>
        private void OnDailyTimer(object? state)
        {
            _ = ExecuteDailyMaintenanceAsync();
        }

        private async Task ExecuteDailyMaintenanceAsync()
        {
            try
            {
                _logger.LogInformation("Running daily media maintenance tasks");

                using var scope = _serviceScopeFactory.CreateScope();
                var mediaLifecycleService = scope.ServiceProvider.GetRequiredService<IMediaLifecycleService>();

                // Cleanup expired media
                var expiredCount = await mediaLifecycleService.CleanupExpiredMediaAsync();
                if (expiredCount > 0)
                {
                    _logger.LogInformation("Daily maintenance: Cleaned up {Count} expired media files", expiredCount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during daily media maintenance");
            }
        }

        /// <summary>
        /// Handles weekly maintenance tasks.
        /// </summary>
        private void OnWeeklyTimer(object? state)
        {
            _ = ExecuteWeeklyMaintenanceAsync();
        }

        private async Task ExecuteWeeklyMaintenanceAsync()
        {
            try
            {
                _logger.LogInformation("Running weekly media maintenance tasks");

                using var scope = _serviceScopeFactory.CreateScope();
                var mediaLifecycleService = scope.ServiceProvider.GetRequiredService<IMediaLifecycleService>();

                // Cleanup orphaned media
                if (_options.OrphanCleanupEnabled)
                {
                    var orphanedCount = await mediaLifecycleService.CleanupOrphanedMediaAsync();
                    if (orphanedCount > 0)
                    {
                        _logger.LogInformation("Weekly maintenance: Cleaned up {Count} orphaned media files", orphanedCount);
                    }
                }

                // Reschedule for next week
                _weeklyTimer.Change(TimeSpan.FromDays(7), TimeSpan.FromDays(7));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during weekly media maintenance");
            }
        }

        /// <summary>
        /// Handles monthly maintenance tasks.
        /// </summary>
        private void OnMonthlyTimer(object? state)
        {
            _ = ExecuteMonthlyMaintenanceAsync();
        }

        private async Task ExecuteMonthlyMaintenanceAsync()
        {
            try
            {
                _logger.LogInformation("Running monthly media maintenance tasks");

                using var scope = _serviceScopeFactory.CreateScope();
                var mediaLifecycleService = scope.ServiceProvider.GetRequiredService<IMediaLifecycleService>();

                // Prune old media based on retention policy
                if (_options.MediaRetentionDays > 0)
                {
                    var prunedCount = await mediaLifecycleService.PruneOldMediaAsync(
                        _options.MediaRetentionDays, 
                        respectRecentAccess: true);
                    
                    if (prunedCount > 0)
                    {
                        _logger.LogInformation(
                            "Monthly maintenance: Pruned {Count} media files older than {Days} days",
                            prunedCount, _options.MediaRetentionDays);
                    }
                }

                // Get and log storage statistics
                var stats = await mediaLifecycleService.GetOverallStorageStatsAsync();
                _logger.LogInformation(
                    "Monthly storage report - Total files: {Files}, Total size: {Size} bytes, Orphaned: {Orphaned}",
                    stats.TotalFiles, stats.TotalSizeBytes, stats.OrphanedFiles);

                // Reschedule for next month
                var nextMonthly = GetNextScheduledTime(DateTime.UtcNow, TimeSpan.FromHours(4), dayOfMonth: 1);
                _monthlyTimer.Change(nextMonthly - DateTime.UtcNow, Timeout.InfiniteTimeSpan);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during monthly media maintenance");
            }
        }

        /// <summary>
        /// Calculates the next scheduled time based on the given parameters.
        /// </summary>
        private DateTime GetNextScheduledTime(DateTime now, TimeSpan timeOfDay, DayOfWeek? dayOfWeek = null, int? dayOfMonth = null)
        {
            var scheduledTime = now.Date.Add(timeOfDay);

            if (dayOfMonth.HasValue)
            {
                // Monthly schedule
                if (now.Day > dayOfMonth.Value || (now.Day == dayOfMonth.Value && now.TimeOfDay > timeOfDay))
                {
                    // Move to next month
                    scheduledTime = scheduledTime.AddMonths(1);
                }
                // Adjust to the specified day of month
                var daysToAdd = dayOfMonth.Value - scheduledTime.Day;
                scheduledTime = scheduledTime.AddDays(daysToAdd);
            }
            else if (dayOfWeek.HasValue)
            {
                // Weekly schedule
                var daysUntilTarget = ((int)dayOfWeek.Value - (int)now.DayOfWeek + 7) % 7;
                if (daysUntilTarget == 0 && now.TimeOfDay > timeOfDay)
                {
                    daysUntilTarget = 7; // Next week
                }
                scheduledTime = scheduledTime.AddDays(daysUntilTarget);
            }
            else
            {
                // Daily schedule
                if (now.TimeOfDay > timeOfDay)
                {
                    scheduledTime = scheduledTime.AddDays(1);
                }
            }

            return scheduledTime;
        }

        /// <summary>
        /// Disposes the service and its timers.
        /// </summary>
        public override void Dispose()
        {
            _dailyTimer?.Dispose();
            _weeklyTimer?.Dispose();
            _monthlyTimer?.Dispose();
            base.Dispose();
        }
    }
}