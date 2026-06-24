using System;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Messaging;
using ConduitLLM.Configuration.Messaging.MassTransit;

using FluentAssertions;

using MassTransit;

using Moq;

using Xunit;

namespace ConduitLLM.Tests.Messaging
{
    /// <summary>
    /// Unit tests for <see cref="MassTransitEventBus"/> — the Phase 1 publish adapter (#915).
    /// </summary>
    public class MassTransitEventBusTests
    {
        public record SampleEvent(string Name);

        [Fact]
        public async Task PublishAsync_DelegatesToPublishEndpoint_WithSameInstanceAndToken()
        {
            var publish = new Mock<IPublishEndpoint>();
            var bus = new MassTransitEventBus(publish.Object);
            var evt = new SampleEvent("hello");
            using var cts = new CancellationTokenSource();

            await bus.PublishAsync(evt, cts.Token);

            // Routing is by the closed generic type, exactly as the old direct calls did.
            publish.Verify(p => p.Publish(evt, cts.Token), Times.Once);
            publish.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task PublishAsync_DefaultToken_PassesDefault()
        {
            var publish = new Mock<IPublishEndpoint>();
            var bus = new MassTransitEventBus(publish.Object);
            var evt = new SampleEvent("x");

            await bus.PublishAsync(evt);

            publish.Verify(p => p.Publish(evt, default(CancellationToken)), Times.Once);
        }

        [Fact]
        public async Task PublishAsync_NullEvent_Throws()
        {
            var bus = new MassTransitEventBus(Mock.Of<IPublishEndpoint>());

            await Assert.ThrowsAsync<ArgumentNullException>(
                () => bus.PublishAsync<SampleEvent>(null!));
        }

        [Fact]
        public void Ctor_NullEndpoint_Throws()
        {
            Action act = () => new MassTransitEventBus(null!);

            act.Should().Throw<ArgumentNullException>();
        }
    }
}
