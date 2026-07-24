using ConduitLLM.Configuration.DTOs.HealthMonitoring;
using ConduitLLM.Configuration.Messaging;
using ConduitLLM.Core.Events;
using ConduitLLM.Gateway.Interfaces;

namespace ConduitLLM.Gateway.EventHandlers;

/// <summary>
/// Routes Admin media-cleanup events into the existing health-monitoring alert and
/// SignalR notification pipeline.
/// </summary>
public sealed class MediaCleanupAlertHandler : IEventHandler<MediaCleanupAlertRaised>
{
    private readonly IAlertManagementService _alertManagementService;
    private readonly ILogger<MediaCleanupAlertHandler> _logger;

    public MediaCleanupAlertHandler(
        IAlertManagementService alertManagementService,
        ILogger<MediaCleanupAlertHandler> logger)
    {
        _alertManagementService = alertManagementService;
        _logger = logger;
    }

    public async Task HandleAsync(
        MediaCleanupAlertRaised message,
        IEventContext context)
    {
        var isBudgetAlert = message.Kind == MediaCleanupAlertKind.BudgetThreshold;
        var budgetExhausted = message.BudgetUsedPercent >= 100;
        var alert = new HealthAlert
        {
            Severity = isBudgetAlert
                ? budgetExhausted ? AlertSeverity.Critical : AlertSeverity.Warning
                : AlertSeverity.Error,
            Type = isBudgetAlert
                ? AlertType.ResourceExhaustion
                : AlertType.ServiceDegraded,
            Component = "media-cleanup",
            Title = isBudgetAlert
                ? "Media cleanup deletion budget threshold reached"
                : "Media cleanup operation failed",
            Message = isBudgetAlert
                ? $"Monthly media deletion budget is {message.BudgetUsedPercent:0.##}% used " +
                  $"({message.MonthlyDeleteCount:N0}/{message.MonthlyDeleteBudget:N0})."
                : $"{message.CleanupType} cleanup reported: {message.Status}.",
            TriggeredAt = message.Timestamp,
            LastUpdated = message.Timestamp,
            Fingerprint = isBudgetAlert
                ? "media-cleanup:budget-threshold"
                : $"media-cleanup:failure:{message.CleanupType}",
            Context = new Dictionary<string, object>
            {
                ["cleanupType"] = message.CleanupType,
                ["triggeredBy"] = message.TriggeredBy,
                ["leaderInstanceId"] = message.LeaderInstanceId,
                ["status"] = message.Status
            },
            SuggestedActions = isBudgetAlert
                ?
                [
                    "Review deletion volume and the configured monthly budget.",
                    "Pause cleanup or increase the limit only after checking provider pricing."
                ]
                :
                [
                    "Review the media cleanup status page and Admin logs.",
                    "Resolve the failing storage, database, or budget dependency before retrying."
                ]
        };

        await _alertManagementService.TriggerAlertAsync(alert);
        _logger.LogWarning(
            "Published {AlertKind} health alert for {CleanupType} media cleanup",
            message.Kind,
            message.CleanupType);
    }
}
