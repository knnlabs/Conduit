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
    private readonly Mock<IOperationalAlertPublisher> _alerts = new();

    [Fact]
    public async Task HandleAsync_OperationFailure_RaisesErrorAlert()
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

        _alerts.Verify(alerts => alerts.Raise(
                OperationalAlertSeverity.Error,
                "media-cleanup",
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.Is<IReadOnlyDictionary<string, object>>(context =>
                    context["cleanupType"].Equals("retention"))),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ExhaustedBudget_RaisesCriticalAlert()
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

        _alerts.Verify(alerts => alerts.Raise(
                OperationalAlertSeverity.Critical,
                "media-cleanup",
                It.IsAny<string>(),
                It.Is<string>(message => message.Contains("100%")),
                It.IsAny<IReadOnlyDictionary<string, object>>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_BudgetBelowLimit_RaisesWarningRatherThanCritical()
    {
        var handler = CreateHandler();

        await handler.HandleAsync(
            new MediaCleanupAlertRaised
            {
                Kind = MediaCleanupAlertKind.BudgetThreshold,
                CleanupType = "purge",
                Status = "Deletion budget threshold reached",
                MonthlyDeleteCount = 400_000,
                MonthlyDeleteBudget = 500_000,
                BudgetUsedPercent = 80
            },
            Mock.Of<IEventContext>());

        _alerts.Verify(alerts => alerts.Raise(
                OperationalAlertSeverity.Warning,
                "media-cleanup",
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, object>>()),
            Times.Once);
    }

    private MediaCleanupAlertHandler CreateHandler() => new(
        _alerts.Object,
        Mock.Of<ILogger<MediaCleanupAlertHandler>>());
}
