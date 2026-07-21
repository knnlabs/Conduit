using System.Diagnostics;

using ConduitLLM.Configuration.Messaging;
using ConduitLLM.Core.Metrics;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services;

/// <summary>Preserves the controller base's optional, measured fire-and-forget behavior.</summary>
public sealed class EventPublisher : IEventPublisher
{
    private readonly IEventBus? _eventBus;
    private readonly ILogger<EventPublisher> _logger;

    public EventPublisher(IServiceProvider services, ILogger<EventPublisher> logger)
    {
        _eventBus = services.GetService<IEventBus>();
        _logger = logger;
    }

    public bool IsEnabled => _eventBus is not null;

    public void PublishFireAndForget<TEvent>(
        TEvent domainEvent,
        string operationName,
        object? contextData = null) where TEvent : class
    {
        if (domainEvent is null)
        {
            _logger.LogWarning("Attempted to publish null {EventType} for {Operation}",
                typeof(TEvent).Name, operationName);
            return;
        }
        if (_eventBus is null)
        {
            _logger.LogWarning("Event publishing not configured - skipping {EventType} for {Operation}",
                typeof(TEvent).Name, operationName);
            EventPublishingMetrics.RecordSkipped(typeof(TEvent).Name);
            return;
        }

        _ = Task.Run(async () =>
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                await _eventBus.PublishAsync(domainEvent);
                stopwatch.Stop();
                _logger.LogDebug("Published {EventType} for {Operation} with context {ContextData}",
                    typeof(TEvent).Name, operationName, contextData);
                EventPublishingMetrics.RecordSuccess(typeof(TEvent).Name, stopwatch.Elapsed.TotalSeconds);
            }
            catch (Exception exception)
            {
                stopwatch.Stop();
                _logger.LogWarning(exception,
                    "Failed to publish {EventType} for {Operation} with context {ContextData}",
                    typeof(TEvent).Name, operationName, contextData);
                EventPublishingMetrics.RecordFailure(typeof(TEvent).Name);
            }
        });
    }
}
