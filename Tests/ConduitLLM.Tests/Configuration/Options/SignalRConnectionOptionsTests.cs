using ConduitLLM.Configuration.Options;

namespace ConduitLLM.Tests.Configuration.Options
{
    /// <summary>
    /// Unit tests for SignalRConnectionOptions configuration
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Component", "SignalRConnectionOptions")]
    [Trait("Feature", "ConnectionLimiting")]
    public class SignalRConnectionOptionsTests
    {
        [Fact]
        public void Constructor_SetsDefaultValues()
        {
            // Arrange & Act
            var options = new SignalRConnectionOptions();

            // Assert
            Assert.Equal(100, options.MaxConnectionsPerVirtualKey);
            Assert.Equal(10000, options.MaxTotalConnections);
            Assert.True(options.EnforceLimits);
        }

        [Fact]
        public void SectionName_IsCorrect()
        {
            // Act & Assert
            Assert.Equal("SignalR:ConnectionLimits", SignalRConnectionOptions.SectionName);
        }

        [Fact]
        public void EnforceLimits_DefaultsToTrue()
        {
            // Arrange & Act
            var options = new SignalRConnectionOptions();

            // Assert
            Assert.True(options.EnforceLimits);
        }

        [Fact]
        public void EnforceLimits_CanBeSetToFalse()
        {
            // Arrange & Act
            var options = new SignalRConnectionOptions
            {
                EnforceLimits = false
            };

            // Assert
            Assert.False(options.EnforceLimits);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(50)]
        [InlineData(100)]
        [InlineData(500)]
        [InlineData(10000)]
        public void MaxConnectionsPerVirtualKey_CanBeSetToValidValues(int value)
        {
            // Arrange & Act
            var options = new SignalRConnectionOptions
            {
                MaxConnectionsPerVirtualKey = value
            };

            // Assert
            Assert.Equal(value, options.MaxConnectionsPerVirtualKey);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(1000)]
        [InlineData(10000)]
        [InlineData(50000)]
        [InlineData(100000)]
        public void MaxTotalConnections_CanBeSetToValidValues(int value)
        {
            // Arrange & Act
            var options = new SignalRConnectionOptions
            {
                MaxTotalConnections = value
            };

            // Assert
            Assert.Equal(value, options.MaxTotalConnections);
        }

        [Fact]
        public void AllProperties_CanBeSetViaObjectInitializer()
        {
            // Arrange
            const int maxPerVk = 50;
            const int maxTotal = 5000;
            const bool enforceLimits = false;

            // Act
            var options = new SignalRConnectionOptions
            {
                MaxConnectionsPerVirtualKey = maxPerVk,
                MaxTotalConnections = maxTotal,
                EnforceLimits = enforceLimits
            };

            // Assert
            Assert.Equal(maxPerVk, options.MaxConnectionsPerVirtualKey);
            Assert.Equal(maxTotal, options.MaxTotalConnections);
            Assert.Equal(enforceLimits, options.EnforceLimits);
        }
    }
}
