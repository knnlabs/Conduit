using System.Text.Json;

using ConduitLLM.Core.Models.SignalR;
using ConduitLLM.Gateway.Models;
using ConduitLLM.Gateway.Services;

using AwesomeAssertions;

namespace ConduitLLM.Tests.Gateway.SignalR;

public sealed class SignalRMessageQueueSerializationTests
{
    private static readonly JsonSerializerOptions Options =
        SignalRMessageQueueService.CreateQueueSerializerOptions();

    [Fact]
    public void TaskProgressMessage_WritesOnlySurvivingDiscriminator()
    {
        SignalRMessage message = new TaskProgressMessage
        {
            TaskId = "task-1",
            ProgressPercentage = 50,
            StatusMessage = "Generating",
            EstimatedSecondsRemaining = 30
        };

        var json = JsonSerializer.Serialize(message, Options);

        json.Should().Contain("\"$signalRMessageType\":\"gateway.task-progress\"");
        json.Should().NotContain("\"core.task-progress\"");
    }

    [Fact]
    public void LegacyCoreDiscriminator_RemainsReadable()
    {
        const string json =
            """
            {
              "$signalRMessageType": "core.task-progress",
              "taskId": "task-1",
              "progressPercentage": 50,
              "status": "running",
              "message": "Generating"
            }
            """;

        var message = JsonSerializer.Deserialize<SignalRMessage>(json, Options);

        var legacy = message.Should().BeOfType<LegacyCoreTaskProgressMessage>().Subject;
        legacy.TaskId.Should().Be("task-1");
        legacy.Status.Should().Be("running");
    }

    [Fact]
    public void GatewayDiscriminator_ReadsSurvivingCoreType()
    {
        const string json =
            """
            {
              "$signalRMessageType": "gateway.task-progress",
              "taskId": "task-1",
              "progressPercentage": 50,
              "statusMessage": "Generating",
              "estimatedSecondsRemaining": 30
            }
            """;

        var message = JsonSerializer.Deserialize<SignalRMessage>(json, Options);

        var progress = message.Should().BeOfType<TaskProgressMessage>().Subject;
        progress.StatusMessage.Should().Be("Generating");
        progress.EstimatedSecondsRemaining.Should().Be(30);
    }
}
