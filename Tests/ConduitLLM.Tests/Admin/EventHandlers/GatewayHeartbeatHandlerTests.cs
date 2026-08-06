using ConduitLLM.Admin.EventHandlers;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.Messaging;
using ConduitLLM.Core.Constants;
using ConduitLLM.Core.Events;

using AwesomeAssertions;

using Microsoft.Extensions.Logging;

using Moq;

namespace ConduitLLM.Tests.Admin.EventHandlers;

public class GatewayHeartbeatHandlerTests
{
    private readonly Mock<IServiceHeartbeatStore> _store = new();
    private readonly GatewayHeartbeatHandler _handler;

    public GatewayHeartbeatHandlerTests()
    {
        _handler = new GatewayHeartbeatHandler(_store.Object, Mock.Of<ILogger<GatewayHeartbeatHandler>>());
    }

    [Fact]
    public async Task HandleAsync_RecordsSnapshotMappedFromTheMessage()
    {
        ServiceHeartbeatSnapshot? recorded = null;
        _store
            .Setup(s => s.RecordAsync(It.IsAny<ServiceHeartbeatSnapshot>(), It.IsAny<CancellationToken>()))
            .Callback<ServiceHeartbeatSnapshot, CancellationToken>((s, _) => recorded = s)
            .Returns(Task.CompletedTask);

        var message = new GatewayHeartbeat
        {
            InstanceId = "host_1234",
            Version = "1.2.3.4",
            CommitSha = "abc123",
            BuildTimestamp = "2026-07-23T12:00:00Z",
            Status = "degraded",
            UptimeSeconds = 99,
            IntervalSeconds = 30
        };

        await _handler.HandleAsync(message, Mock.Of<IEventContext>());

        recorded.Should().NotBeNull();
        recorded!.ServiceId.Should().Be(RedisKeys.ServiceHeartbeat.GatewayServiceId);
        recorded.Heartbeat.Should().BeSameAs(message);
        recorded.Heartbeat.InstanceId.Should().Be("host_1234");
        recorded.Heartbeat.Version.Should().Be("1.2.3.4");
        recorded.Heartbeat.CommitSha.Should().Be("abc123");
        recorded.Heartbeat.BuildTimestamp.Should().Be("2026-07-23T12:00:00Z");
        recorded.Heartbeat.Status.Should().Be("degraded");
        recorded.Heartbeat.UptimeSeconds.Should().Be(99);
        recorded.Heartbeat.IntervalSeconds.Should().Be(30);
        recorded.ReportedAtUtc.Should().Be(message.Timestamp);
    }

    [Fact]
    public async Task HandleAsync_WhenStoreThrows_DoesNotPropagate()
    {
        // A failed heartbeat write must not trigger Wolverine redelivery — the handler swallows.
        _store
            .Setup(s => s.RecordAsync(It.IsAny<ServiceHeartbeatSnapshot>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("redis down"));

        var act = async () => await _handler.HandleAsync(new GatewayHeartbeat(), Mock.Of<IEventContext>());

        await act.Should().NotThrowAsync();
    }
}
