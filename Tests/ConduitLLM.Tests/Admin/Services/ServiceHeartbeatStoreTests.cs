using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Admin.Services;

using FluentAssertions;

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
            InstanceId = "host_1234",
            Version = "1.2.3.4",
            UptimeSeconds = 42,
            IntervalSeconds = 30,
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
            InstanceId = "old",
            ReceivedAtUtc = now.AddSeconds(-1)
        });
        var latest = new ServiceHeartbeatSnapshot
        {
            ServiceId = "gateway",
            InstanceId = "new",
            ReceivedAtUtc = now
        };
        await _store.RecordAsync(latest);

        var result = await _store.GetAsync("gateway");

        result!.InstanceId.Should().Be("new");
    }

    [Fact]
    public async Task GetAllAsync_ReturnsEveryActiveInstance()
    {
        var now = DateTime.UtcNow;
        await _store.RecordAsync(new ServiceHeartbeatSnapshot
        {
            ServiceId = "gateway",
            InstanceId = "gateway-a",
            ReceivedAtUtc = now,
            IntervalSeconds = 30
        });
        await _store.RecordAsync(new ServiceHeartbeatSnapshot
        {
            ServiceId = "gateway",
            InstanceId = "gateway-b",
            ReceivedAtUtc = now,
            IntervalSeconds = 30
        });

        var result = await _store.GetAllAsync("gateway");

        result.Select(item => item.InstanceId)
            .Should().BeEquivalentTo("gateway-a", "gateway-b");
    }

    [Fact]
    public async Task GetAllAsync_CleansExpiredInProcessInstances()
    {
        await _store.RecordAsync(new ServiceHeartbeatSnapshot
        {
            ServiceId = "gateway",
            InstanceId = "expired",
            ReceivedAtUtc = DateTime.UtcNow.AddMinutes(-6),
            IntervalSeconds = 30
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
            InstanceId = "gateway-local",
            ReceivedAtUtc = DateTime.UtcNow,
            IntervalSeconds = 30
        };

        await store.RecordAsync(snapshot);
        var result = await store.GetAllAsync("gateway");

        result.Should().ContainSingle().Which.Should().BeSameAs(snapshot);
    }
}
