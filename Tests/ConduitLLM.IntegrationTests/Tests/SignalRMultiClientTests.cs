using Microsoft.AspNetCore.SignalR.Client;
using AwesomeAssertions;
using Xunit;
using ConduitLLM.IntegrationTests.Infrastructure;
using System.Collections.Concurrent;

namespace ConduitLLM.IntegrationTests.Tests;

[Collection("SignalR Redis Collection")]
[Trait("Category", "Integration")]
[Trait("Component", "SignalR")]
[Trait("Feature", "MultiClient")]
public class SignalRMultiClientTests : SignalRIntegrationTestBase
{
    public SignalRMultiClientTests(RedisTestContainerFixture redisFixture)
        : base(redisFixture) { }

    #region Broadcast Tests

    [Fact]
    public async Task Broadcast_To_All_Clients_Works_Across_Servers()
    {
        // Arrange - 3 servers, 2 clients per server = 6 clients total
        var serverUrls = await HostFactory!.CreateServersAsync(3, RedisFixture.ConnectionString);

        var connections = new List<HubConnection>();
        var receivedMessages = new ConcurrentBag<(string message, string connectionId)>();

        foreach (var url in serverUrls)
        {
            for (int i = 0; i < 2; i++)
            {
                var conn = CreateConnection(url, "/testhub");
                conn.On<string, string>("ReceiveMessage", (msg, sender) =>
                {
                    receivedMessages.Add((msg, conn.ConnectionId!));
                });
                connections.Add(conn);
            }
        }

        await Task.WhenAll(connections.Select(c => c.StartAsync()));
        await Task.Delay(2000); // Allow all connections to establish

        // Act - Broadcast from first client
        await connections[0].InvokeAsync("BroadcastMessage", "Hello everyone!");

        await Task.Delay(5000); // Allow propagation across all servers

        // Assert - All 6 clients should receive (including sender)
        receivedMessages.Count.Should().Be(6);
        receivedMessages.All(m => m.message == "Hello everyone!").Should().BeTrue();
    }

    [Fact]
    public async Task SendToOthers_Excludes_Sender()
    {
        // Arrange
        var serverUrls = await HostFactory!.CreateServersAsync(2, RedisFixture.ConnectionString);

        var sender = CreateConnection(serverUrls[0], "/testhub");
        var receiver1 = CreateConnection(serverUrls[0], "/testhub");
        var receiver2 = CreateConnection(serverUrls[1], "/testhub");

        var receivedBySender = new ConcurrentBag<string>();
        var receivedByOthers = new ConcurrentBag<string>();

        sender.On<string, string>("ReceiveMessage", (msg, s) => receivedBySender.Add(msg));
        receiver1.On<string, string>("ReceiveMessage", (msg, s) => receivedByOthers.Add(msg));
        receiver2.On<string, string>("ReceiveMessage", (msg, s) => receivedByOthers.Add(msg));

        await sender.StartAsync();
        await receiver1.StartAsync();
        await receiver2.StartAsync();

        await Task.Delay(1000); // Allow connections to stabilize

        // Act
        await sender.InvokeAsync("SendToOthers", "Message to others only");

        await Task.Delay(3000);

        // Assert
        receivedBySender.Should().BeEmpty("Sender should not receive their own message");
        receivedByOthers.Count.Should().Be(2, "Both receivers should get the message");
    }

    #endregion

    #region Protocol Mixing Tests

    [Fact]
    public async Task JSON_And_MessagePack_Clients_Can_Communicate()
    {
        // Arrange
        var serverUrls = await HostFactory!.CreateServersAsync(2, RedisFixture.ConnectionString);

        var jsonClient = CreateConnection(serverUrls[0], "/testhub", useMessagePack: false);
        var messagePackClient = CreateConnection(serverUrls[1], "/testhub", useMessagePack: true);

        var jsonReceived = CreateMessageWaiter<(string, string)>();
        var messagePackReceived = CreateMessageWaiter<(string, string)>();

        jsonClient.On<string, string>("ReceiveMessage", (msg, sender) =>
            jsonReceived.TrySetResult((msg, sender)));
        messagePackClient.On<string, string>("ReceiveMessage", (msg, sender) =>
            messagePackReceived.TrySetResult((msg, sender)));

        await jsonClient.StartAsync();
        await messagePackClient.StartAsync();

        await Task.Delay(1000);

        // Act - JSON client broadcasts
        await jsonClient.InvokeAsync("BroadcastMessage", "JSON says hello");

        // Assert - Both should receive
        await WaitForMessageAsync(jsonReceived, 5000, "JSON client should receive");
        await WaitForMessageAsync(messagePackReceived, 5000, "MessagePack client should receive");
    }

    [Fact]
    public async Task Multiple_Protocol_Clients_Receive_Broadcasts_Correctly()
    {
        // Arrange
        var serverUrls = await HostFactory!.CreateServersAsync(2, RedisFixture.ConnectionString);

        var clients = new List<HubConnection>();
        var receivedByClient = new ConcurrentDictionary<string, ConcurrentBag<string>>();

        // Create mixed protocol clients
        for (int i = 0; i < 4; i++)
        {
            var useMessagePack = i % 2 == 0;
            var conn = CreateConnection(serverUrls[i % 2], "/testhub", useMessagePack: useMessagePack);
            var clientId = $"client-{i}-{(useMessagePack ? "msgpack" : "json")}";
            receivedByClient[clientId] = new ConcurrentBag<string>();

            var capturedClientId = clientId;
            conn.On<string, string>("ReceiveMessage", (msg, sender) =>
            {
                receivedByClient[capturedClientId].Add(msg);
            });

            clients.Add(conn);
        }

        await Task.WhenAll(clients.Select(c => c.StartAsync()));
        await Task.Delay(1000);

        // Act - Each client broadcasts
        for (int i = 0; i < clients.Count; i++)
        {
            await clients[i].InvokeAsync("BroadcastMessage", $"Message from client {i}");
            await Task.Delay(500);
        }

        await Task.Delay(5000);

        // Assert - Each client should have received all 4 messages
        foreach (var kvp in receivedByClient)
        {
            kvp.Value.Count.Should().Be(4, $"Client {kvp.Key} should receive all 4 messages");
        }
    }

    #endregion

    #region High Load Tests

    [Fact]
    public async Task Multiple_Concurrent_Messages_Are_Delivered_Correctly()
    {
        // Arrange
        var serverUrls = await HostFactory!.CreateServersAsync(2, RedisFixture.ConnectionString);

        var conn1 = CreateConnection(serverUrls[0], "/testhub");
        var conn2 = CreateConnection(serverUrls[1], "/testhub");

        var receivedByConn1 = new ConcurrentBag<string>();
        var receivedByConn2 = new ConcurrentBag<string>();

        conn1.On<string, string>("ReceiveMessage", (msg, sender) => receivedByConn1.Add(msg));
        conn2.On<string, string>("ReceiveMessage", (msg, sender) => receivedByConn2.Add(msg));

        await conn1.StartAsync();
        await conn2.StartAsync();

        await Task.Delay(1000);

        // Act - Send 50 messages rapidly
        var messageCount = 50;
        var tasks = new List<Task>();

        for (int i = 0; i < messageCount; i++)
        {
            var index = i;
            tasks.Add(conn1.InvokeAsync("BroadcastMessage", $"Rapid message {index}"));
        }

        await Task.WhenAll(tasks);
        await Task.Delay(10000); // Allow all messages to propagate

        // Assert - Both should receive all messages
        receivedByConn1.Count.Should().Be(messageCount);
        receivedByConn2.Count.Should().Be(messageCount);
    }

    [Fact]
    public async Task Client_Specific_Routing_With_SendToOthers()
    {
        // Tests SendToOthers which excludes the sender - a form of client-specific routing

        // Arrange
        var serverUrls = await HostFactory!.CreateServersAsync(2, RedisFixture.ConnectionString);

        var clients = new List<HubConnection>();
        var receivedCounts = new ConcurrentDictionary<string, int>();

        for (int i = 0; i < 4; i++)
        {
            var conn = CreateConnection(serverUrls[i % 2], "/testhub");
            var clientId = $"client-{i}";
            receivedCounts[clientId] = 0;

            var capturedClientId = clientId;
            conn.On<string, string>("ReceiveMessage", (msg, sender) =>
            {
                receivedCounts.AddOrUpdate(capturedClientId, 1, (_, count) => count + 1);
            });

            clients.Add(conn);
        }

        await Task.WhenAll(clients.Select(c => c.StartAsync()));
        await Task.Delay(1000);

        // Act - Each client sends to others
        foreach (var client in clients)
        {
            await client.InvokeAsync("SendToOthers", "Test message");
            await Task.Delay(200);
        }

        await Task.Delay(5000);

        // Assert - Each client should have received from 3 other clients
        foreach (var kvp in receivedCounts)
        {
            kvp.Value.Should().Be(3, $"{kvp.Key} should receive from 3 other clients");
        }
    }

    #endregion
}
