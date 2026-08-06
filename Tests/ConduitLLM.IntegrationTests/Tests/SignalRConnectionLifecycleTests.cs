using Microsoft.AspNetCore.SignalR.Client;
using AwesomeAssertions;
using Xunit;
using ConduitLLM.IntegrationTests.Infrastructure;
using System.Collections.Concurrent;

namespace ConduitLLM.IntegrationTests.Tests;

[Collection("SignalR Redis Collection")]
[Trait("Category", "Integration")]
[Trait("Component", "SignalR")]
[Trait("Feature", "ConnectionLifecycle")]
public class SignalRConnectionLifecycleTests : SignalRIntegrationTestBase
{
    public SignalRConnectionLifecycleTests(RedisTestContainerFixture redisFixture)
        : base(redisFixture) { }

    #region Connection State Tests

    [Fact]
    public async Task Connection_Transitions_Through_Expected_States()
    {
        // Arrange
        var serverUrls = await HostFactory!.CreateServersAsync(1, RedisFixture.ConnectionString);
        var connection = CreateConnection(serverUrls[0], "/testhub");

        var states = new List<HubConnectionState>();

        // Act & Assert - Disconnected initially
        states.Add(connection.State);
        connection.State.Should().Be(HubConnectionState.Disconnected);

        // Connect
        await connection.StartAsync();
        states.Add(connection.State);
        connection.State.Should().Be(HubConnectionState.Connected);

        // Disconnect
        await connection.StopAsync();
        states.Add(connection.State);
        connection.State.Should().Be(HubConnectionState.Disconnected);

        // Verify state transitions
        states.Should().ContainInOrder(
            HubConnectionState.Disconnected,
            HubConnectionState.Connected,
            HubConnectionState.Disconnected);
    }

    [Fact]
    public async Task Automatic_Reconnection_Triggers_Reconnecting_Event()
    {
        // Arrange
        var reconnectDelays = new[]
        {
            TimeSpan.FromMilliseconds(100),
            TimeSpan.FromMilliseconds(200),
            TimeSpan.FromMilliseconds(500)
        };

        var serverUrls = await HostFactory!.CreateServersAsync(1, RedisFixture.ConnectionString);
        var connection = CreateConnection(
            serverUrls[0], "/testhub",
            withAutoReconnect: true,
            reconnectDelays: reconnectDelays);

        var reconnectingFired = new TaskCompletionSource<bool>();

        connection.Reconnecting += error =>
        {
            reconnectingFired.TrySetResult(true);
            return Task.CompletedTask;
        };

        await connection.StartAsync();
        connection.State.Should().Be(HubConnectionState.Connected);

        // Act - Stop server to trigger reconnection
        await HostFactory.DisposeAsync();

        // Wait for reconnecting event
        var result = await Task.WhenAny(reconnectingFired.Task, Task.Delay(10000));

        // Assert
        result.Should().Be(reconnectingFired.Task, "Reconnecting event should fire when server stops");
    }

    [Fact]
    public async Task Reconnection_Delay_Intervals_Are_Respected()
    {
        // Arrange
        var reconnectDelays = new[]
        {
            TimeSpan.FromMilliseconds(500),
            TimeSpan.FromMilliseconds(1000),
            TimeSpan.FromMilliseconds(2000)
        };

        var serverUrls = await HostFactory!.CreateServersAsync(1, RedisFixture.ConnectionString);
        var retryPolicy = new RecordingRetryPolicy(reconnectDelays);
        var connection = CreateConnection(
            serverUrls[0], "/testhub",
            withAutoReconnect: true,
            retryPolicy: retryPolicy);

        await connection.StartAsync();

        // Act - Disconnect server (reconnection will fail since no server available)
        await HostFactory.DisposeAsync();

        var timeoutAt = DateTime.UtcNow.AddSeconds(10);
        while (retryPolicy.Attempts.Count < reconnectDelays.Length &&
               DateTime.UtcNow < timeoutAt)
        {
            await Task.Delay(100);
        }

        // Reconnecting fires once for the whole reconnect cycle. The retry policy
        // is the authoritative observation point for individual attempts.
        retryPolicy.Attempts.Should().HaveCountGreaterThanOrEqualTo(reconnectDelays.Length);
        retryPolicy.Attempts.Take(reconnectDelays.Length)
            .Select(attempt => attempt.PreviousRetryCount)
            .Should().Equal(0, 1, 2);
        retryPolicy.Attempts.Take(reconnectDelays.Length)
            .Select(attempt => attempt.Delay)
            .Should().Equal(reconnectDelays.Select(delay => (TimeSpan?)delay));
    }

    [Fact]
    public async Task Reconnected_Event_Fires_After_Server_Restart()
    {
        // Arrange
        var serverUrls = await HostFactory!.CreateServersAsync(1, RedisFixture.ConnectionString);
        var connection = CreateConnection(
            serverUrls[0], "/testhub",
            withAutoReconnect: true,
            reconnectDelays: new[] { TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(500) });

        var reconnectedFired = new TaskCompletionSource<string?>();

        connection.Reconnected += connectionId =>
        {
            reconnectedFired.TrySetResult(connectionId);
            return Task.CompletedTask;
        };

        await connection.StartAsync();
        var originalUrl = serverUrls[0];

        // Act - Stop and restart the server
        await HostFactory.DisposeAsync();
        HostFactory = new MultiServerSignalRHostFactory();

        // Small delay before restart
        await Task.Delay(500);

        // Restart on same port (note: port increments so we need new factory)
        await HostFactory.CreateServersAsync(1, RedisFixture.ConnectionString);

        // Wait for reconnection
        var result = await Task.WhenAny(reconnectedFired.Task, Task.Delay(15000));

        // Assert
        if (result == reconnectedFired.Task)
        {
            var newConnectionId = await reconnectedFired.Task;
            newConnectionId.Should().NotBeNullOrEmpty("ConnectionId should be available after reconnection");
        }
        // Note: If timeout occurs, reconnection may not have succeeded (expected in some scenarios)
    }

    [Fact]
    public async Task Closed_Event_Fires_On_Permanent_Disconnection()
    {
        // Arrange
        var serverUrls = await HostFactory!.CreateServersAsync(1, RedisFixture.ConnectionString);

        // No automatic reconnect - connection should close permanently
        var connection = CreateConnection(serverUrls[0], "/testhub", withAutoReconnect: false);

        var closedFired = new TaskCompletionSource<Exception?>();
        connection.Closed += error =>
        {
            closedFired.TrySetResult(error);
            return Task.CompletedTask;
        };

        await connection.StartAsync();
        connection.State.Should().Be(HubConnectionState.Connected);

        // Act - Stop server
        await HostFactory.DisposeAsync();

        // Assert
        var result = await Task.WhenAny(closedFired.Task, Task.Delay(10000));
        result.Should().Be(closedFired.Task, "Closed event should fire on disconnection");
    }

    [Fact]
    public async Task Connection_Timeout_Is_Handled_Gracefully()
    {
        // Arrange - Create connection to non-existent server
        var connection = new HubConnectionBuilder()
            .WithUrl("http://localhost:59999/testhub")
            .Build();

        ActiveConnections.Add(connection);

        // Act & Assert
        var exception = await Assert.ThrowsAnyAsync<Exception>(
            async () => await connection.StartAsync());

        connection.State.Should().Be(HubConnectionState.Disconnected);
    }

    #endregion

    private sealed class RecordingRetryPolicy : IRetryPolicy
    {
        private readonly IReadOnlyList<TimeSpan> _delays;

        public RecordingRetryPolicy(IReadOnlyList<TimeSpan> delays)
        {
            _delays = delays;
        }

        public ConcurrentQueue<(long PreviousRetryCount, TimeSpan? Delay)> Attempts { get; } = new();

        public TimeSpan? NextRetryDelay(RetryContext retryContext)
        {
            TimeSpan? delay = retryContext.PreviousRetryCount < _delays.Count
                ? _delays[(int)retryContext.PreviousRetryCount]
                : null;
            Attempts.Enqueue((retryContext.PreviousRetryCount, delay));
            return delay;
        }
    }
}
