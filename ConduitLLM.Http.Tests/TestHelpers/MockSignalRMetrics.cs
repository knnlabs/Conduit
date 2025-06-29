using System;
using System.Diagnostics.Metrics;
using ConduitLLM.Http.Metrics;
using Moq;

namespace ConduitLLM.Http.Tests.TestHelpers
{
    /// <summary>
    /// Helper class to create mock SignalR metrics for testing.
    /// </summary>
    public static class MockSignalRMetrics
    {
        /// <summary>
        /// Creates a mock ISignalRMetrics with all properties properly set up.
        /// </summary>
        public static Mock<ISignalRMetrics> Create()
        {
            var mockMetrics = new Mock<ISignalRMetrics>();
            
            // Create a test meter for metrics
            using var meter = new Meter("test", "1.0.0");
            
            // Set up all counter properties
            mockMetrics.Setup(m => m.ConnectionsTotal).Returns(meter.CreateCounter<long>("test_connections_total"));
            mockMetrics.Setup(m => m.ActiveConnections).Returns(meter.CreateUpDownCounter<long>("test_active_connections"));
            mockMetrics.Setup(m => m.AuthenticationFailures).Returns(meter.CreateCounter<long>("test_auth_failures"));
            mockMetrics.Setup(m => m.ConnectionErrors).Returns(meter.CreateCounter<long>("test_connection_errors"));
            
            mockMetrics.Setup(m => m.MessagesSent).Returns(meter.CreateCounter<long>("test_messages_sent"));
            mockMetrics.Setup(m => m.MessagesReceived).Returns(meter.CreateCounter<long>("test_messages_received"));
            mockMetrics.Setup(m => m.MessageProcessingDuration).Returns(meter.CreateHistogram<double>("test_message_duration"));
            mockMetrics.Setup(m => m.MessageErrors).Returns(meter.CreateCounter<long>("test_message_errors"));
            
            mockMetrics.Setup(m => m.HubMethodInvocations).Returns(meter.CreateCounter<long>("test_hub_invocations"));
            mockMetrics.Setup(m => m.HubMethodDuration).Returns(meter.CreateHistogram<double>("test_hub_duration"));
            mockMetrics.Setup(m => m.HubErrors).Returns(meter.CreateCounter<long>("test_hub_errors"));
            
            mockMetrics.Setup(m => m.ReconnectionAttempts).Returns(meter.CreateCounter<long>("test_reconnection_attempts"));
            mockMetrics.Setup(m => m.ReconnectionSuccesses).Returns(meter.CreateCounter<long>("test_reconnection_successes"));
            mockMetrics.Setup(m => m.ReconnectionFailures).Returns(meter.CreateCounter<long>("test_reconnection_failures"));
            
            mockMetrics.Setup(m => m.GroupJoins).Returns(meter.CreateCounter<long>("test_group_joins"));
            mockMetrics.Setup(m => m.GroupLeaves).Returns(meter.CreateCounter<long>("test_group_leaves"));
            mockMetrics.Setup(m => m.ActiveGroups).Returns(meter.CreateUpDownCounter<long>("test_active_groups"));
            
            // Set up method behaviors
            mockMetrics.Setup(m => m.RecordHubMethodInvocation(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>()))
                .Returns(Mock.Of<IDisposable>());
                
            mockMetrics.Setup(m => m.RecordMessageProcessing(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Mock.Of<IDisposable>());
            
            return mockMetrics;
        }
    }
}