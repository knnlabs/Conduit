using ConduitLLM.Configuration.Messaging;
using ConduitLLM.Core.Events;
using ConduitLLM.Gateway.EventHandlers;
using ConduitLLM.Gateway.Interfaces;

using Microsoft.Extensions.Logging;

using Moq;

namespace ConduitLLM.Tests.Gateway.EventHandlers;

[Trait("Category", "Unit")]
[Trait("Component", "Billing")]
public sealed class SpendThresholdAlertHandlerTests
{
    [Fact]
    public async Task HandleAsync_RaisesCriticalBillingAlert()
    {
        var alerts = new Mock<IOperationalAlertPublisher>();
        var handler = new SpendThresholdAlertHandler(
            alerts.Object,
            Mock.Of<ILogger<SpendThresholdAlertHandler>>());

        await handler.HandleAsync(
            new SpendThresholdExceeded
            {
                VirtualKeyId = 42,
                KeyName = "production",
                CurrentSpend = 125m,
                MaxBudget = 100m,
                AmountOver = 25m,
                ExceededAt = DateTime.UtcNow
            },
            Mock.Of<IEventContext>());

        alerts.Verify(publisher => publisher.Raise(
                OperationalAlertSeverity.Critical,
                "billing",
                "Virtual key group balance depleted",
                It.Is<string>(message =>
                    message.Contains("production") &&
                    message.Contains("25")),
                It.Is<IReadOnlyDictionary<string, object>>(alertContext =>
                    alertContext["virtualKeyId"].Equals(42) &&
                    alertContext["amountOver"].Equals(25m))),
            Times.Once);
    }
}
