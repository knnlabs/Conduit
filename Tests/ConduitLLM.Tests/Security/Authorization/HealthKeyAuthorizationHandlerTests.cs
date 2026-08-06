using System.Net;
using System.Security.Claims;

using ConduitLLM.Security.Authorization;

using AwesomeAssertions;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

using Moq;

using Xunit;
using Xunit.Abstractions;

namespace ConduitLLM.Tests.Security.Authorization;

/// <summary>
/// Unit tests for <see cref="HealthKeyAuthorizationHandler"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
public class HealthKeyAuthorizationHandlerTests : TestBase
{
    private const string TestHealthKey = "test-health-monitoring-key-12345";
    private readonly Mock<ILogger<HealthKeyAuthorizationHandler>> _loggerMock;

    public HealthKeyAuthorizationHandlerTests(ITestOutputHelper output) : base(output)
    {
        _loggerMock = CreateLogger<HealthKeyAuthorizationHandler>();
    }

    private HealthKeyAuthorizationHandler CreateHandler(string? healthKey = TestHealthKey)
    {
        // Set the environment variable for the test
        if (healthKey != null)
        {
            Environment.SetEnvironmentVariable(HealthKeyAuthorizationHandler.HealthKeyEnvVar, healthKey);
        }
        else
        {
            Environment.SetEnvironmentVariable(HealthKeyAuthorizationHandler.HealthKeyEnvVar, null);
        }

        return new HealthKeyAuthorizationHandler(_loggerMock.Object);
    }

    private static HttpContext CreateHttpContext(IPAddress? remoteIpAddress, string? healthKeyHeader = null)
    {
        var context = new DefaultHttpContext();

        if (remoteIpAddress != null)
        {
            context.Connection.RemoteIpAddress = remoteIpAddress;
        }

        if (healthKeyHeader != null)
        {
            context.Request.Headers[HealthKeyAuthorizationHandler.HealthKeyHeaderName] = healthKeyHeader;
        }

        return context;
    }

    private static AuthorizationHandlerContext CreateAuthorizationContext(HttpContext httpContext)
    {
        var requirements = new[] { new HealthKeyRequirement() };
        var user = new ClaimsPrincipal(new ClaimsIdentity());
        return new AuthorizationHandlerContext(requirements, user, httpContext);
    }

    [Fact]
    public async Task HandleRequirementAsync_PrivateNetworkRequest_10x_Succeeds()
    {
        // Arrange
        var handler = CreateHandler();
        var httpContext = CreateHttpContext(IPAddress.Parse("10.0.0.1"));
        var authContext = CreateAuthorizationContext(httpContext);

        // Act
        await handler.HandleAsync(authContext);

        // Assert
        authContext.HasSucceeded.Should().BeTrue("10.x.x.x is a private network");
    }

    [Fact]
    public async Task HandleRequirementAsync_PrivateNetworkRequest_172_Succeeds()
    {
        // Arrange
        var handler = CreateHandler();
        var httpContext = CreateHttpContext(IPAddress.Parse("172.16.0.1"));
        var authContext = CreateAuthorizationContext(httpContext);

        // Act
        await handler.HandleAsync(authContext);

        // Assert
        authContext.HasSucceeded.Should().BeTrue("172.16.x.x is a private network");
    }

    [Fact]
    public async Task HandleRequirementAsync_PrivateNetworkRequest_192_Succeeds()
    {
        // Arrange
        var handler = CreateHandler();
        var httpContext = CreateHttpContext(IPAddress.Parse("192.168.1.1"));
        var authContext = CreateAuthorizationContext(httpContext);

        // Act
        await handler.HandleAsync(authContext);

        // Assert
        authContext.HasSucceeded.Should().BeTrue("192.168.x.x is a private network");
    }

    [Fact]
    public async Task HandleRequirementAsync_PrivateNetworkRequest_Loopback_Succeeds()
    {
        // Arrange
        var handler = CreateHandler();
        var httpContext = CreateHttpContext(IPAddress.Loopback);
        var authContext = CreateAuthorizationContext(httpContext);

        // Act
        await handler.HandleAsync(authContext);

        // Assert
        authContext.HasSucceeded.Should().BeTrue("127.0.0.1 is loopback/private");
    }

    [Fact]
    public async Task HandleRequirementAsync_PrivateNetworkRequest_IPv6Loopback_Succeeds()
    {
        // Arrange
        var handler = CreateHandler();
        var httpContext = CreateHttpContext(IPAddress.IPv6Loopback);
        var authContext = CreateAuthorizationContext(httpContext);

        // Act
        await handler.HandleAsync(authContext);

        // Assert
        authContext.HasSucceeded.Should().BeTrue("::1 is IPv6 loopback/private");
    }

    [Fact]
    public async Task HandleRequirementAsync_ExternalWithValidKey_Succeeds()
    {
        // Arrange
        var handler = CreateHandler();
        var httpContext = CreateHttpContext(IPAddress.Parse("203.0.113.1"), TestHealthKey);
        var authContext = CreateAuthorizationContext(httpContext);

        // Act
        await handler.HandleAsync(authContext);

        // Assert
        authContext.HasSucceeded.Should().BeTrue("Valid health key was provided");
    }

    [Fact]
    public async Task HandleRequirementAsync_ExternalWithInvalidKey_Fails()
    {
        // Arrange
        var handler = CreateHandler();
        var httpContext = CreateHttpContext(IPAddress.Parse("203.0.113.1"), "wrong-key");
        var authContext = CreateAuthorizationContext(httpContext);

        // Act
        await handler.HandleAsync(authContext);

        // Assert
        authContext.HasSucceeded.Should().BeFalse("Invalid health key was provided");
    }

    [Fact]
    public async Task HandleRequirementAsync_ExternalWithNoKey_Fails()
    {
        // Arrange
        var handler = CreateHandler();
        var httpContext = CreateHttpContext(IPAddress.Parse("203.0.113.1"));
        var authContext = CreateAuthorizationContext(httpContext);

        // Act
        await handler.HandleAsync(authContext);

        // Assert
        authContext.HasSucceeded.Should().BeFalse("No health key was provided");
    }

    [Fact]
    public async Task HandleRequirementAsync_ExternalWithEmptyKey_Fails()
    {
        // Arrange
        var handler = CreateHandler();
        var httpContext = CreateHttpContext(IPAddress.Parse("203.0.113.1"), "");
        var authContext = CreateAuthorizationContext(httpContext);

        // Act
        await handler.HandleAsync(authContext);

        // Assert
        authContext.HasSucceeded.Should().BeFalse("Empty health key is not valid");
    }

    [Fact]
    public async Task HandleRequirementAsync_KeyNotConfigured_PrivateNetworkStillSucceeds()
    {
        // Arrange
        var handler = CreateHandler(healthKey: null);
        var httpContext = CreateHttpContext(IPAddress.Parse("10.0.0.1"));
        var authContext = CreateAuthorizationContext(httpContext);

        // Act
        await handler.HandleAsync(authContext);

        // Assert
        authContext.HasSucceeded.Should().BeTrue("Private network should still work without key configured");
    }

    [Fact]
    public async Task HandleRequirementAsync_KeyNotConfigured_ExternalFails()
    {
        // Arrange
        var handler = CreateHandler(healthKey: null);
        var httpContext = CreateHttpContext(IPAddress.Parse("203.0.113.1"), "some-key");
        var authContext = CreateAuthorizationContext(httpContext);

        // Act
        await handler.HandleAsync(authContext);

        // Assert
        authContext.HasSucceeded.Should().BeFalse("External requests should fail when key is not configured");
    }

    [Fact]
    public async Task HandleRequirementAsync_NoHttpContext_DoesNotSucceed()
    {
        // Arrange
        var handler = CreateHandler();
        var requirements = new[] { new HealthKeyRequirement() };
        var user = new ClaimsPrincipal(new ClaimsIdentity());
        var authContext = new AuthorizationHandlerContext(requirements, user, resource: null);

        // Act
        await handler.HandleAsync(authContext);

        // Assert
        authContext.HasSucceeded.Should().BeFalse("No HttpContext means we can't evaluate the requirement");
    }

    [Fact]
    public async Task HandleRequirementAsync_NullRemoteIpAddress_WithValidKey_Succeeds()
    {
        // Arrange
        var handler = CreateHandler();
        var httpContext = CreateHttpContext(remoteIpAddress: null, TestHealthKey);
        var authContext = CreateAuthorizationContext(httpContext);

        // Act
        await handler.HandleAsync(authContext);

        // Assert
        authContext.HasSucceeded.Should().BeTrue("Valid key should work even without remote IP");
    }

    [Fact]
    public async Task HandleRequirementAsync_NullRemoteIpAddress_WithoutKey_Fails()
    {
        // Arrange
        var handler = CreateHandler();
        var httpContext = CreateHttpContext(remoteIpAddress: null);
        var authContext = CreateAuthorizationContext(httpContext);

        // Act
        await handler.HandleAsync(authContext);

        // Assert
        authContext.HasSucceeded.Should().BeFalse("No IP and no key should fail");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Clean up environment variable after tests
            Environment.SetEnvironmentVariable(HealthKeyAuthorizationHandler.HealthKeyEnvVar, null);
        }
        base.Dispose(disposing);
    }
}
