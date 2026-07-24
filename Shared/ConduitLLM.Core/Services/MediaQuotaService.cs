using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Interfaces;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services;

/// <summary>
/// Database-backed media quota evaluator. Media rows are aggregated in SQL by group;
/// no media collection is materialized on the generation hot path.
/// </summary>
public sealed class MediaQuotaService : IMediaQuotaService
{
    private readonly IConfigurationDbContext _context;
    private readonly ILogger<MediaQuotaService> _logger;

    public MediaQuotaService(
        IConfigurationDbContext context,
        ILogger<MediaQuotaService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task EnsureCanStoreAsync(
        int virtualKeyId,
        long prospectiveSizeBytes,
        CancellationToken cancellationToken = default)
    {
        var assignment = await _context.VirtualKeys
            .AsNoTracking()
            .Where(key => key.Id == virtualKeyId)
            .Select(key => new
            {
                GroupId = key.VirtualKeyGroupId,
                PolicyId = key.VirtualKeyGroup.MediaRetentionPolicyId
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (assignment == null)
        {
            _logger.LogWarning(
                "Skipping media quota evaluation because virtual key {VirtualKeyId} was not found",
                virtualKeyId);
            return;
        }

        var policy = await ResolvePolicyAsync(assignment.PolicyId, cancellationToken);
        if (policy == null ||
            (!policy.MaxStorageSizeBytes.HasValue && !policy.MaxFileCount.HasValue))
        {
            return;
        }

        var usage = await QueryUsageAsync(assignment.GroupId, cancellationToken);
        var wouldUseBytes = checked(usage.TotalSizeBytes + Math.Max(0, prospectiveSizeBytes));
        var wouldUseFiles = checked(usage.TotalFiles + 1);
        var exceedsBytes = policy.MaxStorageSizeBytes.HasValue &&
            wouldUseBytes > policy.MaxStorageSizeBytes.Value;
        var exceedsFiles = policy.MaxFileCount.HasValue &&
            wouldUseFiles > policy.MaxFileCount.Value;

        if ((!exceedsBytes && !exceedsFiles) ||
            policy.QuotaExceededBehavior == MediaQuotaExceededBehavior.AllowAndEvict)
        {
            if (exceedsBytes || exceedsFiles)
            {
                _logger.LogInformation(
                    "Allowing media write for group {GroupId} above quota; scheduled eviction is configured",
                    assignment.GroupId);
            }

            return;
        }

        throw new RateLimitExceededException(
            BuildQuotaExceededMessage(
                assignment.GroupId,
                wouldUseBytes,
                wouldUseFiles,
                policy));
    }

    public async Task<IReadOnlyList<MediaGroupQuotaUsage>> GetGroupUsagesAsync(
        int? virtualKeyGroupId = null,
        CancellationToken cancellationToken = default)
    {
        var defaultPolicy = await _context.MediaRetentionPolicies
            .AsNoTracking()
            .Where(policy => policy.IsDefault && policy.IsActive)
            .OrderBy(policy => policy.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var groupsQuery = _context.VirtualKeyGroups.AsNoTracking();
        if (virtualKeyGroupId.HasValue)
        {
            groupsQuery = groupsQuery.Where(group => group.Id == virtualKeyGroupId.Value);
        }

        var groups = await groupsQuery
            .Select(group => new
            {
                group.Id,
                group.GroupName,
                group.MediaRetentionPolicyId
            })
            .ToListAsync(cancellationToken);

        var assignedPolicyIds = groups
            .Where(group => group.MediaRetentionPolicyId.HasValue)
            .Select(group => group.MediaRetentionPolicyId!.Value)
            .Distinct()
            .ToList();
        var assignedPolicies = await _context.MediaRetentionPolicies
            .AsNoTracking()
            .Where(policy => assignedPolicyIds.Contains(policy.Id) && policy.IsActive)
            .ToDictionaryAsync(policy => policy.Id, cancellationToken);

        var groupIds = groups.Select(group => group.Id).ToList();
        var usageRows = await _context.MediaRecords
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Join(
                _context.VirtualKeys.AsNoTracking(),
                media => media.VirtualKeyId,
                key => key.Id,
                (media, key) => new
                {
                    key.VirtualKeyGroupId,
                    SizeBytes = media.SizeBytes ?? 0
                })
            .Where(item => groupIds.Contains(item.VirtualKeyGroupId))
            .GroupBy(item => item.VirtualKeyGroupId)
            .Select(group => new
            {
                GroupId = group.Key,
                TotalFiles = group.Count(),
                TotalSizeBytes = group.Sum(item => item.SizeBytes)
            })
            .ToDictionaryAsync(row => row.GroupId, cancellationToken);

        return groups.Select(group =>
        {
            MediaRetentionPolicy? policy = null;
            if (group.MediaRetentionPolicyId.HasValue)
            {
                assignedPolicies.TryGetValue(group.MediaRetentionPolicyId.Value, out policy);
            }
            policy ??= defaultPolicy;
            usageRows.TryGetValue(group.Id, out var usage);

            return new MediaGroupQuotaUsage
            {
                VirtualKeyGroupId = group.Id,
                VirtualKeyGroupName = group.GroupName,
                MediaRetentionPolicyId = policy?.Id,
                MediaRetentionPolicyName = policy?.Name,
                TotalFiles = usage?.TotalFiles ?? 0,
                TotalSizeBytes = usage?.TotalSizeBytes ?? 0,
                MaxStorageSizeBytes = policy?.MaxStorageSizeBytes,
                MaxFileCount = policy?.MaxFileCount,
                QuotaExceededBehavior = policy?.QuotaExceededBehavior ??
                    MediaQuotaExceededBehavior.Reject,
                RespectRecentAccess = policy?.RespectRecentAccess ?? true,
                RecentAccessWindowDays = policy?.RecentAccessWindowDays ?? 7
            };
        }).ToList();
    }

    private async Task<MediaRetentionPolicy?> ResolvePolicyAsync(
        int? assignedPolicyId,
        CancellationToken cancellationToken)
    {
        if (assignedPolicyId.HasValue)
        {
            var assigned = await _context.MediaRetentionPolicies
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    policy => policy.Id == assignedPolicyId.Value && policy.IsActive,
                    cancellationToken);
            if (assigned != null)
                return assigned;
        }

        return await _context.MediaRetentionPolicies
            .AsNoTracking()
            .Where(policy => policy.IsDefault && policy.IsActive)
            .OrderBy(policy => policy.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<(int TotalFiles, long TotalSizeBytes)> QueryUsageAsync(
        int groupId,
        CancellationToken cancellationToken)
    {
        var row = await _context.MediaRecords
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Join(
                _context.VirtualKeys.AsNoTracking().Where(key => key.VirtualKeyGroupId == groupId),
                media => media.VirtualKeyId,
                key => key.Id,
                (media, _) => new { SizeBytes = media.SizeBytes ?? 0 })
            .GroupBy(_ => 1)
            .Select(group => new
            {
                TotalFiles = group.Count(),
                TotalSizeBytes = group.Sum(item => item.SizeBytes)
            })
            .SingleOrDefaultAsync(cancellationToken);

        return row == null
            ? (0, 0)
            : (row.TotalFiles, row.TotalSizeBytes);
    }

    private static string BuildQuotaExceededMessage(
        int groupId,
        long wouldUseBytes,
        int wouldUseFiles,
        MediaRetentionPolicy policy)
    {
        var limits = new List<string>();
        if (policy.MaxStorageSizeBytes.HasValue)
        {
            limits.Add(
                $"storage {wouldUseBytes:N0}/{policy.MaxStorageSizeBytes.Value:N0} bytes");
        }
        if (policy.MaxFileCount.HasValue)
        {
            limits.Add($"files {wouldUseFiles:N0}/{policy.MaxFileCount.Value:N0}");
        }

        return $"Media quota exceeded for virtual key group {groupId} " +
            $"({string.Join(", ", limits)}). Delete media or ask an administrator to raise the quota.";
    }
}

/// <summary>
/// Bridges singleton storage services to the scoped quota evaluator.
/// </summary>
public sealed class MediaQuotaGuard : IMediaQuotaGuard
{
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public MediaQuotaGuard(IServiceScopeFactory serviceScopeFactory)
    {
        _serviceScopeFactory = serviceScopeFactory;
    }

    public async Task EnsureCanStoreAsync(
        string? createdBy,
        long prospectiveSizeBytes,
        CancellationToken cancellationToken = default)
    {
        if (!int.TryParse(createdBy, out var virtualKeyId) || virtualKeyId <= 0)
            return;

        using var scope = _serviceScopeFactory.CreateScope();
        var quotaService = scope.ServiceProvider.GetRequiredService<IMediaQuotaService>();
        await quotaService.EnsureCanStoreAsync(
            virtualKeyId,
            prospectiveSizeBytes,
            cancellationToken);
    }
}
