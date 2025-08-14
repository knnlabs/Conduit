using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Security
{
    /// <summary>
    /// Admin authentication tests
    /// </summary>
    public partial class AuthenticationServiceTests
    {
        #region Admin Authentication Tests

        [Fact]
        public async Task AuthenticateAdmin_WithCorrectPassword_ShouldSucceed()
        {
            // Arrange
            var service = new MockAuthenticationService(
                _mockConfiguration.Object,
                _mockHttpContextAccessor.Object,
                _mockSecurityMonitoring.Object,
                _mockLogger.Object);

            // Act
            var result = await service.AuthenticateAdminAsync("admin", "admin123!");

            // Assert
            result.IsAuthenticated.Should().BeTrue();
            result.UserId.Should().Be("admin");
            result.AuthenticationMethod.Should().Be("Password");
        }

        [Fact]
        public async Task AuthenticateAdmin_WithIncorrectPassword_ShouldFail()
        {
            // Arrange
            var service = new MockAuthenticationService(
                _mockConfiguration.Object,
                _mockHttpContextAccessor.Object,
                _mockSecurityMonitoring.Object,
                _mockLogger.Object);
            
            var context = CreateHttpContext(null, "192.168.1.100");
            _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(context);

            // Act
            var result = await service.AuthenticateAdminAsync("admin", "wrong-password");

            // Assert
            result.IsAuthenticated.Should().BeFalse();
            result.FailureReason.Should().Be("Invalid credentials");
            
            // Should record security event
            _mockSecurityMonitoring.Verify(x => x.RecordEventAsync(
                It.Is<SecurityEvent>(e => 
                    e.EventType == SecurityEventType.FailedAuthentication &&
                    e.Details!.Contains("Admin login failed"))),
                Times.Once);
        }

        #endregion
    }
}