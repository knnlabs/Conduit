using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Messaging.Wolverine;

using FluentAssertions;

using Moq;

using Wolverine;

using Xunit;

namespace ConduitLLM.Tests.Messaging
{
    /// <summary>
    /// Unit tests for the Wolverine <c>IEventBus</c> adapter (#925).
    /// </summary>
    public class WolverineEventBusTests
    {
        public record TestEvent(string Payload);

        private readonly Mock<IMessageBus> _busMock = new();

        [Fact]
        public void Constructor_NullBus_Throws()
        {
            var act = () => new WolverineEventBus(null!);

            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public async Task PublishAsync_DelegatesToWolverineBus()
        {
            var bus = new WolverineEventBus(_busMock.Object);
            var @event = new TestEvent("hello");

            await bus.PublishAsync(@event);

            _busMock.Verify(b => b.PublishAsync(@event, null), Times.Once);
        }

        [Fact]
        public async Task PublishAsync_NullEvent_Throws()
        {
            var bus = new WolverineEventBus(_busMock.Object);

            var act = () => bus.PublishAsync<TestEvent>(null!);

            await act.Should().ThrowAsync<ArgumentNullException>();
        }

        [Fact]
        public async Task PublishAsync_CanceledToken_Throws()
        {
            var bus = new WolverineEventBus(_busMock.Object);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var act = () => bus.PublishAsync(new TestEvent("hello"), cts.Token);

            await act.Should().ThrowAsync<OperationCanceledException>();
            _busMock.Verify(b => b.PublishAsync(It.IsAny<TestEvent>(), null), Times.Never);
        }

        [Fact]
        public async Task PublishBatchAsync_PublishesEachEvent()
        {
            var bus = new WolverineEventBus(_busMock.Object);
            var events = new List<TestEvent> { new("one"), new("two"), new("three") };

            await bus.PublishBatchAsync(events);

            foreach (var @event in events)
            {
                _busMock.Verify(b => b.PublishAsync(@event, null), Times.Once);
            }
        }

        [Fact]
        public async Task PublishBatchAsync_NullEvents_Throws()
        {
            var bus = new WolverineEventBus(_busMock.Object);

            var act = () => bus.PublishBatchAsync<TestEvent>(null!);

            await act.Should().ThrowAsync<ArgumentNullException>();
        }
    }
}
