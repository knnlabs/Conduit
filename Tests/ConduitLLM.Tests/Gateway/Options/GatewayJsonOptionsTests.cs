using System.Text.Json;

using ConduitLLM.Configuration.DTOs.BatchOperations;
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

    [Fact]
    public void VirtualKeyBatchUpdateRejectsTheRemovedPerKeyBudget()
    {
        var deserialize = () => JsonSerializer.Deserialize<VirtualKeyUpdateDto>(
            """{"virtual_key_id":42,"max_budget":100}""",
            GatewayJsonOptions.Create());

        Assert.Throws<JsonException>(deserialize);
    }

    [Fact]
    public void BatchStatusUsesTheCoreContractAndErrorsHideStackTraces()
    {
        var statusJson = JsonSerializer.Serialize(
            new BatchOperationStatus
            {
                OperationId = "batch-1",
                OperationType = "virtual_key_update",
                Status = BatchOperationStatusEnum.Running,
                CanResume = true
            },
            GatewayJsonOptions.Create());
        var errorJson = JsonSerializer.Serialize(
            new BatchItemError
            {
                Error = "failed",
                StackTrace = "server-only"
            },
            GatewayJsonOptions.Create());

        using var statusDocument = JsonDocument.Parse(statusJson);
        Assert.Equal("running", statusDocument.RootElement.GetProperty("status").GetString());
        Assert.True(statusDocument.RootElement.GetProperty("can_resume").GetBoolean());
        Assert.DoesNotContain("stack_trace", errorJson, StringComparison.Ordinal);
    }

    private sealed record WireSample(string RequestId, WireState State);

    private enum WireState
    {
        Pending,
        InProgress
    }
}
