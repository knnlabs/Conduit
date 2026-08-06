using System.Text.Json;

using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Admin.Services;
using ConduitLLM.Core.Events;

using AwesomeAssertions;

using Microsoft.Extensions.Logging;

using Moq;

using StackExchange.Redis;

namespace ConduitLLM.Tests.Admin.Services;

public class ServiceHeartbeatStoreTests
{
    // Constructed without a Redis connection, so every test exercises the in-process fallback
    // path (the single-instance / Redis-absent behavior).
    private readonly ServiceHeartbeatStore _store =
        new(Mock.Of<ILogger<ServiceHeartbeatStore>>());

    [Fact]
    public async Task GetAsync_WhenNothingRecorded_ReturnsNull()
    {
        var result = await _store.GetAsync("gateway");

        result.Should().BeNull();
    }

    [Fact]
    public async Task RecordAsync_ThenGetAsync_ReturnsTheStoredSnapshot()
    {
        var now = DateTime.UtcNow;
        var snapshot = new ServiceHeartbeatSnapshot
        {
            ServiceId = "gateway",
            Heartbeat = Heartbeat("host_1234", version: "1.2.3.4", uptimeSeconds: 42),
            ReportedAtUtc = now,
            ReceivedAtUtc = now
        };

        await _store.RecordAsync(snapshot);
        var result = await _store.GetAsync("gateway");

        result.Should().BeSameAs(snapshot);
    }

    [Fact]
    public async Task GetAsync_ForADifferentServiceId_ReturnsNull()
    {
        await _store.RecordAsync(new ServiceHeartbeatSnapshot { ServiceId = "gateway" });

        var result = await _store.GetAsync("some-other-service");

        result.Should().BeNull();
    }

    [Fact]
    public async Task RecordAsync_Twice_KeepsTheLatestSnapshot()
    {
        var now = DateTime.UtcNow;
        await _store.RecordAsync(new ServiceHeartbeatSnapshot
        {
            ServiceId = "gateway",
            Heartbeat = Heartbeat("old"),
            ReceivedAtUtc = now.AddSeconds(-1)
        });
        var latest = new ServiceHeartbeatSnapshot
        {
            ServiceId = "gateway",
            Heartbeat = Heartbeat("new"),
            ReceivedAtUtc = now
        };
        await _store.RecordAsync(latest);

        var result = await _store.GetAsync("gateway");

        result!.Heartbeat.InstanceId.Should().Be("new");
    }

    [Fact]
    public async Task GetAllAsync_ReturnsEveryActiveInstance()
    {
        var now = DateTime.UtcNow;
        await _store.RecordAsync(new ServiceHeartbeatSnapshot
        {
            ServiceId = "gateway",
            Heartbeat = Heartbeat("gateway-a"),
            ReceivedAtUtc = now
        });
        await _store.RecordAsync(new ServiceHeartbeatSnapshot
        {
            ServiceId = "gateway",
            Heartbeat = Heartbeat("gateway-b"),
            ReceivedAtUtc = now
        });

        var result = await _store.GetAllAsync("gateway");

        result.Select(item => item.Heartbeat.InstanceId)
            .Should().BeEquivalentTo("gateway-a", "gateway-b");
    }

    [Fact]
    public async Task GetAllAsync_CleansExpiredInProcessInstances()
    {
        await _store.RecordAsync(new ServiceHeartbeatSnapshot
        {
            ServiceId = "gateway",
            Heartbeat = Heartbeat("expired"),
            ReceivedAtUtc = DateTime.UtcNow.AddMinutes(-6)
        });

        (await _store.GetAllAsync("gateway")).Should().BeEmpty();
    }

    [Theory]
    [InlineData(0, 300)]
    [InlineData(30, 300)]
    [InlineData(120, 1200)]
    [InlineData(3600, 1800)]
    public void CalculateTtl_IsCadenceAwareAndBounded(double intervalSeconds, double expectedSeconds)
    {
        ServiceHeartbeatStore.CalculateTtl(intervalSeconds).TotalSeconds
            .Should().Be(expectedSeconds);
    }

    [Fact]
    public async Task GetAllAsync_WhenRedisIndexIsEmpty_UsesFreshInProcessSnapshot()
    {
        var database = new Mock<IDatabase>();
        database.Setup(item => item.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<bool>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
        database.Setup(item => item.SetAddAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
        database.Setup(item => item.KeyExpireAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<ExpireWhen>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
        database.Setup(item => item.SetMembersAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync([]);
        var redis = new Mock<IConnectionMultiplexer>();
        redis.Setup(item => item.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(database.Object);
        var store = new ServiceHeartbeatStore(
            Mock.Of<ILogger<ServiceHeartbeatStore>>(),
            redis.Object);
        var snapshot = new ServiceHeartbeatSnapshot
        {
            ServiceId = "gateway",
            Heartbeat = Heartbeat("gateway-local"),
            ReceivedAtUtc = DateTime.UtcNow
        };

        await store.RecordAsync(snapshot);
        var result = await store.GetAllAsync("gateway");

        result.Should().ContainSingle().Which.Should().BeSameAs(snapshot);
    }

    [Fact]
    public void DeserializeSnapshot_ReadsLegacyFlatRedisShape()
    {
        const string json = """
            {
              "ServiceId": "gateway",
              "InstanceId": "legacy-a",
              "Version": "2.9.0",
              "CommitSha": "oldsha",
              "BuildTimestamp": "2026-01-01T00:00:00Z",
              "Status": "degraded",
              "UptimeSeconds": 123,
              "IntervalSeconds": 30,
              "ReportedAtUtc": "2026-07-23T11:59:59Z",
              "ReceivedAtUtc": "2026-07-23T12:00:00Z"
            }
            """;

        var snapshot = ServiceHeartbeatStore.DeserializeSnapshot(json);

        snapshot.Should().NotBeNull();
        snapshot!.ServiceId.Should().Be("gateway");
        snapshot.Heartbeat.InstanceId.Should().Be("legacy-a");
        snapshot.Heartbeat.Status.Should().Be("degraded");
        snapshot.Heartbeat.Timestamp.Should().Be(
            new DateTime(2026, 7, 23, 11, 59, 59, DateTimeKind.Utc));
    }

    [Fact]
    public void SnapshotSerialization_EmbedsHeartbeatPayload()
    {
        var snapshot = new ServiceHeartbeatSnapshot
        {
            ServiceId = "gateway",
            Heartbeat = Heartbeat("gateway-a"),
            ReceivedAtUtc = DateTime.UtcNow
        };

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(snapshot));

        document.RootElement.TryGetProperty("Heartbeat", out var heartbeat).Should().BeTrue();
        heartbeat.GetProperty("InstanceId").GetString().Should().Be("gateway-a");
        document.RootElement.TryGetProperty("InstanceId", out _).Should().BeFalse();
    }

    private static GatewayHeartbeat Heartbeat(
        string instanceId,
        string version = "3.0.0",
        double uptimeSeconds = 0) =>
        new()
        {
            InstanceId = instanceId,
            Version = version,
            UptimeSeconds = uptimeSeconds,
            IntervalSeconds = 30
        };
}
