using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ConduitLLM.Core.Events;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Options;
using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Http.Consumers
{
    /// <summary>
    /// Evaluates media retention policies for a virtual key group and identifies media for cleanup.
    /// </summary>
    public class MediaRetentionPolicyConsumer : IConsumer<MediaRetentionCheckRequested>
    {
        private readonly IVirtualKeyGroupRepository _groupRepository;
        private readonly IMediaRecordRepository _mediaRepository;
        private readonly IConfigurationDbContext _context;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly MediaLifecycleOptions _options;
        private readonly ILogger<MediaRetentionPolicyConsumer> _logger;

        public MediaRetentionPolicyConsumer(
            IVirtualKeyGroupRepository groupRepository,
            IMediaRecordRepository mediaRepository,
            IConfigurationDbContext context,
            IPublishEndpoint publishEndpoint,
            IOptions<MediaLifecycleOptions> options,
            ILogger<MediaRetentionPolicyConsumer> logger)
        {
            _groupRepository = groupRepository;
            _mediaRepository = mediaRepository;
            _context = context;
            _publishEndpoint = publishEndpoint;
            _options = options.Value;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<MediaRetentionCheckRequested> context)
        {
            var message = context.Message;
            
            _logger.LogInformation(
                "Processing retention check for VirtualKeyGroup {GroupId}, Reason: {Reason}",
                message.VirtualKeyGroupId, message.Reason);

            try
            {
                // Check if we're in test mode and should skip this group
                if (_options.TestVirtualKeyGroups.Any() && 
                    !_options.TestVirtualKeyGroups.Contains(message.VirtualKeyGroupId))
                {
                    _logger.LogDebug(
                        "Skipping group {GroupId} - not in test groups list",
                        message.VirtualKeyGroupId);
                    return;
                }

                // Get VirtualKeyGroup with retention policy
                var group = await _context.VirtualKeyGroups
                    .Include(g => g.MediaRetentionPolicy)
                    .FirstOrDefaultAsync(g => g.Id == message.VirtualKeyGroupId);

                if (group == null)
                {
                    _logger.LogWarning(
                        "VirtualKeyGroup {GroupId} not found",
                        message.VirtualKeyGroupId);
                    return;
                }

                // Get retention policy (use default if none specified)
                var policy = group.MediaRetentionPolicy ?? await GetDefaultPolicyAsync();
                if (policy == null)
                {
                    _logger.LogWarning(
                        "No retention policy found for group {GroupId} and no default policy exists",
                        message.VirtualKeyGroupId);
                    return;
                }

                // Calculate retention days based on balance
                var retentionDays = group.Balance switch
                {
                    > 0 => policy.PositiveBalanceRetentionDays,
                    0 => policy.ZeroBalanceRetentionDays,
                    < 0 => policy.NegativeBalanceRetentionDays
                };

                _logger.LogInformation(
                    "Group {GroupId} balance: {Balance:C}, retention days: {Days}",
                    group.Id, group.Balance, retentionDays);

                // Calculate cutoff date
                var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);

                // Get all virtual keys in the group
                var virtualKeyIds = await _context.VirtualKeys
                    .Where(vk => vk.VirtualKeyGroupId == message.VirtualKeyGroupId)
                    .Select(vk => vk.Id)
                    .ToListAsync();

                if (!virtualKeyIds.Any())
                {
                    _logger.LogDebug("No virtual keys found in group {GroupId}", group.Id);
                    return;
                }

                // Query media records that are eligible for cleanup
                var mediaToDelete = await _context.MediaRecords
                    .Where(m => virtualKeyIds.Contains(m.VirtualKeyId))
                    .Where(m => m.CreatedAt < cutoffDate)
                    .Where(m => !policy.RespectRecentAccess || 
                               m.LastAccessedAt == null || 
                               m.LastAccessedAt < DateTime.UtcNow.AddDays(-policy.RecentAccessWindowDays))
                    .Select(m => new { m.StorageKey, m.SizeBytes })
                    .ToListAsync();

                if (!mediaToDelete.Any())
                {
                    _logger.LogInformation(
                        "No media eligible for cleanup in group {GroupId}",
                        group.Id);
                    return;
                }

                var totalSize = mediaToDelete.Sum(m => m.SizeBytes ?? 0);
                _logger.LogInformation(
                    "Found {Count} media files ({Size:N0} bytes) eligible for cleanup in group {GroupId}",
                    mediaToDelete.Count, totalSize, group.Id);

                // Check if manual approval is required
                if (_options.RequireManualApprovalForLargeBatches && 
                    mediaToDelete.Count > _options.LargeBatchThreshold)
                {
                    _logger.LogWarning(
                        "Batch of {Count} files exceeds threshold of {Threshold}. Manual approval required.",
                        mediaToDelete.Count, _options.LargeBatchThreshold);
                    
                    // TODO: Implement manual approval workflow
                    // For now, skip large batches
                    return;
                }

                // Batch media for deletion
                var batches = mediaToDelete
                    .Select(m => m.StorageKey)
                    .Chunk(_options.MaxBatchSize);

                foreach (var batch in batches)
                {
                    var cleanupBatch = new MediaCleanupBatchRequested(
                        message.VirtualKeyGroupId,
                        batch.ToList(),
                        message.Reason,
                        DateTime.UtcNow);

                    if (_options.DryRunMode)
                    {
                        _logger.LogInformation(
                            "[DRY RUN] Would publish cleanup batch with {Count} items for group {GroupId}",
                            batch.Length, group.Id);
                    }
                    else
                    {
                        await _publishEndpoint.Publish(cleanupBatch);
                        _logger.LogInformation(
                            "Published cleanup batch with {Count} items for group {GroupId}",
                            batch.Length, group.Id);
                    }

                    // Delay between batches to avoid overwhelming the system
                    if (_options.DelayBetweenBatchesMs > 0)
                    {
                        await Task.Delay(_options.DelayBetweenBatchesMs);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error processing retention check for group {GroupId}",
                    message.VirtualKeyGroupId);
                throw;
            }
        }

        private async Task<MediaRetentionPolicy?> GetDefaultPolicyAsync()
        {
            return await _context.MediaRetentionPolicies
                .FirstOrDefaultAsync(p => p.IsDefault && p.IsActive);
        }
    }
}