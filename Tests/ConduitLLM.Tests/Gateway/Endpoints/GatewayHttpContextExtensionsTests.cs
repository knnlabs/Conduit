using System.Security.Claims;

using ConduitLLM.Gateway.Endpoints;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;

namespace ConduitLLM.Tests.Gateway.Endpoints;

public class GatewayHttpContextExtensionsTests
{
    [Fact]
    public void Accessors_PreferItemsOverClaims()
    {
        var context = ContextWithClaims(("VirtualKeyId", "11"), ("VirtualKey", "claim-key"));
        context.Items["VirtualKeyId"] = 42;
        context.Items["VirtualKey"] = "item-key";

        context.GetVirtualKeyId().Should().Be(42);
        context.GetVirtualKey().Should().Be("item-key");
    }

    [Fact]
    public void Accessors_FallBackToClaims()
    {
        var context = ContextWithClaims(("VirtualKeyId", "11"), ("VirtualKey", "claim-key"));

        context.GetVirtualKeyId().Should().Be(11);
        context.GetVirtualKey().Should().Be("claim-key");
    }

    [Fact]
    public void Accessors_ReturnNullForMissingOrInvalidValues()
    {
        var context = ContextWithClaims(("VirtualKeyId", "invalid"), ("VirtualKey", string.Empty));

        context.GetVirtualKeyId().Should().BeNull();
        context.GetVirtualKey().Should().BeNull();
    }

    private static DefaultHttpContext ContextWithClaims(params (string Type, string Value)[] claims)
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            claims.Select(claim => new Claim(claim.Type, claim.Value)), "Test"));
        return context;
    }
}
