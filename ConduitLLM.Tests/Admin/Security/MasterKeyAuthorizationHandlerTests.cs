using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ConduitLLM.Admin.Security;

namespace ConduitLLM.Tests.Admin.Security
{
    public class MasterKeyAuthorizationHandlerTests
    {
        private readonly Mock<IConfiguration> _configurationMock;
        private readonly Mock<ILogger<MasterKeyAuthorizationHandler>> _loggerMock;
        private readonly MasterKeyAuthorizationHandler _handler;
        private readonly MasterKeyRequirement _requirement;

        public MasterKeyAuthorizationHandlerTests()
        {
            _configurationMock = new Mock<IConfiguration>();
            _loggerMock = new Mock<ILogger<MasterKeyAuthorizationHandler>>();
            _handler = new MasterKeyAuthorizationHandler(_configurationMock.Object, _loggerMock.Object);
            _requirement = new MasterKeyRequirement();
        }

        [Fact]
        public async Task HandleRequirementAsync_AuthenticatedUserWithMasterKeyClaim_Succeeds()
        {
            // Arrange
            var claims = new[]
            {
                new Claim(ClaimTypes.Name, "AdminUser"),
                new Claim("MasterKey", "true")
            };
            var identity = new ClaimsIdentity(claims, "MasterKey");
            var principal = new ClaimsPrincipal(identity);

            var httpContext = new DefaultHttpContext();
            httpContext.User = principal;

            var authContext = new AuthorizationHandlerContext(
                new[] { _requirement },
                principal,
                httpContext
            );

            // Act
            await _handler.HandleAsync(authContext);

            // Assert
            Assert.True(authContext.HasSucceeded);
        }

        [Fact]
        public async Task HandleRequirementAsync_AuthenticatedUserWithoutMasterKeyClaim_Fails()
        {
            // Arrange
            var claims = new[]
            {
                new Claim(ClaimTypes.Name, "RegularUser")
                // No MasterKey claim
            };
            var identity = new ClaimsIdentity(claims, "SomeAuth");
            var principal = new ClaimsPrincipal(identity);

            var httpContext = new DefaultHttpContext();
            httpContext.User = principal;

            var authContext = new AuthorizationHandlerContext(
                new[] { _requirement },
                principal,
                httpContext
            );

            // Act
            await _handler.HandleAsync(authContext);

            // Assert
            Assert.False(authContext.HasSucceeded);
        }

        [Fact]
        public async Task HandleRequirementAsync_UnauthenticatedUser_ChecksHeaders()
        {
            // Arrange
            var masterKey = "test-master-key";
            Environment.SetEnvironmentVariable("CONDUIT_API_TO_API_BACKEND_AUTH_KEY", masterKey);

            var principal = new ClaimsPrincipal(); // Unauthenticated
            var httpContext = new DefaultHttpContext();
            httpContext.User = principal;
            httpContext.Request.Headers["X-API-Key"] = masterKey;

            var authContext = new AuthorizationHandlerContext(
                new[] { _requirement },
                principal,
                httpContext
            );

            // Act
            await _handler.HandleAsync(authContext);

            // Assert
            Assert.True(authContext.HasSucceeded);

            // Cleanup
            Environment.SetEnvironmentVariable("CONDUIT_API_TO_API_BACKEND_AUTH_KEY", null);
        }

        [Fact]
        public async Task HandleRequirementAsync_EphemeralKeyInHeader_DoesNotSucceedDirectly()
        {
            // Arrange
            var ephemeralKey = "emk_testkey123456789";
            Environment.SetEnvironmentVariable("CONDUIT_API_TO_API_BACKEND_AUTH_KEY", "master-key");

            var principal = new ClaimsPrincipal(); // Unauthenticated
            var httpContext = new DefaultHttpContext();
            httpContext.User = principal;
            httpContext.Request.Headers["X-API-Key"] = ephemeralKey;

            var authContext = new AuthorizationHandlerContext(
                new[] { _requirement },
                principal,
                httpContext
            );

            // Act
            await _handler.HandleAsync(authContext);

            // Assert
            Assert.False(authContext.HasSucceeded);
            // Ephemeral keys should only succeed if the user is already authenticated with MasterKey claim

            // Cleanup
            Environment.SetEnvironmentVariable("CONDUIT_API_TO_API_BACKEND_AUTH_KEY", null);
        }

        [Fact]
        public async Task HandleRequirementAsync_EphemeralKeyWithAuthentication_Succeeds()
        {
            // Arrange - User authenticated via ephemeral key (has MasterKey claim)
            var claims = new[]
            {
                new Claim(ClaimTypes.Name, "AdminUser"),
                new Claim("MasterKey", "true") // Set by MasterKeyAuthenticationHandler for ephemeral keys
            };
            var identity = new ClaimsIdentity(claims, "MasterKey");
            var principal = new ClaimsPrincipal(identity);

            var httpContext = new DefaultHttpContext();
            httpContext.User = principal;
            httpContext.Request.Headers["X-Master-Key"] = "emk_testkey123456789";

            var authContext = new AuthorizationHandlerContext(
                new[] { _requirement },
                principal,
                httpContext
            );

            // Act
            await _handler.HandleAsync(authContext);

            // Assert
            Assert.True(authContext.HasSucceeded);
            // Succeeds because user is authenticated with MasterKey claim
        }

        [Fact]
        public async Task HandleRequirementAsync_ValidMasterKeyInLegacyHeader_Succeeds()
        {
            // Arrange
            var masterKey = "test-master-key";
            Environment.SetEnvironmentVariable("CONDUIT_API_TO_API_BACKEND_AUTH_KEY", masterKey);

            var principal = new ClaimsPrincipal(); // Unauthenticated
            var httpContext = new DefaultHttpContext();
            httpContext.User = principal;
            httpContext.Request.Headers["X-Master-Key"] = masterKey; // Legacy header

            var authContext = new AuthorizationHandlerContext(
                new[] { _requirement },
                principal,
                httpContext
            );

            // Act
            await _handler.HandleAsync(authContext);

            // Assert
            Assert.True(authContext.HasSucceeded);

            // Cleanup
            Environment.SetEnvironmentVariable("CONDUIT_API_TO_API_BACKEND_AUTH_KEY", null);
        }

        [Fact]
        public async Task HandleRequirementAsync_NoMasterKeyConfigured_Fails()
        {
            // Arrange
            Environment.SetEnvironmentVariable("CONDUIT_API_TO_API_BACKEND_AUTH_KEY", null);

            var principal = new ClaimsPrincipal();
            var httpContext = new DefaultHttpContext();
            httpContext.User = principal;
            httpContext.Request.Headers["X-API-Key"] = "any-key";

            var authContext = new AuthorizationHandlerContext(
                new[] { _requirement },
                principal,
                httpContext
            );

            // Act
            await _handler.HandleAsync(authContext);

            // Assert
            Assert.False(authContext.HasSucceeded);
        }

        [Fact]
        public async Task HandleRequirementAsync_BearerTokenWithMasterKey_Succeeds()
        {
            // Arrange
            var masterKey = "test-master-key";
            Environment.SetEnvironmentVariable("CONDUIT_API_TO_API_BACKEND_AUTH_KEY", masterKey);

            var principal = new ClaimsPrincipal();
            var httpContext = new DefaultHttpContext();
            httpContext.User = principal;
            httpContext.Request.Headers["Authorization"] = $"Bearer {masterKey}";

            var authContext = new AuthorizationHandlerContext(
                new[] { _requirement },
                principal,
                httpContext
            );

            // Act
            await _handler.HandleAsync(authContext);

            // Assert
            Assert.True(authContext.HasSucceeded);

            // Cleanup
            Environment.SetEnvironmentVariable("CONDUIT_API_TO_API_BACKEND_AUTH_KEY", null);
        }

        [Fact]
        public async Task HandleRequirementAsync_QueryStringTokenForSignalR_Succeeds()
        {
            // Arrange
            var masterKey = "test-master-key";
            Environment.SetEnvironmentVariable("CONDUIT_API_TO_API_BACKEND_AUTH_KEY", masterKey);

            var principal = new ClaimsPrincipal();
            var httpContext = new DefaultHttpContext();
            httpContext.User = principal;
            httpContext.Request.Path = "/hubs/admin-notifications";
            httpContext.Request.QueryString = new QueryString($"?access_token={masterKey}");

            var authContext = new AuthorizationHandlerContext(
                new[] { _requirement },
                principal,
                httpContext
            );

            // Act
            await _handler.HandleAsync(authContext);

            // Assert
            Assert.True(authContext.HasSucceeded);

            // Cleanup
            Environment.SetEnvironmentVariable("CONDUIT_API_TO_API_BACKEND_AUTH_KEY", null);
        }
    }
}