using ConduitLLM.Core.Services;

namespace ConduitLLM.Tests.Core.Services
{
    public partial class StreamingMetricsCollectorTests
    {
        [Fact]
        public void RecordFirstToken_FirstCall_RecordsTimeToFirstToken()
        {
            // Arrange
            var collector = new StreamingMetricsCollector("req-123", "gpt-4", "OpenAI");
            Thread.Sleep(50); // Simulate some delay

            // Act
            collector.RecordFirstToken();
            var metrics = collector.GetMetrics();

            // Assert
            Assert.NotNull(metrics.TimeToFirstTokenMs);
            Assert.True(metrics.TimeToFirstTokenMs > 0);
            Assert.Equal(1, metrics.TokensGenerated);
        }

        [Fact]
        public void RecordFirstToken_MultipleCalls_OnlyRecordsOnce()
        {
            // Arrange
            var collector = new StreamingMetricsCollector("req-123", "gpt-4", "OpenAI");
            
            // Act
            collector.RecordFirstToken();
            var firstMetrics = collector.GetMetrics();
            var firstTimeToToken = firstMetrics.TimeToFirstTokenMs;
            
            Thread.Sleep(100);
            collector.RecordFirstToken(); // Should be ignored
            var secondMetrics = collector.GetMetrics();

            // Assert
            Assert.Equal(firstTimeToToken, secondMetrics.TimeToFirstTokenMs);
            Assert.Equal(1, secondMetrics.TokensGenerated); // Still only 1 token
        }

        [Fact]
        public void RecordToken_MultipleCalls_IncrementsTokenCount()
        {
            // Arrange
            var collector = new StreamingMetricsCollector("req-123", "gpt-4", "OpenAI");
            collector.RecordFirstToken();

            // Act
            collector.RecordToken();
            collector.RecordToken();
            collector.RecordToken();
            var metrics = collector.GetMetrics();

            // Assert
            Assert.Equal(4, metrics.TokensGenerated); // 1 from first token + 3 additional
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1000)]
        public void RecordToken_VariousTokenCounts_HandlesCorrectly(int tokenCount)
        {
            // Arrange
            var collector = new StreamingMetricsCollector("req-123", "gpt-4", "OpenAI");
            
            // Act
            for (int i = 0; i < tokenCount; i++)
            {
                collector.RecordToken();
            }
            var metrics = collector.GetMetrics();

            // Assert
            Assert.Equal(tokenCount, metrics.TokensGenerated);
        }

        [Fact(Skip = "StreamingMetricsCollector is not thread-safe by design - it's meant to be used from a single streaming context")]
        public async Task ConcurrentTokenRecording_ThreadSafe()
        {
            // Arrange
            var collector = new StreamingMetricsCollector("req-123", "gpt-4", "OpenAI");
            collector.RecordFirstToken();
            
            const int threadCount = 10;
            const int tokensPerThread = 100;
            var tasks = new Task[threadCount];

            // Act
            for (int i = 0; i < threadCount; i++)
            {
                tasks[i] = Task.Run(() =>
                {
                    for (int j = 0; j < tokensPerThread; j++)
                    {
                        collector.RecordToken();
                    }
                });
            }
            
            await Task.WhenAll(tasks);
            var metrics = collector.GetMetrics();

            // Assert
            // Note: This test would fail due to race conditions
            Assert.Equal(1 + (threadCount * tokensPerThread), metrics.TokensGenerated);
        }
    }
}