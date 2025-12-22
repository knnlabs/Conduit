using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using FluentAssertions;
using Xunit;

namespace ConduitLLM.IntegrationTests.Infrastructure;

/// <summary>
/// Base class for SignalR integration tests providing common utilities.
/// </summary>
public abstract class SignalRIntegrationTestBase : IAsyncLifetime
{
    protected readonly RedisTestContainerFixture RedisFixture;
    protected MultiServerSignalRHostFactory? HostFactory;
    protected readonly List<HubConnection> ActiveConnections = new();

    protected SignalRIntegrationTestBase(RedisTestContainerFixture redisFixture)
    {
        RedisFixture = redisFixture;
    }

    public virtual async Task InitializeAsync()
    {
        await RedisFixture.FlushAllAsync();
        HostFactory = new MultiServerSignalRHostFactory();
    }

    public virtual async Task DisposeAsync()
    {
        // Dispose all connections
        foreach (var connection in ActiveConnections)
        {
            try
            {
                if (connection.State != HubConnectionState.Disconnected)
                {
                    await connection.StopAsync();
                }
                await connection.DisposeAsync();
            }
            catch
            {
                // Ignore errors during cleanup
            }
        }
        ActiveConnections.Clear();

        // Dispose host factory
        if (HostFactory != null)
        {
            await HostFactory.DisposeAsync();
        }
    }

    /// <summary>
    /// Creates a SignalR client connection to the specified server URL.
    /// </summary>
    protected HubConnection CreateConnection(
        string serverUrl,
        string hubPath,
        bool useMessagePack = false,
        bool withAutoReconnect = false,
        TimeSpan[]? reconnectDelays = null)
    {
        var builder = new HubConnectionBuilder()
            .WithUrl($"{serverUrl}{hubPath}");

        if (useMessagePack)
        {
            builder.AddMessagePackProtocol();
        }

        if (withAutoReconnect)
        {
            if (reconnectDelays != null)
            {
                builder.WithAutomaticReconnect(reconnectDelays);
            }
            else
            {
                builder.WithAutomaticReconnect();
            }
        }

        var connection = builder.Build();
        ActiveConnections.Add(connection);
        return connection;
    }

    /// <summary>
    /// Creates a TaskCompletionSource with timeout for async message waiting.
    /// </summary>
    protected static TaskCompletionSource<T> CreateMessageWaiter<T>(int timeoutMs = 10000)
    {
        var tcs = new TaskCompletionSource<T>();
        var cts = new CancellationTokenSource(timeoutMs);
        cts.Token.Register(() => tcs.TrySetCanceled());
        return tcs;
    }

    /// <summary>
    /// Waits for a message with timeout and assertion.
    /// </summary>
    protected static async Task<T> WaitForMessageAsync<T>(
        TaskCompletionSource<T> tcs,
        int timeoutMs = 10000,
        string? assertionMessage = null)
    {
        var completed = await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs));
        completed.Should().Be(tcs.Task, assertionMessage ?? "Message should be received within timeout");
        return tcs.Task.Result;
    }

    /// <summary>
    /// Asserts connection state with FluentAssertions.
    /// </summary>
    protected static void AssertConnectionState(HubConnection connection, HubConnectionState expectedState)
    {
        connection.State.Should().Be(expectedState);
    }
}
