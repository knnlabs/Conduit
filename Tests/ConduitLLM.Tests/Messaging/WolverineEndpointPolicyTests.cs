using System;
using System.Linq;

using ConduitLLM.Configuration.Messaging;
using ConduitLLM.Configuration.Messaging.Wolverine;
using ConduitLLM.Core.Messaging;

using AwesomeAssertions;

using Xunit;

namespace ConduitLLM.Tests.Messaging
{
    /// <summary>
    /// Tests for the Wolverine translation of the tuned endpoint descriptors (#926):
    /// the retry-cooldown expansion and the invariants of the shared event→queue topology.
    /// </summary>
    public class WolverineEndpointPolicyTests
    {
        [Fact]
        public void ComputeRetryCooldowns_Immediate_AllZero()
        {
            var cooldowns = WolverineEndpointPolicy.ComputeRetryCooldowns(RetryPolicy.Immediate(3));

            cooldowns.Should().Equal(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero);
        }

        [Fact]
        public void ComputeRetryCooldowns_Incremental_LinearBackoff()
        {
            // video-generation-events: Incremental(3, 2s, 5s)
            var cooldowns = WolverineEndpointPolicy.ComputeRetryCooldowns(
                ConduitEndpointPolicies.VideoGeneration.Retry!);

            cooldowns.Should().Equal(
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(7),
                TimeSpan.FromSeconds(12));
        }

        [Fact]
        public void ComputeRetryCooldowns_Exponential_DoublingCappedAtMax()
        {
            var cooldowns = WolverineEndpointPolicy.ComputeRetryCooldowns(
                RetryPolicy.Exponential(3, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(2)));

            cooldowns.Should().Equal(
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(3),
                TimeSpan.FromSeconds(7));
        }

        [Fact]
        public void ComputeRetryCooldowns_Exponential_CapsAtMaxInterval()
        {
            var cooldowns = WolverineEndpointPolicy.ComputeRetryCooldowns(
                RetryPolicy.Exponential(5, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(2)));

            cooldowns.Should().Equal(
                TimeSpan.FromSeconds(1),   // 1 + 2·(2⁰·2−2)/... = 1 + 2·1−2 → 1
                TimeSpan.FromSeconds(3),   // 1 + 2·(2¹−1) = 3
                TimeSpan.FromSeconds(7),   // 1 + 2·(2²−1) = 7
                TimeSpan.FromSeconds(10),  // 1 + 2·(2³−1) = 15 → capped 10
                TimeSpan.FromSeconds(10)); // capped
        }

        [Fact]
        public void Topology_TunedAndDefaultQueues_CoverEveryGatewayBridgedEventExactlyOnce()
        {
            var routed = ConduitMessagingTopology.WebhookDeliveryEvents
                .Concat(ConduitMessagingTopology.SpendUpdateEvents)
                .Concat(ConduitMessagingTopology.VideoGenerationEvents)
                .Concat(ConduitMessagingTopology.ImageGenerationEvents)
                .Concat(ConduitMessagingTopology.GatewayEvents)
                .ToList();

            // No event type is routed to two Gateway queues (shared events fan out to the
            // admin queue as well, which is intentional and not covered by this list).
            routed.Should().OnlyHaveUniqueItems();

            // Every event type the Gateway bridges is routed to exactly one Gateway queue.
            var bridged = ConduitLLM.Gateway.Extensions.CacheInvalidationMessagingExtensions.BridgedEventTypes
                .Concat(ConduitLLM.Gateway.Extensions.MediaGenerationMessagingExtensions.BridgedEventTypes)
                .Append(typeof(ConduitLLM.Core.Events.SpendUpdateRequested))
                .Append(typeof(ConduitLLM.Core.Events.WebhookDeliveryRequested))
                .Append(typeof(ConduitLLM.Configuration.Events.BatchSpendFlushRequestedEvent))
                .Distinct()
                .ToList();

            routed.Should().BeEquivalentTo(bridged);
        }

        [Fact]
        public void Topology_SharedEvents_AreTheSharedBridgeList()
        {
            ConduitMessagingTopology.SharedEvents.Should().BeEquivalentTo(
                ConduitLLM.Core.Extensions.SharedCacheInvalidationMessagingExtensions.BridgedEventTypes);

            // Shared events ride the per-service queues, not the tuned ones.
            ConduitMessagingTopology.GatewayEvents.Should().NotContain(ConduitMessagingTopology.SharedEvents);
        }

        [Fact]
        public void Topology_StrictOrderingEndpoints_MatchTheRabbitMqSemantics()
        {
            // The two endpoints that relied on RabbitMQ single-active-consumer must map
            // to Wolverine strict ordering; webhook/video must not.
            ConduitEndpointPolicies.SpendUpdate.SingleActiveConsumer.Should().BeTrue();
            ConduitEndpointPolicies.ImageGeneration.SingleActiveConsumer.Should().BeTrue();
            ConduitEndpointPolicies.WebhookDelivery.SingleActiveConsumer.Should().BeFalse();
            ConduitEndpointPolicies.VideoGeneration.SingleActiveConsumer.Should().BeFalse();
        }
    }
}
