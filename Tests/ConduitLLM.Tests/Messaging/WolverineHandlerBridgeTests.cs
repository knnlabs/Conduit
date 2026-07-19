using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Messaging;
using ConduitLLM.Configuration.Messaging.Wolverine;

using FluentAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using Wolverine;

using Xunit;

namespace ConduitLLM.Tests.Messaging
{
    /// <summary>
    /// Unit tests for <see cref="WolverineHandlerBridge{TEvent}"/> and the Wolverine-backed
    /// <c>IEventContext</c> it hands to handlers (#925): sequential dispatch to every handler,
    /// exception propagation, and
    /// a context that surfaces envelope metadata and cascading/scheduled publishes.
    /// </summary>
    public class WolverineHandlerBridgeTests
    {
        public record TestEvent(string Payload);
        public record FollowOnEvent(string Payload);

        private sealed class RecordingHandler : IEventHandler<TestEvent>
        {
            public List<TestEvent> Received { get; } = new();
            public IEventContext? LastContext { get; private set; }

            public Task HandleAsync(TestEvent @event, IEventContext context)
            {
                Received.Add(@event);
                LastContext = context;
                return Task.CompletedTask;
            }
        }

        private sealed class ThrowingHandler : IEventHandler<TestEvent>
        {
            public Task HandleAsync(TestEvent @event, IEventContext context)
                => throw new InvalidOperationException("handler failed");
        }

        private static WolverineHandlerBridge<TestEvent> CreateBridge(params IEventHandler<TestEvent>[] handlers)
            => new(handlers, NullLogger<WolverineHandlerBridge<TestEvent>>.Instance);

        [Fact]
        public async Task Handle_DispatchesToAllHandlers()
        {
            var first = new RecordingHandler();
            var second = new RecordingHandler();
            var bridge = CreateBridge(first, second);
            var @event = new TestEvent("hello");

            await bridge.Handle(@event, Mock.Of<IMessageContext>(), CancellationToken.None);

            first.Received.Should().ContainSingle().Which.Should().Be(@event);
            second.Received.Should().ContainSingle().Which.Should().Be(@event);
        }

        [Fact]
        public async Task Handle_HandlerException_PropagatesToWolverine()
        {
            var bridge = CreateBridge(new ThrowingHandler());

            var act = () => bridge.Handle(new TestEvent("boom"), Mock.Of<IMessageContext>(), CancellationToken.None);

            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("handler failed");
        }

        [Fact]
        public async Task Context_SurfacesEnvelopeMetadataAndToken()
        {
            var envelope = new Envelope { CorrelationId = "corr-1" };
            envelope.Headers["x-conduit-test"] = "header-value";
            var contextMock = new Mock<IMessageContext>();
            contextMock.SetupGet(c => c.Envelope).Returns(envelope);
            var handler = new RecordingHandler();
            var bridge = CreateBridge(handler);
            using var cts = new CancellationTokenSource();

            await bridge.Handle(new TestEvent("hello"), contextMock.Object, cts.Token);

            var context = handler.LastContext!;
            context.MessageId.Should().Be(envelope.Id);
            context.CorrelationId.Should().Be("corr-1");
            context.CancellationToken.Should().Be(cts.Token);
            context.TryGetHeader("x-conduit-test", out var value).Should().BeTrue();
            value.Should().Be("header-value");
            context.TryGetHeader("missing", out var missing).Should().BeFalse();
            missing.Should().BeNull();
        }

        [Fact]
        public async Task Context_PublishAsync_CascadesThroughMessageContext()
        {
            var contextMock = new Mock<IMessageContext>();
            var handler = new RecordingHandler();
            var bridge = CreateBridge(handler);

            await bridge.Handle(new TestEvent("hello"), contextMock.Object, CancellationToken.None);
            var followOn = new FollowOnEvent("next");
            await handler.LastContext!.PublishAsync(followOn);

            contextMock.Verify(c => c.PublishAsync(followOn, null), Times.Once);
        }

        [Fact]
        public async Task Context_SchedulePublishAsync_UsesWolverineScheduling()
        {
            var contextMock = new Mock<IMessageContext>();
            var handler = new RecordingHandler();
            var bridge = CreateBridge(handler);
            var deliveryTime = new DateTime(2026, 7, 16, 12, 0, 0, DateTimeKind.Utc);

            await bridge.Handle(new TestEvent("hello"), contextMock.Object, CancellationToken.None);
            var retry = new FollowOnEvent("retry");
            await handler.LastContext!.SchedulePublishAsync(deliveryTime, retry);

            contextMock.Verify(
                c => c.PublishAsync(retry, It.Is<DeliveryOptions>(o => o.ScheduledTime == new DateTimeOffset(deliveryTime))),
                Times.Once);
        }

        [Fact]
        public async Task Context_SchedulePublishAsync_TreatsUnspecifiedKindAsUtc()
        {
            var contextMock = new Mock<IMessageContext>();
            var handler = new RecordingHandler();
            var bridge = CreateBridge(handler);
            var unspecified = new DateTime(2026, 7, 16, 12, 0, 0, DateTimeKind.Unspecified);

            await bridge.Handle(new TestEvent("hello"), contextMock.Object, CancellationToken.None);
            var retry = new FollowOnEvent("retry");
            await handler.LastContext!.SchedulePublishAsync(unspecified, retry);

            contextMock.Verify(
                c => c.PublishAsync(retry, It.Is<DeliveryOptions>(o => o.ScheduledTime == new DateTimeOffset(unspecified, TimeSpan.Zero))),
                Times.Once);
        }
    }
}
