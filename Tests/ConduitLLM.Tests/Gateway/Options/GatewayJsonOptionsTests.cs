using System.Text.Json;

using ConduitLLM.Gateway.Options;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ConduitLLM.Tests.Gateway.Serialization;

public sealed class GatewayJsonOptionsTests
{
    [Fact]
    public void SerializerUsesSnakeCaseForPropertiesAndEnums()
    {
        var json = JsonSerializer.Serialize(
            new WireSample("request-1", WireState.InProgress),
            GatewayJsonOptions.Create());

        Assert.Equal(
            """{"request_id":"request-1","state":"in_progress"}""",
            json);
    }

    [Fact]
    public void SerializerRejectsIntegerEnumValues()
    {
        var deserialize = () => JsonSerializer.Deserialize<WireSample>(
            """{"request_id":"request-1","state":1}""",
            GatewayJsonOptions.Create());

        Assert.Throws<JsonException>(deserialize);
    }

    [Fact]
    public void BasicSettingsExposeTheHttpWireSerializerAsTheSharedInstance()
    {
        var builder = WebApplication.CreateBuilder();
        global::Program.ConfigureBasicSettings(builder);
        using var services = builder.Services.BuildServiceProvider();

        var shared = services.GetRequiredService<JsonSerializerOptions>();
        var http = services
            .GetRequiredService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>()
            .Value
            .SerializerOptions;

        Assert.Same(http, shared);
        Assert.Same(JsonNamingPolicy.SnakeCaseLower, shared.PropertyNamingPolicy);
    }

    private sealed record WireSample(string RequestId, WireState State);

    private enum WireState
    {
        Pending,
        InProgress
    }
}
