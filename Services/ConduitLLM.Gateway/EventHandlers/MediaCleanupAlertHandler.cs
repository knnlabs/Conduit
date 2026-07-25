using ConduitLLM.Configuration.Messaging;
using ConduitLLM.Core.Events;
using ConduitLLM.Gateway.Interfaces;

namespace ConduitLLM.Gateway.EventHandlers;

/// <summary>
/// Records Admin media-cleanup events as operational alerts.
/// </summary>
public sealed class MediaCleanupAlertHandler : IEventHandler<MediaCleanupAlertRaised>
{
    private readonly IOperationalAlertPublisher _alertPublisher;
    private readonly ILogger<MediaCleanupAlertHandler> _logger;

    public MediaCleanupAlertHandler(
        IOperationalAlertPublisher alertPublisher,
        ILogger<MediaCleanupAlertHandler> logger)
    {
        _alertPublisher = alertPublisher;
        _logger = logger;
    }

    public Task HandleAsync(
        MediaCleanupAlertRaised message,
        IEventContext context)
    {
        var isBudgetAlert = message.Kind == MediaCleanupAlertKind.BudgetThreshold;
        var budgetExhausted = message.BudgetUsedPercent >= 100;

        _alertPublisher.Raise(
            severity: isBudgetAlert
                ? budgetExhausted ? OperationalAlertSeverity.Critical : OperationalAlertSeverity.Warning
                : OperationalAlertSeverity.Error,
            component: "media-cleanup",
            title: isBudgetAlert
                ? "Media cleanup deletion budget threshold reached"
                : "Media cleanup operation failed",
            message: isBudgetAlert
                ? $"Monthly media deletion budget is {message.BudgetUsedPercent:0.##}% used " +
                  $"({message.MonthlyDeleteCount:N0}/{message.MonthlyDeleteBudget:N0})."
                : $"{message.CleanupType} cleanup reported: {message.Status}.",
            context: new Dictionary<string, object>
            {
                ["cleanupType"] = message.CleanupType,
                ["triggeredBy"] = message.TriggeredBy,
                ["leaderInstanceId"] = message.LeaderInstanceId,
                ["status"] = message.Status
            });

        _logger.LogWarning(
            "Published {AlertKind} operational alert for {CleanupType} media cleanup",
            message.Kind,
            message.CleanupType);

        return Task.CompletedTask;
    }
}
