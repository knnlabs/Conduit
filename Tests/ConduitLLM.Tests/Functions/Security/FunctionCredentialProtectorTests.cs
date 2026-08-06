using ConduitLLM.Functions.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Moq;

namespace ConduitLLM.Tests.Functions.Security;

public class FunctionCredentialProtectorTests
{
    private static FunctionCredentialProtector Create() =>
        new(new EphemeralDataProtectionProvider(), Mock.Of<ILogger<FunctionCredentialProtector>>());

    [Fact]
    public void Protect_ThenReveal_RoundTripsPlaintext()
    {
        var protector = Create();
        const string secret = "mcp-bearer-token-123";

        var protectedValue = protector.Protect(secret);

        Assert.NotNull(protectedValue);
        Assert.NotEqual(secret, protectedValue);
        Assert.StartsWith(FunctionCredentialProtector.Prefix, protectedValue);
        Assert.Equal(secret, protector.Reveal(protectedValue));
    }

    [Fact]
    public void Reveal_PassesThroughLegacyPlaintext()
    {
        var protector = Create();

        // A value without the prefix is treated as legacy plaintext and returned unchanged.
        Assert.Equal("plain-exa-key", protector.Reveal("plain-exa-key"));
    }

    [Fact]
    public void Protect_IsIdempotent()
    {
        var protector = Create();

        var once = protector.Protect("token");
        var twice = protector.Protect(once);

        Assert.Equal(once, twice);
        Assert.Equal("token", protector.Reveal(twice));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Protect_And_Reveal_PreserveNullOrEmpty(string? value)
    {
        var protector = Create();

        Assert.Equal(value, protector.Protect(value));
        Assert.Equal(value, protector.Reveal(value));
        Assert.False(protector.IsProtected(value));
    }
}
