using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;
using Xunit;

namespace ConduitLLM.Tests.Core.Services
{
    public partial class StreamingMetricsCollectorTests
    {
        [Fact]
        public void StreamingMetrics_Properties_InitializedCorrectly()
        {
            // Arrange & Act
            var metrics = new StreamingMetrics
            {
                RequestId = "test-123",
                ElapsedMs = 1000,
                TokensGenerated = 50,
                CurrentTokensPerSecond = 50.0,
                TimeToFirstTokenMs = 100,
                AvgInterTokenLatencyMs = 20.0
            };

            // Assert
            Assert.Equal("test-123", metrics.RequestId);
            Assert.Equal(1000, metrics.ElapsedMs);
            Assert.Equal(50, metrics.TokensGenerated);
            Assert.Equal(50.0, metrics.CurrentTokensPerSecond);
            Assert.Equal(100, metrics.TimeToFirstTokenMs);
            Assert.Equal(20.0, metrics.AvgInterTokenLatencyMs);
        }
    }
}