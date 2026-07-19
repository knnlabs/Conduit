using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Messaging;
using ConduitLLM.Configuration.Messaging.Wolverine;

using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Wolverine;

using Xunit;

namespace ConduitLLM.Tests.Messaging
{
    /// <summary>
    /// Pilot end-to-end test for the Wolverine backend (#925): a handler written purely against
    /// <see cref="IEventHandler{TEvent}"/> / <see cref="IEventContext"/> runs when its
    /// event is published through the Wolverine-backed <see cref="IEventBus"/>, dispatched
    /// via <see cref="WolverineHandlerBridge{TEvent}"/> on in-memory local queues (no
    /// Postgres required; conventional discovery disabled, exactly as in production).
    /// </summary>
    public class WolverineBridgePilotTests
    {
        public record PilotEvent(string Payload);

        public sealed class PilotSink
        {
            public List<PilotEvent> Received { get; } = new();
            public Guid? LastMessageId { get; set; }
            public string? LastCorrelationId { get; set; }
            public TaskCompletionSource<PilotEvent> Signal { get; } =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public sealed class RecordingPilotHandler : IEventHandler<PilotEvent>
        {
            private readonly PilotSink _sink;
            public RecordingPilotHandler(PilotSink sink) => _sink = sink;

            public Task HandleAsync(PilotEvent @event, IEventContext context)
            {
                _sink.Received.Add(@event);
                _sink.LastMessageId = context.MessageId;
                _sink.LastCorrelationId = context.CorrelationId;
                _sink.Signal.TrySetResult(@event);
                return Task.CompletedTask;
            }
        }

        [Fact]
        public async Task PilotHandler_RunsEndToEnd_ViaWolverineBridge()
        {
            var sink = new PilotSink();

            using var host = await Host.CreateDefaultBuilder()
                .UseWolverine(opts =>
                {
                    // Production parity: no conventional discovery; the bridge is the
                    // only handler registration (WolverineMessagingExtensions does the
                    // same, minus the Postgres transport this test does not need).
                    opts.Discovery.DisableConventionalDiscovery();
                    opts.UseRuntimeCompilation();
                    opts.AddEventBridge<PilotEvent>();

                    opts.Services.AddSingleton(sink);
                    opts.Services.AddScoped<IEventHandler<PilotEvent>, RecordingPilotHandler>();
                    opts.Services.AddWolverineEventBus();
                })
                .StartAsync();

            try
            {
                using (var scope = host.Services.CreateScope())
                {
                    var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();
                    eventBus.Should().BeOfType<WolverineEventBus>();

                    await eventBus.PublishAsync(new PilotEvent("hello"));
                }

                // The bridge dispatched the event to the IEventHandler with a usable context.
                var received = await sink.Signal.Task.WaitAsync(TimeSpan.FromSeconds(10));
                received.Payload.Should().Be("hello");
                sink.Received.Should().ContainSingle();
                sink.LastMessageId.Should().NotBeNull();
                sink.LastCorrelationId.Should().NotBeNullOrEmpty();
            }
            finally
            {
                await host.StopAsync();
            }
        }
    }
}
