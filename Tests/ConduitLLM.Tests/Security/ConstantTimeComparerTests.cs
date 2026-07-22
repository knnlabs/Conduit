using ConduitLLM.Security.Cryptography;

namespace ConduitLLM.Tests.Security;

public class ConstantTimeComparerTests
{
    [Theory]
    [InlineData("")]
    [InlineData("service-secret")]
    [InlineData("pässphrase-🔑")]
    public void Equals_WithIdenticalValues_ReturnsTrue(string value)
    {
        Assert.True(ConstantTimeComparer.Equals(value, value));
    }

    [Theory]
    [InlineData("service-secret", "xervice-secret")]
    [InlineData("service-secret", "service-secrex")]
    [InlineData("service-secret", "service-secret-longer")]
    [InlineData("service-secret", "SERVICE-SECRET")]
    [InlineData("é", "é")]
    public void Equals_WithDifferentValues_ReturnsFalse(string provided, string expected)
    {
        Assert.False(ConstantTimeComparer.Equals(provided, expected));
    }

    [Fact]
    public void Equals_WithNullValue_ReturnsFalse()
    {
        Assert.False(ConstantTimeComparer.Equals(null, "secret"));
        Assert.False(ConstantTimeComparer.Equals("secret", null));
        Assert.False(ConstantTimeComparer.Equals(null, null));
    }
}
