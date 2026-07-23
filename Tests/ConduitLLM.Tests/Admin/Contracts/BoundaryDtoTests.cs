using System.Text.Json;

using ConduitLLM.Admin.DTOs;
using ConduitLLM.Admin.Extensions;
using ConduitLLM.Functions.Entities;

using FluentAssertions;

namespace ConduitLLM.Tests.Admin.Contracts;

[Trait("Category", "Unit")]
[Trait("Component", "OpenApi")]
public sealed class BoundaryDtoTests
{
    [Fact]
    public void FunctionExecutionMapping_ParsesJsonAndSafelyPreservesMalformedValues()
    {
        var entity = new FunctionExecution
        {
            RequestJson = """{"query":"weather"}""",
            ResponseJson = "not-json",
            CostCalculationDetails = "[1,2]"
        };

        var dto = entity.ToDto();

        dto.Request!.Value.GetProperty("query").GetString().Should().Be("weather");
        dto.Response!.Value.ValueKind.Should().Be(JsonValueKind.String);
        dto.Response.Value.GetString().Should().Be("not-json");
        dto.CostCalculation!.Value.GetArrayLength().Should().Be(2);
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
}
