using ConduitLLM.Configuration.Messaging;
using ConduitLLM.Core.Services;

using Microsoft.Extensions.DependencyInjection;

namespace ConduitLLM.Tests.Core.Services;

public sealed class EventPublisherTests
{
    [Fact]
    public async Task SingletonPublisher_ResolvesEventBusInsidePublishScope()
    {
        var published = new TaskCompletionSource<object>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(published);
        services.AddScoped<IEventBus, CapturingEventBus>();
        services.AddSingleton<IEventPublisher, EventPublisher>();
        using var provider = services.BuildServiceProvider(validateScopes: true);
        var publisher = provider.GetRequiredService<IEventPublisher>();
        var domainEvent = new object();

        publisher.PublishFireAndForget(domainEvent, "test");

        Assert.Same(
            domainEvent,
            await published.Task.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    private sealed class CapturingEventBus(
        TaskCompletionSource<object> published) : IEventBus
    {
        public Task PublishAsync<TEvent>(
            TEvent @event,
            CancellationToken cancellationToken = default)
            where TEvent : class
        {
            published.TrySetResult(@event);
            return Task.CompletedTask;
        }

        public Task PublishBatchAsync<TEvent>(
            IEnumerable<TEvent> events,
            CancellationToken cancellationToken = default)
            where TEvent : class =>
            Task.WhenAll(events.Select(item => PublishAsync(item, cancellationToken)));
    }
}
