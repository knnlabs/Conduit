using Microsoft.AspNetCore.SignalR.Client;
using FluentAssertions;
using Xunit;
using ConduitLLM.IntegrationTests.Infrastructure;
using System.Collections.Concurrent;

namespace ConduitLLM.IntegrationTests.Tests;

[Collection("SignalR Redis Collection")]
[Trait("Category", "Integration")]
[Trait("Component", "SignalR")]
[Trait("Feature", "RedisBackplane")]
public class SignalRRedisBackplaneTests : SignalRIntegrationTestBase
{
    public SignalRRedisBackplaneTests(RedisTestContainerFixture redisFixture)
        : base(redisFixture) { }

    #region Failover Tests

    [Fact]
    public async Task SignalR_Continues_Operating_When_Redis_Temporarily_Unavailable()
    {
        // Arrange
        var serverUrls = await HostFactory!.CreateServersAsync(2, RedisFixture.ConnectionString);

        var conn1 = CreateConnection(serverUrls[0], "/testhub");
        var conn2 = CreateConnection(serverUrls[1], "/testhub");

        await conn1.StartAsync();
        await conn2.StartAsync();

        // Act - Stop Redis
        await RedisFixture.StopAsync();
        await Task.Delay(500); // Allow some time for circuit breaker to engage

        // Assert - Local operations should still work
        var result = await conn1.InvokeAsync<string>("Echo", "Test message");
        result.Should().Be("Echo: Test message");

        // Cleanup
        await RedisFixture.RestartAsync();
    }

    [Fact]
    public async Task Messages_Delivered_Across_Servers_After_Redis_Reconnection()
    {
        // Arrange
        var serverUrls = await HostFactory!.CreateServersAsync(2, RedisFixture.ConnectionString);

        var conn1 = CreateConnection(serverUrls[0], "/testhub");
        var conn2 = CreateConnection(serverUrls[1], "/testhub");

        await conn1.StartAsync();
        await conn2.StartAsync();

        // First verify cross-server works
        await WaitForBackplaneDeliveryAsync(
            conn1,
            conn2,
            "Before outage",
            TimeSpan.FromSeconds(5));

        // Stop Redis
        await RedisFixture.StopAsync();
        await Task.Delay(1000);

        // Restart Redis
        await RedisFixture.RestartAsync();

        // Act & Assert - poll delivery because StackExchange.Redis reconnects
        // asynchronously after the container is accepting connections again.
        var result = await WaitForBackplaneDeliveryAsync(
            conn1,
            conn2,
            "Cross-server after reconnect",
            TimeSpan.FromSeconds(15));
        result.message.Should().Be("Cross-server after reconnect");
    }

    [Fact]
    public async Task Multi_Server_Message_Delivery_Works_With_Redis_Backplane()
    {
        // Arrange - 3 servers for thorough testing
        var serverUrls = await HostFactory!.CreateServersAsync(3, RedisFixture.ConnectionString);

        var connections = new List<HubConnection>();
        var receivedMessages = new ConcurrentBag<(string message, string sender)>();

        foreach (var url in serverUrls)
        {
            var conn = CreateConnection(url, "/testhub");
            conn.On<string, string>("ReceiveMessage", (msg, sender) =>
                receivedMessages.Add((msg, sender)));
            connections.Add(conn);
        }

        // Connect all
        await Task.WhenAll(connections.Select(c => c.StartAsync()));
        await Task.Delay(1000); // Allow connections to stabilize

        // Act - Broadcast from server 1
        await connections[0].InvokeAsync("BroadcastMessage", "Hello from server 1");

        // Wait for propagation
        await Task.Delay(3000);

        // Assert - All 3 clients (including sender) should receive
        receivedMessages.Count.Should().Be(3);
        receivedMessages.All(m => m.message == "Hello from server 1").Should().BeTrue();
    }

    [Fact]
    public async Task Graceful_Degradation_When_Backplane_Not_Configured()
    {
        // Arrange - Start without Redis to test graceful degradation
        var serverUrls = await HostFactory!.CreateServersAsync(1, redisConnectionString: null);

        var conn1 = CreateConnection(serverUrls[0], "/testhub");
        var conn2 = CreateConnection(serverUrls[0], "/testhub"); // Same server

        var received = CreateMessageWaiter<(string, string)>();
        conn2.On<string, string>("ReceiveMessage", (msg, sender) =>
            received.TrySetResult((msg, sender)));

        await conn1.StartAsync();
        await conn2.StartAsync();

        // Act - Local broadcast should work
        await conn1.InvokeAsync("BroadcastMessage", "Local test");

        // Assert
        var result = await WaitForMessageAsync(received, 5000);
        result.Item1.Should().Be("Local test");
    }

    [Fact]
    public async Task SignalR_Recovers_After_Brief_Redis_Outage()
    {
        // Arrange
        var serverUrls = await HostFactory!.CreateServersAsync(2, RedisFixture.ConnectionString);

        var conn1 = CreateConnection(serverUrls[0], "/testhub");
        var conn2 = CreateConnection(serverUrls[1], "/testhub");

        await conn1.StartAsync();
        await conn2.StartAsync();

        // Verify cross-server works before outage
        await WaitForBackplaneDeliveryAsync(
            conn1,
            conn2,
            "Before outage",
            TimeSpan.FromSeconds(5));

        // Simulate brief Redis outage (3 seconds)
        await RedisFixture.StopAsync();
        await Task.Delay(3000);
        await RedisFixture.RestartAsync();

        // Act & Assert
        var result = await WaitForBackplaneDeliveryAsync(
            conn1,
            conn2,
            "After recovery",
            TimeSpan.FromSeconds(15));
        result.Item1.Should().Be("After recovery");
    }

    #endregion

    private static async Task<(string message, string sender)> WaitForBackplaneDeliveryAsync(
        HubConnection sendingConnection,
        HubConnection receivingConnection,
        string message,
        TimeSpan timeout)
    {
        var received = new TaskCompletionSource<(string message, string sender)>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = receivingConnection.On<string, string>(
            "ReceiveMessage",
            (receivedMessage, sender) =>
            {
                if (receivedMessage == message)
                {
                    received.TrySetResult((receivedMessage, sender));
                }
            });

        var deadline = DateTime.UtcNow + timeout;
        Exception? lastError = null;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                await sendingConnection.InvokeAsync("BroadcastMessage", message);
            }
            catch (Exception exception)
            {
                lastError = exception;
            }

            var completed = await Task.WhenAny(received.Task, Task.Delay(500));
            if (completed == received.Task)
            {
                return await received.Task;
            }
        }

        throw new TimeoutException(
            $"Message '{message}' was not delivered through the Redis backplane within {timeout}.",
            lastError);
    }
}
