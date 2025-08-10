using System;
using System.Threading;
using ConduitLLM.Core.Services;
using Xunit;

namespace ConduitLLM.Tests.Core.Services
{
    public partial class StreamingMetricsCollectorTests
    {
        [Fact]
        public void GetMetrics_BasicMetrics_ReturnsCorrectValues()
        {
            // Arrange
            var collector = new StreamingMetricsCollector("req-123", "gpt-4", "OpenAI");
            collector.RecordFirstToken();
            Thread.Sleep(50);
            collector.RecordToken();

            // Act
            var metrics = collector.GetMetrics();

            // Assert
            Assert.Equal("req-123", metrics.RequestId);
            Assert.True(metrics.ElapsedMs > 0);
            Assert.Equal(2, metrics.TokensGenerated);
            Assert.NotNull(metrics.TimeToFirstTokenMs);
            Assert.True(metrics.CurrentTokensPerSecond > 0);
        }

        [Fact]
        public void GetMetrics_CalculatesAverageInterTokenLatency()
        {
            // Arrange
            var collector = new StreamingMetricsCollector("req-123", "gpt-4", "OpenAI");
            collector.RecordFirstToken();
            
            // Generate tokens with delays
            for (int i = 0; i < 5; i++)
            {
                Thread.Sleep(20);
                collector.RecordToken();
            }

            // Act
            var metrics = collector.GetMetrics();

            // Assert
            Assert.NotNull(metrics.AvgInterTokenLatencyMs);
            Assert.True(metrics.AvgInterTokenLatencyMs > 0);
        }

        [Fact]
        public void GetMetrics_SingleToken_NoInterTokenLatency()
        {
            // Arrange
            var collector = new StreamingMetricsCollector("req-123", "gpt-4", "OpenAI");
            collector.RecordFirstToken();

            // Act
            var metrics = collector.GetMetrics();

            // Assert
            Assert.Null(metrics.AvgInterTokenLatencyMs);
        }

        [Fact]
        public void GetMetrics_CalculatesTokensPerSecond()
        {
            // Arrange
            var collector = new StreamingMetricsCollector("req-123", "gpt-4", "OpenAI");
            collector.RecordFirstToken();
            
            // Generate 10 tokens over ~100ms
            for (int i = 0; i < 9; i++)
            {
                Thread.Sleep(10);
                collector.RecordToken();
            }

            // Act
            var metrics = collector.GetMetrics();

            // Assert
            Assert.True(metrics.CurrentTokensPerSecond > 0);
            Assert.True(metrics.CurrentTokensPerSecond < 1000); // Reasonable upper bound
        }

        [Fact]
        public void GetMetrics_VeryLongStreaming_HandlesLargeValues()
        {
            // Arrange
            var collector = new StreamingMetricsCollector("req-123", "gpt-4", "OpenAI");
            collector.RecordFirstToken();
            Thread.Sleep(10); // Ensure some elapsed time
            
            // Simulate a long streaming session
            for (int i = 0; i < 1000; i++)
            {
                collector.RecordToken();
            }

            // Act
            var metrics = collector.GetMetrics();

            // Assert
            Assert.Equal(1001, metrics.TokensGenerated);
            Assert.True(metrics.ElapsedMs > 0);
            Assert.True(metrics.CurrentTokensPerSecond > 0);
        }
    }
}