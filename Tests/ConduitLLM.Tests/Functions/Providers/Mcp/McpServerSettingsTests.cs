using ConduitLLM.Functions.Providers.Mcp;

namespace ConduitLLM.Tests.Functions.Providers.Mcp;

public class McpServerSettingsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{ not valid json")]
    public void Parse_ReturnsDefaults_ForNullOrBlankOrInvalid(string? json)
    {
        var settings = McpServerSettings.Parse(json);

        Assert.Null(settings.AllowedTools);
        Assert.Null(settings.AllowedToolSet); // null => all tools permitted
        Assert.False(settings.AllowPrivateNetwork);
    }

    [Fact]
    public void Parse_ReadsAllowlistAndAuthAndNetworkFlag()
    {
        const string json = """
        {
            "allowedTools": ["search", "fetch"],
            "authScheme": "Bearer",
            "authHeader": "Authorization",
            "allowPrivateNetwork": true
        }
        """;

        var settings = McpServerSettings.Parse(json);

        Assert.Equal(new[] { "search", "fetch" }, settings.AllowedTools);
        Assert.NotNull(settings.AllowedToolSet);
        Assert.Contains("search", settings.AllowedToolSet!);
        Assert.DoesNotContain("other", settings.AllowedToolSet!);
        Assert.Equal("Bearer", settings.AuthScheme);
        Assert.Equal("Authorization", settings.AuthHeader);
        Assert.True(settings.AllowPrivateNetwork);
    }

    [Fact]
    public void Parse_EmptyAllowlist_DeniesAllTools()
    {
        var settings = McpServerSettings.Parse("""{ "allowedTools": [] }""");

        Assert.NotNull(settings.AllowedToolSet);
        Assert.Empty(settings.AllowedToolSet!);
    }
}
