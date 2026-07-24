using ConduitLLM.Admin.Endpoints;
using ConduitLLM.Configuration.Messaging.Wolverine;
using ConduitLLM.Core.Interfaces;

using FluentAssertions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

using Moq;

using StackExchange.Redis;

namespace ConduitLLM.Tests.Admin.Endpoints;

public class HealthMonitoringDependencyTests
{
    [Fact]
    public async Task RedisWithoutConfiguration_IsExplicitlyDegraded()
    {
        var status = await HealthMonitoringEndpoints.BuildRedisStatusAsync(
            null,
            CancellationToken.None,
            configuredOverride: false);

        status.Status.Should().Be("degraded");
        status.Id.Should().Be("redis");
        status.Details.Should().NotBeNull();
    }

    [Fact]
    public async Task ConfiguredRedisWithoutConnection_IsUnhealthy()
    {
        var status = await HealthMonitoringEndpoints.BuildRedisStatusAsync(
            null,
            CancellationToken.None,
            configuredOverride: true);

        status.Status.Should().Be("unhealthy");
    }

    [Fact]
    public async Task ReachableRedis_ReportsPingAndVersion()
    {
        var database = new Mock<IDatabase>();
        database.Setup(item => item.PingAsync(It.IsAny<CommandFlags>()))
            .ReturnsAsync(TimeSpan.FromMilliseconds(2));
        var endpoint = new System.Net.DnsEndPoint("redis", 6379);
        var server = new Mock<IServer>();
        server.SetupGet(item => item.Version).Returns(new Version(8, 0, 1));
        var redis = new Mock<IConnectionMultiplexer>();
        redis.Setup(item => item.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(database.Object);
        redis.Setup(item => item.GetEndPoints(It.IsAny<bool>()))
            .Returns([endpoint]);
        redis.Setup(item => item.GetServer(endpoint, It.IsAny<object>()))
            .Returns(server.Object);

        var status = await HealthMonitoringEndpoints.BuildRedisStatusAsync(
            redis.Object,
            CancellationToken.None,
            configuredOverride: true);

        status.Status.Should().Be("healthy");
        status.Version.Should().Be("8.0.1");
        status.ResponseTime.Should().BeGreaterThanOrEqualTo(0);
    }

    [Theory]
    [InlineData("Development", "healthy")]
    [InlineData("Production", "degraded")]
    public async Task InMemoryMessaging_ReflectsEnvironment(
        string environmentName,
        string expectedStatus)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [WolverineMessagingExtensions.TransportKey] = "InMemory"
            })
            .Build();
        var environment = Mock.Of<IHostEnvironment>(
            item => item.EnvironmentName == environmentName);

        var status = await HealthMonitoringEndpoints.BuildMessagingStatusAsync(
            CreateHealthCheckService(),
            configuration,
            environment,
            CancellationToken.None);

        status.Status.Should().Be(expectedStatus);
        status.Id.Should().Be("messaging");
    }

    [Fact]
    public async Task DurableMessaging_UsesWolverineReadinessCheck()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHealthChecks()
            .AddCheck(
                "wolverine_bus",
                () => HealthCheckResult.Healthy("ready"),
                tags: ["messaging"]);
        using var provider = services.BuildServiceProvider();
        var configuration = new ConfigurationBuilder().Build();
        var environment = Mock.Of<IHostEnvironment>(
            item => item.EnvironmentName == Environments.Production);

        var status = await HealthMonitoringEndpoints.BuildMessagingStatusAsync(
            provider.GetRequiredService<HealthCheckService>(),
            configuration,
            environment,
            CancellationToken.None);

        status.Status.Should().Be("healthy");
    }

    [Fact]
    public async Task MediaStorageProbe_ReportsBackendState()
    {
        var probe = new Mock<IMediaStorageHealthProbe>();
        probe.Setup(item => item.ProbeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MediaStorageHealthProbeResult(
                "degraded",
                "in-memory",
                "ephemeral",
                true));

        var status = await HealthMonitoringEndpoints.BuildMediaStorageStatusAsync(
            probe.Object,
            CancellationToken.None);

        status.Status.Should().Be("degraded");
        status.Id.Should().Be("media-storage");
    }

    [Fact]
    public async Task FailedMediaStorageProbe_IsUnhealthy()
    {
        var probe = new Mock<IMediaStorageHealthProbe>();
        probe.Setup(item => item.ProbeAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("bucket unavailable"));

        var status = await HealthMonitoringEndpoints.BuildMediaStorageStatusAsync(
            probe.Object,
            CancellationToken.None);

        status.Status.Should().Be("unhealthy");
    }

    private static HealthCheckService CreateHealthCheckService()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHealthChecks();
        return services.BuildServiceProvider().GetRequiredService<HealthCheckService>();
    }
}
