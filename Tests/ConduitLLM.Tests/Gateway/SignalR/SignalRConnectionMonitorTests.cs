using System.Net;
using System.Text.Json;

using ConduitLLM.Configuration.Options;
using ConduitLLM.Configuration.Services;
using ConduitLLM.Gateway.Services;

using FluentAssertions;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Moq;
using StackExchange.Redis;

using SignalRConnectionInfo = ConduitLLM.Gateway.Models.ConnectionInfo;

namespace ConduitLLM.Tests.Gateway.SignalR;

public class SignalRConnectionMonitorTests
{
    [Theory]
    [InlineData(7, 3, false)]
    [InlineData(7, 4, true)]
    [InlineData(8, 0, true)]
    public void SupportsHashFieldExpiration_Should_Require_Redis_7_4(
        int major,
        int minor,
        bool expected)
    {
        SignalRConnectionMonitor.SupportsHashFieldExpiration(
                new Version(major, minor),
                enabled: true)
            .Should()
            .Be(expected);
    }

    [Fact]
    public async Task OnConnection_Should_Set_And_Refresh_Hash_Field_Ttl_On_Redis_7_4()
    {
        var fixture = CreateFixture(new Version(7, 4));
        await fixture.Monitor.StartAsync(CancellationToken.None);

        await fixture.Monitor.OnConnectionAsync("connection-1", "TestHub", CreateCallerContext());
        await fixture.Monitor.RecordActivityAsync("connection-1");

        fixture.Database.Verify(db => db.HashFieldExpireAsync(
            It.IsAny<RedisKey>(),
            It.Is<RedisValue[]>(fields => fields.Length == 1 && fields[0] == "connection-1"),
            TimeSpan.FromMinutes(70),
            ExpireWhen.Always,
            CommandFlags.None), Times.Exactly(2));
    }

    [Fact]
    public async Task OnConnection_Should_Use_Periodic_Fallback_On_Older_Redis()
    {
        var fixture = CreateFixture(new Version(7, 2));
        await fixture.Monitor.StartAsync(CancellationToken.None);

        await fixture.Monitor.OnConnectionAsync("connection-1", "TestHub", CreateCallerContext());

        fixture.Database.Verify(db => db.HashFieldExpireAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<RedisValue[]>(),
            It.IsAny<TimeSpan>(),
            It.IsAny<ExpireWhen>(),
            It.IsAny<CommandFlags>()), Times.Never);
    }

    [Fact]
    public async Task Cleanup_Should_Remove_Stale_Connections_On_Unsupported_Redis()
    {
        var fixture = CreateFixture(new Version(7, 2));
        var stale = new SignalRConnectionInfo
        {
            ConnectionId = "stale-connection",
            HubName = "TestHub",
            ConnectedAt = DateTime.UtcNow.AddHours(-2),
            LastActivityAt = DateTime.UtcNow.AddHours(-2)
        };
        fixture.Database
            .Setup(db => db.HashGetAllAsync(It.IsAny<RedisKey>(), CommandFlags.None))
            .ReturnsAsync([new HashEntry("stale-connection", JsonSerializer.Serialize(stale))]);
        fixture.Server
            .Setup(server => server.Keys(
                It.IsAny<int>(),
                It.IsAny<RedisValue>(),
                It.IsAny<int>(),
                It.IsAny<long>(),
                It.IsAny<int>(),
                It.IsAny<CommandFlags>()))
            .Returns([]);
        await fixture.Monitor.StartAsync(CancellationToken.None);

        await fixture.Monitor.CleanupStaleConnectionsAsync();

        fixture.Database.Verify(db => db.HashDeleteAsync(
            It.IsAny<RedisKey>(),
            (RedisValue)"stale-connection",
            CommandFlags.None), Times.Once);
    }

    private static MonitorFixture CreateFixture(Version serverVersion)
    {
        var database = new Mock<IDatabase>();
        var server = new Mock<IServer>();
        var multiplexer = new Mock<IConnectionMultiplexer>();
        var endpoint = new DnsEndPoint("localhost", 6379);

        database
            .Setup(db => db.HashSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<RedisValue>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
        database
            .Setup(db => db.HashGetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(() => JsonSerializer.Serialize(new SignalRConnectionInfo
            {
                ConnectionId = "connection-1",
                HubName = "TestHub",
                ConnectedAt = DateTime.UtcNow,
                LastActivityAt = DateTime.UtcNow
            }));
        database
            .Setup(db => db.HashFieldExpireAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<ExpireWhen>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync([ExpireResult.Success]);
        database
            .Setup(db => db.HashDeleteAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        server.SetupGet(x => x.Version).Returns(serverVersion);
        multiplexer.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(database.Object);
        multiplexer.Setup(x => x.GetEndPoints(It.IsAny<bool>())).Returns([endpoint]);
        multiplexer.Setup(x => x.GetServer(endpoint, It.IsAny<object>())).Returns(server.Object);

        var factory = new Mock<RedisConnectionFactory>(
            Options.Create(new CacheOptions { RedisConnectionString = "localhost:6379" }),
            NullLogger<RedisConnectionFactory>.Instance);
        factory.Setup(x => x.GetConnectionAsync()).ReturnsAsync(multiplexer.Object);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SignalR:ConnectionMonitor:StaleThresholdMinutes"] = "60",
                ["SignalR:ConnectionMonitor:CleanupIntervalMinutes"] = "5",
                ["SignalR:ConnectionMonitor:EnableHashFieldExpiration"] = "true"
            })
            .Build();
        var monitor = new SignalRConnectionMonitor(
            NullLogger<SignalRConnectionMonitor>.Instance,
            configuration,
            factory.Object);

        return new MonitorFixture(monitor, database, server);
    }

    private static HubCallerContext CreateCallerContext()
    {
        var context = new Mock<HubCallerContext>();
        context.SetupGet(x => x.Items).Returns(new Dictionary<object, object?>());
        context.SetupGet(x => x.Features).Returns(new FeatureCollection());
        return context.Object;
    }

    private sealed record MonitorFixture(
        SignalRConnectionMonitor Monitor,
        Mock<IDatabase> Database,
        Mock<IServer> Server);
}
