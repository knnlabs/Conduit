using MassTransit;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Consumers;

/// <summary>
/// Base class for simple cache invalidation consumers that follow the pattern:
/// log received → invalidate cache → log success/failure → rethrow on failure.
/// </summary>
/// <typeparam name="TEvent">The MassTransit event type to consume.</typeparam>
/// <remarks>
/// Consumers with more complex logic (multiple caches, nullable caches, multi-event handling,
/// error swallowing) should continue to implement <see cref="IConsumer{TMessage}"/> directly.
/// </remarks>
public abstract class CacheInvalidationConsumerBase<TEvent> : IConsumer<TEvent>
    where TEvent : class
{
    protected readonly ILogger Logger;

    protected CacheInvalidationConsumerBase(ILogger logger)
    {
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Consume(ConsumeContext<TEvent> context)
    {
        var message = context.Message;
        LogReceived(message);

        try
        {
            await InvalidateCacheAsync(message);
            LogSuccess(message);
        }
        catch (Exception ex)
        {
            LogFailure(message, ex);
            throw; // Always rethrow for MassTransit retry policy
        }
    }

    /// <summary>
    /// Performs the actual cache invalidation for the given event message.
    /// </summary>
    protected abstract Task InvalidateCacheAsync(TEvent message);

    /// <summary>
    /// Logs that the event was received.
    /// </summary>
    protected abstract void LogReceived(TEvent message);

    /// <summary>
    /// Logs that cache invalidation succeeded.
    /// </summary>
    protected abstract void LogSuccess(TEvent message);

    /// <summary>
    /// Logs that cache invalidation failed.
    /// </summary>
    protected abstract void LogFailure(TEvent message, Exception ex);
}
