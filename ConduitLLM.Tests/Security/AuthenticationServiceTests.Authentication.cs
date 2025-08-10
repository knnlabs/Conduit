using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Security
{
    /// <summary>
    /// General authentication tests
    /// </summary>
    public partial class AuthenticationServiceTests
    {
        #region AuthenticateAsync Tests

        [Fact]
        public async Task AuthenticateAsync_WithValidApiKey_ShouldReturnSuccess()
        {
            // Arrange
            var service = new MockAuthenticationService(
                _mockConfiguration.Object,
                _mockHttpContextAccessor.Object,
                _mockSecurityMonitoring.Object,
                _mockLogger.Object);
            
            var context = CreateHttpContext("valid-api-key", "192.168.1.100");
            _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(context);

            service.AddValidApiKey("valid-api-key", "user123");

            // Act
            var result = await service.AuthenticateAsync();

            // Assert
            result.IsAuthenticated.Should().BeTrue();
            result.UserId.Should().Be("user123");
            result.AuthenticationMethod.Should().Be("ApiKey");
        }

        [Fact]
        public async Task AuthenticateAsync_WithInvalidApiKey_ShouldReturnFailure()
        {
            // Arrange
            var service = new MockAuthenticationService(
                _mockConfiguration.Object,
                _mockHttpContextAccessor.Object,
                _mockSecurityMonitoring.Object,
                _mockLogger.Object);
            
            var context = CreateHttpContext("invalid-key", "192.168.1.100");
            _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(context);

            // Act
            var result = await service.AuthenticateAsync();

            // Assert
            result.IsAuthenticated.Should().BeFalse();
            result.FailureReason.Should().Be("Invalid API key");
            
            // Should record security event
            _mockSecurityMonitoring.Verify(x => x.RecordEventAsync(
                It.Is<SecurityEvent>(e => 
                    e.EventType == SecurityEventType.FailedAuthentication &&
                    e.SourceIp == "192.168.1.100")),
                Times.Once);
        }

        [Fact]
        public async Task AuthenticateAsync_WithMissingApiKey_ShouldReturnFailure()
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
            var result = await service.AuthenticateAsync();

            // Assert
            result.IsAuthenticated.Should().BeFalse();
            result.FailureReason.Should().Be("API key not provided");
        }

        [Fact]
        public async Task AuthenticateAsync_WithDisabledApiKeyAuth_ShouldAllowAnonymous()
        {
            // Arrange
            _mockConfiguration.Setup(x => x["Security:EnableApiKeyAuth"]).Returns("false");
            
            var service = new MockAuthenticationService(
                _mockConfiguration.Object,
                _mockHttpContextAccessor.Object,
                _mockSecurityMonitoring.Object,
                _mockLogger.Object);
            
            var context = CreateHttpContext(null, "192.168.1.100");
            _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(context);

            // Act
            var result = await service.AuthenticateAsync();

            // Assert
            result.IsAuthenticated.Should().BeTrue();
            result.UserId.Should().Be("anonymous");
            result.AuthenticationMethod.Should().Be("Anonymous");
        }

        #endregion
    }
}