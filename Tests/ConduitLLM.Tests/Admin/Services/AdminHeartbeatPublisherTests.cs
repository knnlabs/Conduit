using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Admin.Services;
using ConduitLLM.Core.Constants;

using AwesomeAssertions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

using Moq;

namespace ConduitLLM.Tests.Admin.Services;

public class AdminHeartbeatPublisherTests
{
    [Fact]
    public async Task RecordHeartbeatAsync_RecordsReadinessAndBuildIdentity()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHealthChecks()
            .AddCheck("ready", () => HealthCheckResult.Healthy(), tags: ["ready"]);
        await using var provider = services.BuildServiceProvider();
        var store = new Mock<IServiceHeartbeatStore>();
        ServiceHeartbeatSnapshot? snapshot = null;
        store.Setup(item => item.RecordAsync(
                It.IsAny<ServiceHeartbeatSnapshot>(),
                It.IsAny<CancellationToken>()))
            .Callback<ServiceHeartbeatSnapshot, CancellationToken>(
                (value, _) => snapshot = value)
            .Returns(Task.CompletedTask);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AdminHeartbeat:IntervalSeconds"] = "30"
            })
            .Build();
        var publisher = new AdminHeartbeatPublisher(
            store.Object,
            provider.GetRequiredService<HealthCheckService>(),
            configuration,
            Mock.Of<ILogger<AdminHeartbeatPublisher>>());

        await publisher.RecordHeartbeatAsync(CancellationToken.None);

        snapshot.Should().NotBeNull();
        snapshot!.ServiceId.Should().Be(RedisKeys.ServiceHeartbeat.AdminServiceId);
        snapshot.Heartbeat.InstanceId.Should().NotBeNullOrWhiteSpace();
        snapshot.Heartbeat.Status.Should().Be("healthy");
        snapshot.Heartbeat.IntervalSeconds.Should().Be(30);
        snapshot.Heartbeat.Version.Should().NotBeNullOrWhiteSpace();
        snapshot.Heartbeat.CommitSha.Should().NotBeNullOrWhiteSpace();
        snapshot.Heartbeat.BuildTimestamp.Should().NotBeNullOrWhiteSpace();
    }
}
