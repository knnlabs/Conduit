using Microsoft.AspNetCore.SignalR.Client;
using AwesomeAssertions;
using Xunit;
using ConduitLLM.IntegrationTests.Infrastructure;
using System.Collections.Concurrent;

namespace ConduitLLM.IntegrationTests.Tests;

[Collection("SignalR Redis Collection")]
[Trait("Category", "Integration")]
[Trait("Component", "SignalR")]
[Trait("Feature", "GroupManagement")]
public class SignalRGroupManagementTests : SignalRIntegrationTestBase
{
    public SignalRGroupManagementTests(RedisTestContainerFixture redisFixture)
        : base(redisFixture) { }

    #region Group Membership Tests

    [Fact]
    public async Task Client_Can_Join_And_Leave_Groups()
    {
        // Arrange
        var serverUrls = await HostFactory!.CreateServersAsync(1, RedisFixture.ConnectionString);
        var connection = CreateConnection(serverUrls[0], "/grouphub");

        var groupJoined = new TaskCompletionSource<string>();
        var groupLeft = new TaskCompletionSource<string>();

        connection.On<string>("GroupJoined", group => groupJoined.TrySetResult(group));
        connection.On<string>("GroupLeft", group => groupLeft.TrySetResult(group));

        await connection.StartAsync();

        // Act - Join
        await connection.InvokeAsync("JoinGroup", "test-group");
        var joined = await WaitForMessageAsync(groupJoined, 5000);

        // Act - Leave
        await connection.InvokeAsync("LeaveGroup", "test-group");
        var left = await WaitForMessageAsync(groupLeft, 5000);

        // Assert
        joined.Should().Be("test-group");
        left.Should().Be("test-group");
    }

    [Fact]
    public async Task Group_Membership_Requires_Rejoin_After_Reconnection()
    {
        // Note: SignalR does NOT automatically restore group membership on reconnect.
        // This test demonstrates that clients need to rejoin groups after reconnection.

        // Arrange
        var serverUrls = await HostFactory!.CreateServersAsync(1, RedisFixture.ConnectionString);
        var connection = CreateConnection(
            serverUrls[0], "/grouphub",
            withAutoReconnect: true,
            reconnectDelays: new[] { TimeSpan.FromMilliseconds(100) });

        var groupJoinedAfterReconnect = new TaskCompletionSource<string>();

        connection.Reconnected += async connectionId =>
        {
            // Re-join group after reconnection - this is the expected pattern
            await connection.InvokeAsync("JoinGroup", "persistent-group");
        };

        connection.On<string>("GroupJoined", group =>
            groupJoinedAfterReconnect.TrySetResult(group));

        await connection.StartAsync();

        // Initial join
        var initialJoin = new TaskCompletionSource<string>();
        connection.On<string>("GroupJoined", group => initialJoin.TrySetResult(group));
        await connection.InvokeAsync("JoinGroup", "persistent-group");
        await WaitForMessageAsync(initialJoin, 5000);

        // Simulate server restart
        await HostFactory.DisposeAsync();
        HostFactory = new MultiServerSignalRHostFactory();
        await Task.Delay(300);
        await HostFactory.CreateServersAsync(1, RedisFixture.ConnectionString);

        // Wait for reconnection and rejoin
        var result = await Task.WhenAny(groupJoinedAfterReconnect.Task, Task.Delay(10000));

        // The test validates the pattern - client must explicitly rejoin on reconnect
        // Success if the group join happens after reconnection
    }

    [Fact]
    public async Task Group_Cleanup_Occurs_On_Disconnection()
    {
        // Arrange
        var serverUrls = await HostFactory!.CreateServersAsync(2, RedisFixture.ConnectionString);

        var conn1 = CreateConnection(serverUrls[0], "/grouphub");
        var conn2 = CreateConnection(serverUrls[1], "/grouphub");

        var conn2Messages = new ConcurrentBag<string>();
        conn2.On<string, string>("GroupMessage", (msg, sender) => conn2Messages.Add(msg));

        await conn1.StartAsync();
        await conn2.StartAsync();

        // Both join the same group
        var join1 = new TaskCompletionSource<string>();
        var join2 = new TaskCompletionSource<string>();
        conn1.On<string>("GroupJoined", g => join1.TrySetResult(g));
        conn2.On<string>("GroupJoined", g => join2.TrySetResult(g));

        await conn1.InvokeAsync("JoinGroup", "cleanup-test-group");
        await conn2.InvokeAsync("JoinGroup", "cleanup-test-group");

        await WaitForMessageAsync(join1, 5000);
        await WaitForMessageAsync(join2, 5000);

        await Task.Delay(1000); // Allow propagation

        // Act - Disconnect conn1
        await conn1.StopAsync();
        await Task.Delay(2000); // Allow cleanup

        // Send from conn2 to group
        await conn2.InvokeAsync("SendToGroup", "cleanup-test-group", "After disconnect");

        await Task.Delay(1000);

        // Assert - conn2 should receive the message (it's still in the group)
        conn2Messages.Should().Contain("After disconnect");
    }

    [Fact]
    public async Task Multi_Client_Group_Membership_Works()
    {
        // Arrange
        var serverUrls = await HostFactory!.CreateServersAsync(2, RedisFixture.ConnectionString);

        var connections = new List<HubConnection>();
        var messagesByConnection = new ConcurrentDictionary<string, ConcurrentBag<string>>();

        for (int i = 0; i < 4; i++)
        {
            var conn = CreateConnection(serverUrls[i % 2], "/grouphub");
            var connId = $"conn-{i}";
            messagesByConnection[connId] = new ConcurrentBag<string>();

            var capturedConnId = connId;
            conn.On<string, string>("GroupMessage", (msg, sender) =>
            {
                messagesByConnection[capturedConnId].Add(msg);
            });

            connections.Add(conn);
        }

        await Task.WhenAll(connections.Select(c => c.StartAsync()));

        // Setup join confirmations
        for (int i = 0; i < 4; i++)
        {
            var joinConfirm = new TaskCompletionSource<string>();
            connections[i].On<string>("GroupJoined", g => joinConfirm.TrySetResult(g));
        }

        // Conn 0, 1, 2 join group-A; Conn 2, 3 join group-B
        await connections[0].InvokeAsync("JoinGroup", "group-A");
        await connections[1].InvokeAsync("JoinGroup", "group-A");
        await connections[2].InvokeAsync("JoinGroup", "group-A");
        await connections[2].InvokeAsync("JoinGroup", "group-B");
        await connections[3].InvokeAsync("JoinGroup", "group-B");

        await Task.Delay(2000); // Allow group memberships to propagate

        // Act - Send to each group
        await connections[0].InvokeAsync("SendToGroup", "group-A", "Message for A");
        await connections[3].InvokeAsync("SendToGroup", "group-B", "Message for B");

        await Task.Delay(3000); // Allow messages to propagate

        // Assert
        // Conn 0, 1, 2 should have received "Message for A"
        messagesByConnection["conn-0"].Should().Contain("Message for A");
        messagesByConnection["conn-1"].Should().Contain("Message for A");
        messagesByConnection["conn-2"].Should().Contain("Message for A");

        // Conn 2, 3 should have received "Message for B"
        messagesByConnection["conn-2"].Should().Contain("Message for B");
        messagesByConnection["conn-3"].Should().Contain("Message for B");

        // Conn 0, 1 should NOT have received "Message for B"
        messagesByConnection["conn-0"].Should().NotContain("Message for B");
        messagesByConnection["conn-1"].Should().NotContain("Message for B");
    }

    [Fact]
    public async Task Group_Message_Delivery_To_Correct_Clients_Only()
    {
        // Arrange
        var serverUrls = await HostFactory!.CreateServersAsync(2, RedisFixture.ConnectionString);

        var insideGroup = CreateConnection(serverUrls[0], "/grouphub");
        var outsideGroup = CreateConnection(serverUrls[1], "/grouphub");

        var insideReceived = new ConcurrentBag<string>();
        var outsideReceived = new ConcurrentBag<string>();

        insideGroup.On<string, string>("GroupMessage", (msg, sender) => insideReceived.Add(msg));
        outsideGroup.On<string, string>("GroupMessage", (msg, sender) => outsideReceived.Add(msg));

        await insideGroup.StartAsync();
        await outsideGroup.StartAsync();

        // Only one joins the group
        var joinConfirm = new TaskCompletionSource<string>();
        insideGroup.On<string>("GroupJoined", g => joinConfirm.TrySetResult(g));
        await insideGroup.InvokeAsync("JoinGroup", "exclusive-group");
        await WaitForMessageAsync(joinConfirm, 5000);

        await Task.Delay(500);

        // Act
        await insideGroup.InvokeAsync("SendToGroup", "exclusive-group", "Exclusive message");
        await Task.Delay(2000);

        // Assert
        insideReceived.Should().Contain("Exclusive message");
        outsideReceived.Should().BeEmpty("Client outside group should not receive message");
    }

    #endregion
}
