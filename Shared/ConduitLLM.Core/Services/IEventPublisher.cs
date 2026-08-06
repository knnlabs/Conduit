namespace ConduitLLM.Core.Services;

/// <summary>Request-handler-friendly event publishing abstraction.</summary>
public interface IEventPublisher
{
    bool IsEnabled { get; }
    void PublishFireAndForget<TEvent>(TEvent domainEvent, string operationName, object? contextData = null)
        where TEvent : class;
}
