using ConduitLLM.Configuration.DTOs.HealthMonitoring;
using ConduitLLM.Configuration.Messaging;
using ConduitLLM.Core.Events;
using ConduitLLM.Gateway.EventHandlers;
using ConduitLLM.Gateway.Interfaces;

using Microsoft.Extensions.Logging;

using Moq;

namespace ConduitLLM.Tests.Gateway.EventHandlers;

[Trait("Category", "Unit")]
[Trait("Component", "MediaLifecycle")]
public sealed class MediaCleanupAlertHandlerTests
{
    private readonly Mock<IAlertManagementService> _alerts = new();

    [Fact]
    public async Task HandleAsync_OperationFailure_PublishesHealthAlert()
    {
        var handler = CreateHandler();

        await handler.HandleAsync(
            new MediaCleanupAlertRaised
            {
                Kind = MediaCleanupAlertKind.OperationFailure,
                CleanupType = "retention",
                Status = "Completed with errors",
                TriggeredBy = "scheduled",
                LeaderInstanceId = "admin-1"
            },
            Mock.Of<IEventContext>());

        _alerts.Verify(alerts => alerts.TriggerAlertAsync(
            It.Is<HealthAlert>(alert =>
                alert.Component == "media-cleanup" &&
                alert.Severity == AlertSeverity.Error &&
                alert.Type == AlertType.ServiceDegraded &&
                alert.Context["cleanupType"].Equals("retention"))),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ExhaustedBudget_PublishesCriticalHealthAlert()
    {
        var handler = CreateHandler();

        await handler.HandleAsync(
            new MediaCleanupAlertRaised
            {
                Kind = MediaCleanupAlertKind.BudgetThreshold,
                CleanupType = "purge",
                Status = "Deletion budget exhausted",
                MonthlyDeleteCount = 500_000,
                MonthlyDeleteBudget = 500_000,
                BudgetUsedPercent = 100
            },
            Mock.Of<IEventContext>());

        _alerts.Verify(alerts => alerts.TriggerAlertAsync(
            It.Is<HealthAlert>(alert =>
                alert.Component == "media-cleanup" &&
                alert.Severity == AlertSeverity.Critical &&
                alert.Type == AlertType.ResourceExhaustion &&
                alert.Message.Contains("100%"))),
            Times.Once);
    }

    private MediaCleanupAlertHandler CreateHandler() => new(
        _alerts.Object,
        Mock.Of<ILogger<MediaCleanupAlertHandler>>());
}
