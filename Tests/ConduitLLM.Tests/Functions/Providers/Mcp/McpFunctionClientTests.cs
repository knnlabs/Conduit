using ConduitLLM.Functions.Entities;
using ConduitLLM.Functions.Enums;
using ConduitLLM.Functions.Models;
using ConduitLLM.Functions.Providers.Mcp;

using Microsoft.Extensions.Logging;

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
