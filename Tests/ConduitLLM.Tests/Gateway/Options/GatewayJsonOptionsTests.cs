using System.Text.Json;

using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
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
            new AsyncTaskStatus { TaskId = "request-1", State = TaskState.Processing },
            GatewayJsonOptions.Create());

        using var document = JsonDocument.Parse(json);
        Assert.Equal("request-1", document.RootElement.GetProperty("task_id").GetString());
        Assert.Equal("processing", document.RootElement.GetProperty("state").GetString());
    }

    [Fact]
    public void SerializerRejectsIntegerEnumValues()
    {
        var deserialize = () => JsonSerializer.Deserialize<AsyncTaskStatus>(
            """{"task_id":"request-1","state":1}""",
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

    [Fact]
    public void AsyncTaskStatusSerializesTheSharedCompletionTimestamp()
    {
        var completedAt = new DateTime(2026, 7, 26, 12, 30, 0, DateTimeKind.Utc);
        var json = JsonSerializer.Serialize(
            new AsyncTaskStatusResponse
            {
                TaskId = "task-1",
                Status = "completed",
                CompletedAt = completedAt
            },
            GatewayJsonOptions.Create());

        using var document = JsonDocument.Parse(json);
        Assert.Equal(
            "2026-07-26T12:30:00Z",
            document.RootElement.GetProperty("completed_at").GetString());
    }
}
