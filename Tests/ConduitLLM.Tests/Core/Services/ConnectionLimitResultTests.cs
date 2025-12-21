using ConduitLLM.Core.Services;

namespace ConduitLLM.Tests.Core.Services
{
    /// <summary>
    /// Unit tests for ConnectionLimitResult class
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Component", "ConnectionLimitResult")]
    [Trait("Feature", "ConnectionLimiting")]
    public class ConnectionLimitResultTests
    {
        [Fact]
        public void DefaultValues_AreCorrect()
        {
            // Arrange & Act
            var result = new ConnectionLimitResult();

            // Assert
            Assert.False(result.IsAllowed);
            Assert.Equal(0, result.CurrentConnections);
            Assert.Equal(0, result.MaxConnections);
            Assert.Equal(string.Empty, result.DenialReason);
        }

        [Fact]
        public void IsAllowed_CanBeSetToTrue()
        {
            // Arrange & Act
            var result = new ConnectionLimitResult
            {
                IsAllowed = true
            };

            // Assert
            Assert.True(result.IsAllowed);
        }

        [Fact]
        public void IsAllowed_CanBeSetToFalse()
        {
            // Arrange & Act
            var result = new ConnectionLimitResult
            {
                IsAllowed = false
            };

            // Assert
            Assert.False(result.IsAllowed);
        }

        [Fact]
        public void DenialReason_DefaultsToEmptyString()
        {
            // Arrange & Act
            var result = new ConnectionLimitResult();

            // Assert
            Assert.Equal(string.Empty, result.DenialReason);
        }

        [Fact]
        public void DenialReason_CanBeSet()
        {
            // Arrange
            const string reason = "Connection limit exceeded (100/100). Please close existing connections.";

            // Act
            var result = new ConnectionLimitResult
            {
                DenialReason = reason
            };

            // Assert
            Assert.Equal(reason, result.DenialReason);
        }

        [Theory]
        [InlineData(0, 100)]
        [InlineData(50, 100)]
        [InlineData(99, 100)]
        [InlineData(100, 100)]
        [InlineData(150, 100)]
        public void CurrentConnections_AndMaxConnections_CanBeSet(int current, int max)
        {
            // Arrange & Act
            var result = new ConnectionLimitResult
            {
                CurrentConnections = current,
                MaxConnections = max
            };

            // Assert
            Assert.Equal(current, result.CurrentConnections);
            Assert.Equal(max, result.MaxConnections);
        }

        [Fact]
        public void AllProperties_CanBeSetTogether()
        {
            // Arrange
            const bool isAllowed = false;
            const int currentConnections = 100;
            const int maxConnections = 100;
            const string denialReason = "Connection limit exceeded";

            // Act
            var result = new ConnectionLimitResult
            {
                IsAllowed = isAllowed,
                CurrentConnections = currentConnections,
                MaxConnections = maxConnections,
                DenialReason = denialReason
            };

            // Assert
            Assert.Equal(isAllowed, result.IsAllowed);
            Assert.Equal(currentConnections, result.CurrentConnections);
            Assert.Equal(maxConnections, result.MaxConnections);
            Assert.Equal(denialReason, result.DenialReason);
        }

        [Fact]
        public void AllowedResult_HasEmptyDenialReason()
        {
            // Arrange & Act
            var result = new ConnectionLimitResult
            {
                IsAllowed = true,
                CurrentConnections = 50,
                MaxConnections = 100
            };

            // Assert
            Assert.True(result.IsAllowed);
            Assert.Equal(string.Empty, result.DenialReason);
        }

        [Fact]
        public void DeniedResult_HasDenialReason()
        {
            // Arrange & Act
            var result = new ConnectionLimitResult
            {
                IsAllowed = false,
                CurrentConnections = 100,
                MaxConnections = 100,
                DenialReason = "Connection limit exceeded (100/100). Please close existing connections before opening new ones."
            };

            // Assert
            Assert.False(result.IsAllowed);
            Assert.NotEmpty(result.DenialReason);
            Assert.Contains("100/100", result.DenialReason);
        }
    }
}
