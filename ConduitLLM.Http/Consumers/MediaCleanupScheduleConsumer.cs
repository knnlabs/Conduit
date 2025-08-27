using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ConduitLLM.Core.Events;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Options;

namespace ConduitLLM.Http.Consumers
{
    /// <summary>
    /// Processes scheduled cleanup requests and triggers retention checks for all groups.
    /// </summary>
    public class MediaCleanupScheduleConsumer : IConsumer<MediaCleanupScheduleRequested>
    {
        private readonly IConfigurationDbContext _context;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly MediaLifecycleOptions _options;
        private readonly ILogger<MediaCleanupScheduleConsumer> _logger;

        public MediaCleanupScheduleConsumer(
            IConfigurationDbContext context,
            IPublishEndpoint publishEndpoint,
            IOptions<MediaLifecycleOptions> options,
            ILogger<MediaCleanupScheduleConsumer> logger)
        {
            _context = context;
            _publishEndpoint = publishEndpoint;
            _options = options.Value;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<MediaCleanupScheduleRequested> context)
        {
            var message = context.Message;
            
            _logger.LogInformation(
                "Processing scheduled cleanup request from scheduler {SchedulerId} at {Time}, DryRun: {DryRun}",
                message.SchedulerId, message.ScheduledAt, message.IsDryRun);

            try
            {
                // Get groups to process (all groups since Balance is not nullable)
                IQueryable<int> groupQuery = _context.VirtualKeyGroups
                    .Select(g => g.Id);

                // Filter by target groups if specified
                if (message.TargetGroupIds?.Any() == true)
                {
                    groupQuery = groupQuery.Where(g => message.TargetGroupIds.Contains(g));
                }
                // Or filter by test groups if in test mode
                else if (_options.TestVirtualKeyGroups.Any())
                {
                    groupQuery = groupQuery.Where(g => _options.TestVirtualKeyGroups.Contains(g));
                }

                var groupIds = await groupQuery.ToListAsync();
                
                if (!groupIds.Any())
                {
                    _logger.LogInformation("No virtual key groups found to process");
                    return;
                }

                _logger.LogInformation(
                    "Found {Count} virtual key groups to process for retention checks",
                    groupIds.Count);

                // Publish retention check events for each group
                var publishedCount = 0;
                foreach (var groupId in groupIds)
                {
                    var retentionCheck = new MediaRetentionCheckRequested(
                        groupId,
                        DateTime.UtcNow,
                        "Scheduled");

                    if (message.IsDryRun || _options.DryRunMode)
                    {
                        _logger.LogDebug(
                            "[DRY RUN] Would publish retention check for group {GroupId}",
                            groupId);
                    }
                    else
                    {
                        await _publishEndpoint.Publish(retentionCheck);
                        publishedCount++;
                        
                        _logger.LogDebug(
                            "Published retention check for group {GroupId}",
                            groupId);
                    }

                    // Spread out events to avoid thundering herd
                    await Task.Delay(100);
                }

                _logger.LogInformation(
                    "Scheduled cleanup completed. Published {Published}/{Total} retention checks",
                    publishedCount, groupIds.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error processing scheduled cleanup request from scheduler {SchedulerId}",
                    message.SchedulerId);
                throw;
            }
        }
    }
}