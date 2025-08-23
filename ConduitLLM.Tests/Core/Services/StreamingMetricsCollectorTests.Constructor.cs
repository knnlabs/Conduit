using ConduitLLM.Core.Services;

namespace ConduitLLM.Tests.Core.Services
{
    public partial class StreamingMetricsCollectorTests
    {
        [Fact]
        public void Constructor_ValidParameters_InitializesCorrectly()
        {
            // Arrange & Act
            var collector = new StreamingMetricsCollector("req-123", "gpt-4", "OpenAI");

            // Assert
            Assert.NotNull(collector);
        }

        [Fact]
        public void Constructor_NullRequestId_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new StreamingMetricsCollector(null!, "gpt-4", "OpenAI"));
        }

        [Fact]
        public void Constructor_NullModel_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new StreamingMetricsCollector("req-123", null!, "OpenAI"));
        }

        [Fact]
        public void Constructor_NullProvider_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new StreamingMetricsCollector("req-123", "gpt-4", null!));
        }

        [Fact]
        public void Constructor_CustomEmissionInterval_AcceptsValue()
        {
            // Arrange & Act
            var collector = new StreamingMetricsCollector("req-123", "gpt-4", "OpenAI", TimeSpan.FromSeconds(5));

            // Assert
            Assert.NotNull(collector);
        }
    }
}