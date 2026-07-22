using System.Reflection;
using System.Text.Json;

using ConduitLLM.Functions.Entities;
using ConduitLLM.Functions.Enums;
using ConduitLLM.Functions.Models;
using ConduitLLM.Functions.Providers.Mcp;

using Microsoft.Extensions.Logging;

using ModelContextProtocol.Protocol;

using Moq;

namespace ConduitLLM.Tests.Functions.Providers.Mcp;

public sealed class McpFunctionClientTests
{
    [Fact]
    public async Task ExecuteAsync_ToolOutsideAllowlist_ThrowsBeforeConnecting()
    {
        var client = CreateClient("""{"allowedTools":["search"]}""");
        var parameters = new Dictionary<string, object>
        {
            [McpReservedParameterKeys.ToolName] = "delete_everything"
        };

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => client.ExecuteAsync(parameters));

        Assert.Contains("not in the configured allowlist", exception.Message);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyAllowlist_RejectsEveryTool()
    {
        var client = CreateClient("""{"allowedTools":[]}""");
        var parameters = new Dictionary<string, object>
        {
            [McpReservedParameterKeys.ToolName] = "search"
        };

        await Assert.ThrowsAsync<ArgumentException>(() => client.ExecuteAsync(parameters));
    }

    [Fact]
    public void BuildResponseJson_LargeText_ReturnsBoundedValidJsonWithMarker()
    {
        var client = CreateClient("{}");
        var result = new CallToolResult
        {
            Content = [new TextContentBlock { Text = string.Concat(Enumerable.Repeat("😀", 75_000)) }]
        };

        var json = BuildResponseJson(client, result);

        Assert.True(json.Length <= 100_000);
        using var document = JsonDocument.Parse(json);
        Assert.True(document.RootElement.GetProperty("truncated").GetBoolean());
        Assert.EndsWith(
            "…[truncated]",
            document.RootElement.GetProperty("content")[0].GetString());
    }

    [Fact]
    public void BuildResponseJson_LargeStructuredContent_OmitsItAndReturnsValidJson()
    {
        var client = CreateClient("{}");
        var result = new CallToolResult
        {
            Content = [],
            StructuredContent = JsonSerializer.Deserialize<JsonElement>(
                JsonSerializer.Serialize(new { value = new string('x', 150_000) }))
        };

        var json = BuildResponseJson(client, result);

        Assert.True(json.Length <= 100_000);
        using var document = JsonDocument.Parse(json);
        Assert.True(document.RootElement.GetProperty("truncated").GetBoolean());
        Assert.True(document.RootElement.GetProperty("omittedStructuredContent").GetBoolean());
        Assert.False(document.RootElement.TryGetProperty("structuredContent", out _));
    }

    private static string BuildResponseJson(McpFunctionClient client, CallToolResult result)
    {
        var method = typeof(McpFunctionClient).GetMethod(
            "BuildResponseJson",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (string)method.Invoke(client, [result])!;
    }

    private static McpFunctionClient CreateClient(string providerSettings)
    {
        var configuration = new FunctionConfiguration
        {
            ConfigurationName = "Test MCP",
            ProviderType = FunctionProviderType.Mcp,
            Purpose = FunctionPurpose.Search,
            BaseUrl = "https://mcp.example.test",
            ProviderSettings = providerSettings
        };
        var credential = new FunctionCredential { ProviderType = FunctionProviderType.Mcp };

        return new McpFunctionClient(
            configuration,
            credential,
            httpClientFactory: null,
            Mock.Of<ILoggerFactory>());
    }
}
