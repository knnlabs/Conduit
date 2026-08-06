using ConduitLLM.Configuration.Messaging;
using ConduitLLM.Core.Events;
using ConduitLLM.Gateway.Interfaces;

namespace ConduitLLM.Gateway.EventHandlers;

/// <summary>
/// Converts a depleted virtual-key group balance into an actionable operational alert.
/// </summary>
public sealed class SpendThresholdAlertHandler : IEventHandler<SpendThresholdExceeded>
{
    private readonly IOperationalAlertPublisher _alertPublisher;
    private readonly ILogger<SpendThresholdAlertHandler> _logger;

    public SpendThresholdAlertHandler(
        IOperationalAlertPublisher alertPublisher,
        ILogger<SpendThresholdAlertHandler> logger)
    {
        _alertPublisher = alertPublisher;
        _logger = logger;
    }

    public Task HandleAsync(SpendThresholdExceeded message, IEventContext context)
    {
        _alertPublisher.Raise(
            OperationalAlertSeverity.Critical,
            component: "billing",
            title: "Virtual key group balance depleted",
            message:
                $"Usage by virtual key {message.VirtualKeyId} ({message.KeyName}) depleted its group balance " +
                $"and exceeded the available amount by {message.AmountOver:C}.",
            context: new Dictionary<string, object>
            {
                ["virtualKeyId"] = message.VirtualKeyId,
                ["keyName"] = message.KeyName,
                ["currentSpend"] = message.CurrentSpend,
                ["availableBalance"] = message.MaxBudget,
                ["amountOver"] = message.AmountOver,
                ["exceededAt"] = message.ExceededAt,
                ["keyDisabled"] = message.KeyDisabled
            });

        _logger.LogWarning(
            "Published depleted balance alert for virtual key {VirtualKeyId} ({KeyName})",
            message.VirtualKeyId,
            message.KeyName);

        return Task.CompletedTask;
    }
}
