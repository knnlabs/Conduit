using System.Security.Claims;

using FluentAssertions;

namespace ConduitLLM.Tests.Security
{
    /// <summary>
    /// Token generation and validation tests
    /// </summary>
    public partial class AuthenticationServiceTests
    {
        #region Token Generation Tests

        [Fact]
        public async Task GenerateToken_WithValidUser_ShouldReturnToken()
        {
            // Arrange
            var service = new MockAuthenticationService(
                _mockConfiguration.Object,
                _mockHttpContextAccessor.Object,
                _mockSecurityMonitoring.Object,
                _mockLogger.Object);

            // Act
            var token = await service.GenerateTokenAsync("user123", new[] { "read", "write" });

            // Assert
            token.Should().NotBeNullOrWhiteSpace();
            token.Should().HaveLength(32); // Mock implementation returns 32-char token
        }

        [Fact]
        public async Task ValidateToken_WithValidToken_ShouldReturnClaims()
        {
            // Arrange
            var service = new MockAuthenticationService(
                _mockConfiguration.Object,
                _mockHttpContextAccessor.Object,
                _mockSecurityMonitoring.Object,
                _mockLogger.Object);
            
            var token = await service.GenerateTokenAsync("user123", new[] { "read" });

            // Act
            var result = await service.ValidateTokenAsync(token);

            // Assert
            result.IsValid.Should().BeTrue();
            result.Claims.Should().Contain(c => c.Type == ClaimTypes.Name && c.Value == "user123");
            result.Claims.Should().Contain(c => c.Type == "permission" && c.Value == "read");
        }

        [Fact]
        public async Task ValidateToken_WithInvalidToken_ShouldReturnInvalid()
        {
            // Arrange
            var service = new MockAuthenticationService(
                _mockConfiguration.Object,
                _mockHttpContextAccessor.Object,
                _mockSecurityMonitoring.Object,
                _mockLogger.Object);

            // Act
            var result = await service.ValidateTokenAsync("invalid-token");

            // Assert
            result.IsValid.Should().BeFalse();
            result.FailureReason.Should().Be("Invalid token");
        }

        #endregion
    }
}