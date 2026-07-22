using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;
using ConduitLLM.Functions.Entities;
using ConduitLLM.Functions.Enums;
using ConduitLLM.Functions.Interfaces;
using ConduitLLM.Functions.Models;
using Microsoft.Extensions.Logging;
using Moq;

namespace ConduitLLM.Tests.Core;

/// <summary>
/// Verifies that a single MCP function configuration expands into one LLM tool per
/// server-advertised tool, and that the tool names emitted for the model exactly match the keys of
/// the routing map (so tool calls resolve back to the right server + tool).
/// </summary>
public class FunctionDiscoveryServiceMcpTests
{
    private const int McpConfigId = 42;

    private static FunctionConfiguration McpConfig() => new()
    {
        Id = McpConfigId,
        ConfigurationName = "Acme MCP",
        ProviderType = FunctionProviderType.Mcp,
        Purpose = FunctionPurpose.Search,
        IsEnabled = true,
        BaseUrl = "https://mcp.acme.com"
    };

    private static (FunctionDiscoveryService service, Mock<IFunctionClientFactory> factory) BuildService(
        IReadOnlyList<DiscoveredTool> discovered)
    {
        var configRepo = new Mock<IFunctionConfigurationRepository>();
        configRepo
            .Setup(r => r.GetByIdsAsync(It.IsAny<List<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FunctionConfiguration> { McpConfig() });

        var fakeClient = new FakeDynamicClient(discovered);
        var factory = new Mock<IFunctionClientFactory>();
        factory
            .Setup(f => f.GetClientAsync(FunctionProviderType.Mcp, McpConfigId))
            .ReturnsAsync(fakeClient);

        var service = new FunctionDiscoveryService(
            configRepo.Object,
            factory.Object,
            cacheService: null,
            Mock.Of<ILogger<FunctionDiscoveryService>>());

        return (service, factory);
    }

    [Fact]
    public async Task GetTools_ExpandsMcpConfigurationIntoOneToolPerServerTool()
    {
        var discovered = new List<DiscoveredTool>
        {
            new() { Name = "search", Description = "Search the web" },
            new() { Name = "fetch", Description = "Fetch a URL" }
        };
        var (service, _) = BuildService(discovered);

        var tools = await service.GetToolsForFunctionConfigurationsAsync(new List<int> { McpConfigId }, virtualKeyId: 1);

        Assert.Equal(2, tools.Count);
        Assert.Contains(tools, t => t.Function.Name == "acme_mcp__search");
        Assert.Contains(tools, t => t.Function.Name == "acme_mcp__fetch");
    }

    [Fact]
    public async Task GetMapping_RoutesEachToolNameBackToConfigAndProviderToolName()
    {
        var discovered = new List<DiscoveredTool>
        {
            new() { Name = "search" },
            new() { Name = "fetch" }
        };
        var (service, _) = BuildService(discovered);

        var map = await service.GetFunctionNameToIdMappingAsync(new List<int> { McpConfigId });

        Assert.Equal(new FunctionRoute(McpConfigId, "search"), map["acme_mcp__search"]);
        Assert.Equal(new FunctionRoute(McpConfigId, "fetch"), map["acme_mcp__fetch"]);
    }

    [Fact]
    public async Task ToolNames_AreConsistentBetweenDiscoveryAndRoutingMap()
    {
        var discovered = new List<DiscoveredTool>
        {
            new() { Name = "do-thing" },
            new() { Name = "Another Tool" }
        };
        var (service, _) = BuildService(discovered);

        var tools = await service.GetToolsForFunctionConfigurationsAsync(new List<int> { McpConfigId }, virtualKeyId: 1);
        var map = await service.GetFunctionNameToIdMappingAsync(new List<int> { McpConfigId });

        // Every tool the model can see must be routable.
        foreach (var tool in tools)
        {
            Assert.True(map.ContainsKey(tool.Function.Name),
                $"Tool '{tool.Function.Name}' is offered to the model but has no route.");
        }

        Assert.Equal(tools.Count, map.Count);
    }

    /// <summary>Fake client that reports a fixed dynamic tool set; execution paths are unused here.</summary>
    private sealed class FakeDynamicClient : IFunctionClient, IDynamicToolProvider
    {
        private readonly IReadOnlyList<DiscoveredTool> _tools;

        public FakeDynamicClient(IReadOnlyList<DiscoveredTool> tools) => _tools = tools;

        public FunctionProviderType ProviderType => FunctionProviderType.Mcp;
        public string ProviderName => "MCP";

        public Task<IReadOnlyList<DiscoveredTool>> ListToolsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_tools);

        public Task<FunctionAuthenticationResult> VerifyAuthenticationAsync(string? apiKey = null, CancellationToken cancellationToken = default)
            => Task.FromResult(FunctionAuthenticationResult.Success("ok", 1));

        public Task<FunctionExecutionResult> ExecuteAsync(Dictionary<string, object> parameters, string? apiKey = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new FunctionExecutionResult { IsSuccess = true });

        public FunctionExecutionUsage CalculateUsageFromResponse(Dictionary<string, object> parameters, FunctionExecutionResult result)
            => new() { ResultCount = 1 };
    }
}
