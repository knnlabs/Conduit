using System.Text.Json;
using ConduitLLM.Functions.Entities;
using ConduitLLM.Functions.Enums;
using ConduitLLM.Functions.Interfaces;
using ConduitLLM.Functions.Providers.Tavily;
using Microsoft.Extensions.Logging;
using Moq;

namespace ConduitLLM.Tests.Functions.Providers.Tavily;

public class TavilyClientUsageTests
{
    [Fact]
    public void CalculateUsageFromResponse_JsonElementAutoParameters_TracksSurchargeMetadata()
    {
        var client = new TavilyClient(
            new FunctionConfiguration
            {
                ConfigurationName = "Tavily",
                ProviderType = FunctionProviderType.Tavily,
                Purpose = FunctionPurpose.Search
            },
            new FunctionCredential { ProviderType = FunctionProviderType.Tavily },
            null,
            Mock.Of<ILogger<TavilyClient>>());
        var parameters = JsonSerializer.Deserialize<Dictionary<string, object>>(
            """{"auto_parameters":true}""")!;
        var result = new FunctionExecutionResult
        {
            IsSuccess = true,
            ResponseJson = """{"request_id":"request-1","results":[]}"""
        };

        var usage = client.CalculateUsageFromResponse(parameters, result);

        Assert.True(Assert.IsType<bool>(usage.Metadata!["autoParametersEnabled"]));
    }
}
