using ConduitLLM.Configuration.Utilities;

namespace ConduitLLM.Tests.Configuration.Utilities;

public sealed class VirtualKeyUtilitiesTests
{
    [Theory]
    [InlineData("ABCDEF123456", "condt_abcdef...")]
    [InlineData("abc", "condt_abc...")]
    [InlineData("", "condt_******...")]
    [InlineData(null, "condt_******...")]
    public void GenerateKeyPrefix_UsesCanonicalDisplayShape(
        string? keyHash,
        string expected)
    {
        Assert.Equal(expected, VirtualKeyUtilities.GenerateKeyPrefix(keyHash));
    }
}
