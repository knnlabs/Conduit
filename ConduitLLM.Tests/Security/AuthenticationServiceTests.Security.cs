using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Security
{
    /// <summary>
    /// Security-related tests (HTTPS requirement, rate limiting)
    /// </summary>
    public partial class AuthenticationServiceTests
    {
        #region HTTPS Requirement Tests

        [Fact]
        public async Task AuthenticateAsync_WithHttpWhenHttpsRequired_ShouldFail()
        {
            // Arrange
            var service = new MockAuthenticationService(
                _mockConfiguration.Object,
                _mockHttpContextAccessor.Object,
                _mockSecurityMonitoring.Object,
                _mockLogger.Object);
            
            var context = CreateHttpContext("valid-key", "192.168.1.100", isHttps: false);
            _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(context);
            
            service.AddValidApiKey("valid-key", "user123");

            // Act
            var result = await service.AuthenticateAsync();

            // Assert
            result.IsAuthenticated.Should().BeFalse();
            result.FailureReason.Should().Be("HTTPS required");
        }

        #endregion

        #region Rate Limiting Tests

        [Fact]
        public async Task AuthenticateAsync_WithTooManyFailedAttempts_ShouldBlockTemporarily()
        {
            // Arrange
            var service = new MockAuthenticationService(
                _mockConfiguration.Object,
                _mockHttpContextAccessor.Object,
                _mockSecurityMonitoring.Object,
                _mockLogger.Object);
            
            var context = CreateHttpContext("invalid-key", "192.168.1.100");
            _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(context);

            // Simulate multiple failed attempts
            for (int i = 0; i < 5; i++)
            {
                await service.AuthenticateAsync();
            }

            // Act - 6th attempt should be blocked
            var result = await service.AuthenticateAsync();

            // Assert
            result.IsAuthenticated.Should().BeFalse();
            result.FailureReason.Should().Be("Too many failed attempts. Try again later.");
        }

        #endregion
    }
}