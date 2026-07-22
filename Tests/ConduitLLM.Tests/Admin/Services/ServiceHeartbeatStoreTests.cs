using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Admin.Services;

using FluentAssertions;

using Microsoft.Extensions.Logging;

using Moq;

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
        var snapshot = new ServiceHeartbeatSnapshot
        {
            ServiceId = "gateway",
            InstanceId = "host_1234",
            Version = "1.2.3.4",
            UptimeSeconds = 42,
            IntervalSeconds = 30,
            ReportedAtUtc = new DateTime(2026, 7, 22, 10, 0, 0, DateTimeKind.Utc),
            ReceivedAtUtc = new DateTime(2026, 7, 22, 10, 0, 1, DateTimeKind.Utc)
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
        await _store.RecordAsync(new ServiceHeartbeatSnapshot { ServiceId = "gateway", InstanceId = "old" });
        var latest = new ServiceHeartbeatSnapshot { ServiceId = "gateway", InstanceId = "new" };
        await _store.RecordAsync(latest);

        var result = await _store.GetAsync("gateway");

        result!.InstanceId.Should().Be("new");
    }
}
