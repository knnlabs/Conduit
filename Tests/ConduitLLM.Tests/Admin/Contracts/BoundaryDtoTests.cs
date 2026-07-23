using System.Text.Json;

using ConduitLLM.Admin.DTOs;
using ConduitLLM.Admin.Extensions;
using ConduitLLM.Functions.Entities;
using ConduitLLM.Functions.Utilities;

using FluentAssertions;

namespace ConduitLLM.Tests.Admin.Contracts;

[Trait("Category", "Unit")]
[Trait("Component", "OpenApi")]
public sealed class BoundaryDtoTests
{
    [Fact]
    public void FunctionExecutionMapping_ProducesStructuredObjectsWithoutLeakingMalformedJson()
    {
        var entity = new FunctionExecution
        {
            FunctionConfigurationId = 42,
            State = ConduitLLM.Functions.Enums.ExecutionState.Completed,
            RequestJson = """{"query":"weather"}""",
            ResponseJson = "not-json",
            CostCalculationDetails = "[1,2]",
            EstimatedCost = 0.001m,
            ActualCost = 0.002m,
            Duration = TimeSpan.FromTicks(TimeSpan.TicksPerMillisecond * 12 + 6000),
            LeasedBy = "worker-1",
            LeaseExpiryTime = DateTime.UtcNow.AddMinutes(1)
        };

        var dto = entity.ToDto();

        dto.Input!["query"].GetString().Should().Be("weather");
        dto.Output.Should().BeNull();
        dto.Cost.Breakdown!["value"].GetArrayLength().Should().Be(2);
        dto.FunctionId.Should().Be(42);
        dto.Status.Should().Be(ConduitLLM.Functions.Enums.ExecutionState.Completed);
        dto.DurationMs.Should().Be(13);
        dto.Cost.Estimated.Should().Be(0.001m);
        dto.Cost.Actual.Should().Be(0.002m);
        dto.Cost.Currency.Should().Be("USD");
        dto.Admin.LeasedBy.Should().Be("worker-1");
        dto.Admin.LeaseExpiresAt.Should().Be(entity.LeaseExpiryTime);
    }

    [Fact]
    public void PricingTemplateConditions_PreserveBooleanAndIntegerJsonTypes()
    {
        var conditions = new PricingTemplateConditionsDto(
            Resolution: "1080p",
            WithAudio: true,
            InferenceStepsGte: 50);

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(
            conditions,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        var root = document.RootElement;

        root.GetProperty("resolution").GetString().Should().Be("1080p");
        root.GetProperty("with_audio").ValueKind.Should().Be(JsonValueKind.True);
        root.GetProperty("inference_steps_gte").GetInt32().Should().Be(50);
        root.TryGetProperty("quality", out _).Should().BeFalse();
    }

    [Fact]
    public void StructuredJson_RoundTripsNestedConfigurationObjects()
    {
        var value = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            """{"type":"object","properties":{"query":{"type":"string"}}}""")!;

        var stored = StructuredJson.SerializeObject(value);
        var restored = StructuredJson.ParseObject(stored);

        restored!["type"].GetString().Should().Be("object");
        restored["properties"].GetProperty("query").GetProperty("type").GetString()
            .Should().Be("string");
    }
}
