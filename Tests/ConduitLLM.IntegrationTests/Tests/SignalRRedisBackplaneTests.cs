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
        var firstReceived = CreateMessageWaiter<(string message, string sender)>();
        conn2.On<string, string>("ReceiveMessage", (msg, sender) =>
            firstReceived.TrySetResult((msg, sender)));

        await conn1.InvokeAsync("BroadcastMessage", "Before outage");
        await WaitForMessageAsync(firstReceived, 5000, "Should receive before outage");

        // Stop Redis
        await RedisFixture.StopAsync();
        await Task.Delay(1000);

        // Restart Redis
        await RedisFixture.RestartAsync();
        await Task.Delay(3000); // Allow backplane to reconnect

        // Act - Send message across servers after recovery
        var received = CreateMessageWaiter<(string message, string sender)>();
        conn2.On<string, string>("ReceiveMessage", (msg, sender) =>
            received.TrySetResult((msg, sender)));

        await conn1.InvokeAsync("BroadcastMessage", "Cross-server after reconnect");

        // Assert
        var result = await WaitForMessageAsync(received, 15000);
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
        var received1 = CreateMessageWaiter<(string, string)>();
        conn2.On<string, string>("ReceiveMessage", (msg, sender) =>
            received1.TrySetResult((msg, sender)));

        await conn1.InvokeAsync("BroadcastMessage", "Before outage");
        await WaitForMessageAsync(received1, 5000, "Should receive before outage");

        // Simulate brief Redis outage (3 seconds)
        await RedisFixture.StopAsync();
        await Task.Delay(3000);
        await RedisFixture.RestartAsync();
        await Task.Delay(5000); // Allow reconnection

        // Act - Test cross-server after recovery
        var received2 = CreateMessageWaiter<(string, string)>();
        conn2.On<string, string>("ReceiveMessage", (msg, sender) =>
            received2.TrySetResult((msg, sender)));

        await conn1.InvokeAsync("BroadcastMessage", "After recovery");

        // Assert
        var result = await WaitForMessageAsync(received2, 10000, "Should receive after Redis recovery");
        result.Item1.Should().Be("After recovery");
    }

    #endregion
}
