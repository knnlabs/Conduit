using FluentAssertions;

namespace ConduitLLM.Tests.Security
{
    /// <summary>
    /// API key validation tests
    /// </summary>
    public partial class AuthenticationServiceTests
    {
        #region ValidateApiKey Tests

        [Fact]
        public async Task ValidateApiKey_WithValidKey_ShouldReturnUser()
        {
            // Arrange
            var service = new MockAuthenticationService(
                _mockConfiguration.Object,
                _mockHttpContextAccessor.Object,
                _mockSecurityMonitoring.Object,
                _mockLogger.Object);
            
            service.AddValidApiKey("test-key-123", "user456");

            // Act
            var result = await service.ValidateApiKeyAsync("test-key-123");

            // Assert
            result.IsValid.Should().BeTrue();
            result.UserId.Should().Be("user456");
            result.Permissions.Should().NotBeEmpty();
        }

        [Fact]
        public async Task ValidateApiKey_WithExpiredKey_ShouldReturnInvalid()
        {
            // Arrange
            var service = new MockAuthenticationService(
                _mockConfiguration.Object,
                _mockHttpContextAccessor.Object,
                _mockSecurityMonitoring.Object,
                _mockLogger.Object);
            
            service.AddValidApiKey("expired-key", "user789", DateTime.UtcNow.AddDays(-1));

            // Act
            var result = await service.ValidateApiKeyAsync("expired-key");

            // Assert
            result.IsValid.Should().BeFalse();
            result.FailureReason.Should().Be("API key expired");
        }

        [Fact]
        public async Task ValidateApiKey_WithRevokedKey_ShouldReturnInvalid()
        {
            // Arrange
            var service = new MockAuthenticationService(
                _mockConfiguration.Object,
                _mockHttpContextAccessor.Object,
                _mockSecurityMonitoring.Object,
                _mockLogger.Object);
            
            service.AddValidApiKey("revoked-key", "user999", isRevoked: true);

            // Act
            var result = await service.ValidateApiKeyAsync("revoked-key");

            // Assert
            result.IsValid.Should().BeFalse();
            result.FailureReason.Should().Be("API key revoked");
        }

        #endregion
    }
}