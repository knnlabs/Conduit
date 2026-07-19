using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Messaging;
using ConduitLLM.Configuration.Messaging.MassTransit;

using FluentAssertions;

using MassTransit;
using MassTransit.Testing;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace ConduitLLM.Tests.Messaging
{
    /// <summary>
    /// Pilot end-to-end test for the MassTransit consumer bridge (#916): a handler written
    /// purely against <see cref="IEventHandler{TEvent}"/> / <see cref="IEventContext"/> runs
    /// when its event is published, dispatched through
    /// <see cref="MassTransitConsumerBridge{TEvent}"/> under the in-memory test harness.
    /// </summary>
    public class MassTransitConsumerBridgeTests
    {
        public record PilotEvent(string Payload);

        public sealed class PilotSink
        {
            public List<PilotEvent> Received { get; } = new();
            public Guid? LastMessageId { get; set; }
            public CancellationToken LastToken { get; set; }
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
                _sink.LastToken = context.CancellationToken;
                _sink.Signal.TrySetResult(@event);
                return Task.CompletedTask;
            }
        }

        [Fact]
        public async Task PilotHandler_RunsEndToEnd_ViaBridge()
        {
            var sink = new PilotSink();

            await using var provider = new ServiceCollection()
                .AddSingleton(sink)
                .AddScoped<IEventHandler<PilotEvent>, RecordingPilotHandler>()
                .AddMassTransitTestHarness(x =>
                {
                    x.AddEventBridge<PilotEvent>();
                })
                .BuildServiceProvider(true);

            var harness = provider.GetRequiredService<ITestHarness>();
            await harness.Start();
            try
            {
                await harness.Bus.Publish(new PilotEvent("hello"));

                // The bridge consumed the event...
                (await harness.Consumed.Any<PilotEvent>()).Should().BeTrue();
                var bridgeHarness = harness.GetConsumerHarness<MassTransitConsumerBridge<PilotEvent>>();
                (await bridgeHarness.Consumed.Any<PilotEvent>()).Should().BeTrue();

                // ...and the IEventHandler actually ran with a usable context.
                var received = await sink.Signal.Task.WaitAsync(TimeSpan.FromSeconds(10));
                received.Payload.Should().Be("hello");
                sink.Received.Should().ContainSingle();
                sink.LastMessageId.Should().NotBeNull();
            }
            finally
            {
                await harness.Stop();
            }
        }
    }
}
