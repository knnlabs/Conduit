using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using ConduitLLM.Admin.Security;

namespace ConduitLLM.Tests.Admin.Security
{
    public class MasterKeyAuthorizationHandlerTests
    {
        private readonly MasterKeyAuthorizationHandler _handler;
        private readonly MasterKeyRequirement _requirement;

        public MasterKeyAuthorizationHandlerTests()
        {
            _handler = new MasterKeyAuthorizationHandler();
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
        public async Task HandleRequirementAsync_UnauthenticatedUserWithMasterKeyHeader_Fails()
        {
            var principal = new ClaimsPrincipal(); // Unauthenticated
            var httpContext = new DefaultHttpContext();
            httpContext.User = principal;
            httpContext.Request.Headers["X-API-Key"] = "test-master-key";

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
        public async Task HandleRequirementAsync_EphemeralKeyInHeader_DoesNotSucceedDirectly()
        {
            // Arrange
            var ephemeralKey = "emk_testkey123456789";
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
        public async Task HandleRequirementAsync_UnauthenticatedUserWithLegacyHeader_Fails()
        {
            var principal = new ClaimsPrincipal(); // Unauthenticated
            var httpContext = new DefaultHttpContext();
            httpContext.User = principal;
            httpContext.Request.Headers["X-Master-Key"] = "test-master-key";

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
        public async Task HandleRequirementAsync_UnauthenticatedUserWithBearerToken_Fails()
        {
            var principal = new ClaimsPrincipal();
            var httpContext = new DefaultHttpContext();
            httpContext.User = principal;
            httpContext.Request.Headers["Authorization"] = "Bearer test-master-key";

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
        public async Task HandleRequirementAsync_UnauthenticatedUserWithQueryToken_Fails()
        {
            var principal = new ClaimsPrincipal();
            var httpContext = new DefaultHttpContext();
            httpContext.User = principal;
            httpContext.Request.Path = "/hubs/admin-notifications";
            httpContext.Request.QueryString = new QueryString("?access_token=test-master-key");

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
    }
}
