using ConduitLLM.Core.Services;

namespace ConduitLLM.Tests.Core.Services
{
    public partial class StreamingMetricsCollectorTests
    {
        [Fact]
        public void ShouldEmitMetrics_DefaultInterval_RespectsTiming()
        {
            // Arrange
            var collector = new StreamingMetricsCollector("req-123", "gpt-4", "OpenAI");

            // Act & Assert
            // First call might not be true since lastEmissionTime is set in constructor
            var firstCall = collector.ShouldEmitMetrics();
            if (firstCall)
            {
                Assert.False(collector.ShouldEmitMetrics()); // Immediate second call should be false
            }
            
            // Wait for interval to pass
            Thread.Sleep(1100);
            Assert.True(collector.ShouldEmitMetrics()); // Should be true after interval
        }

        [Fact]
        public void ShouldEmitMetrics_CustomInterval_RespectsTiming()
        {
            // Arrange
            var collector = new StreamingMetricsCollector("req-123", "gpt-4", "OpenAI", 
                TimeSpan.FromMilliseconds(500));

            // Act & Assert
            var firstCall = collector.ShouldEmitMetrics();
            if (firstCall)
            {
                Assert.False(collector.ShouldEmitMetrics());
            }
            
            Thread.Sleep(600);
            Assert.True(collector.ShouldEmitMetrics());
        }
    }
}