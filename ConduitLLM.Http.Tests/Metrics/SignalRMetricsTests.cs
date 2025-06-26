using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Xunit;
using ConduitLLM.Http.Metrics;

namespace ConduitLLM.Http.Tests.Metrics
{
    public class SignalRMetricsTests : IDisposable
    {
        private readonly SignalRMetrics _metrics;

        public SignalRMetricsTests()
        {
            _metrics = new SignalRMetrics("TestSignalRMetrics");
        }

        [Fact]
        public void Constructor_InitializesAllMetrics()
        {
            // Assert
            Assert.NotNull(_metrics.ConnectionsTotal);
            Assert.NotNull(_metrics.ActiveConnections);
            Assert.NotNull(_metrics.AuthenticationFailures);
            Assert.NotNull(_metrics.ConnectionErrors);
            Assert.NotNull(_metrics.MessagesSent);
            Assert.NotNull(_metrics.MessagesReceived);
            Assert.NotNull(_metrics.MessageProcessingDuration);
            Assert.NotNull(_metrics.MessageErrors);
            Assert.NotNull(_metrics.HubMethodInvocations);
            Assert.NotNull(_metrics.HubMethodDuration);
            Assert.NotNull(_metrics.HubErrors);
            Assert.NotNull(_metrics.ReconnectionAttempts);
            Assert.NotNull(_metrics.ReconnectionSuccesses);
            Assert.NotNull(_metrics.ReconnectionFailures);
            Assert.NotNull(_metrics.GroupJoins);
            Assert.NotNull(_metrics.GroupLeaves);
            Assert.NotNull(_metrics.ActiveGroups);
        }

        [Fact]
        public void RecordHubMethodInvocation_ReturnsDisposableTimer()
        {
            // Act
            using var timer = _metrics.RecordHubMethodInvocation("TestHub", "TestMethod", 123);

            // Assert
            Assert.NotNull(timer);
        }

        [Fact]
        public async Task RecordHubMethodInvocation_RecordsDuration()
        {
            // Act
            using (var timer = _metrics.RecordHubMethodInvocation("TestHub", "TestMethod", 123))
            {
                await Task.Delay(10); // Simulate some work
            }

            // Timer should be disposed and duration recorded
            // Note: We can't directly test the metric values without a more complex setup
            // but we can verify it doesn't throw
        }

        [Fact]
        public void RecordHubMethodInvocation_WithoutVirtualKeyId_Works()
        {
            // Act
            using var timer = _metrics.RecordHubMethodInvocation("TestHub", "TestMethod");

            // Assert
            Assert.NotNull(timer);
        }

        [Fact]
        public void RecordMessageProcessing_Receive_ReturnsTimer()
        {
            // Act
            using var timer = _metrics.RecordMessageProcessing("TestMessage", "receive");

            // Assert
            Assert.NotNull(timer);
        }

        [Fact]
        public void RecordMessageProcessing_Send_ReturnsTimer()
        {
            // Act
            using var timer = _metrics.RecordMessageProcessing("TestMessage", "send");

            // Assert
            Assert.NotNull(timer);
        }

        [Fact]
        public async Task RecordMessageProcessing_RecordsDuration()
        {
            // Act
            using (var timer = _metrics.RecordMessageProcessing("TestMessage", "receive"))
            {
                await Task.Delay(10); // Simulate some work
            }

            // Timer should be disposed and duration recorded
        }

        [Fact]
        public void RecordMessageProcessing_InvalidDirection_StillReturnsTimer()
        {
            // Act
            using var timer = _metrics.RecordMessageProcessing("TestMessage", "invalid");

            // Assert
            Assert.NotNull(timer);
        }

        [Fact]
        public void Metrics_CanBeUsedConcurrently()
        {
            // Arrange
            var tasks = new Task[10];

            // Act
            for (int i = 0; i < tasks.Length; i++)
            {
                var index = i;
                tasks[i] = Task.Run(() =>
                {
                    using var timer = _metrics.RecordHubMethodInvocation("TestHub", $"Method{index}");
                    Task.Delay(5).Wait();
                });
            }

            // Assert - should not throw
            Task.WaitAll(tasks);
        }

        public void Dispose()
        {
            _metrics?.Dispose();
        }
    }
}