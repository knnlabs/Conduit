using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;
using Microsoft.AspNetCore.SignalR.StackExchangeRedis;

namespace ConduitLLM.IntegrationTests.Infrastructure;

/// <summary>
/// Factory for creating multiple in-process SignalR hosts with Redis backplane
/// to test multi-server scenarios.
/// </summary>
public class MultiServerSignalRHostFactory : IAsyncDisposable
{
    private readonly List<IHost> _hosts = new();
    private readonly List<string> _serverUrls = new();
    private static int _nextPort = 5600;
    private static readonly object _portLock = new();

    public IReadOnlyList<string> ServerUrls => _serverUrls.AsReadOnly();
    public int ServerCount => _hosts.Count;

    /// <summary>
    /// Creates multiple SignalR hosts with Redis backplane for multi-server testing.
    /// </summary>
    public async Task<IReadOnlyList<string>> CreateServersAsync(
        int serverCount,
        string? redisConnectionString,
        bool enableMessagePack = true,
        Action<IServiceCollection>? configureServices = null)
    {
        for (int i = 0; i < serverCount; i++)
        {
            int port;
            lock (_portLock)
            {
                port = _nextPort++;
            }
            var serverUrl = $"http://localhost:{port}";
            _serverUrls.Add(serverUrl);

            var host = CreateHost(serverUrl, redisConnectionString, enableMessagePack, configureServices);
            _hosts.Add(host);

            await host.StartAsync();
        }

        return _serverUrls;
    }

    private static IHost CreateHost(
        string url,
        string? redisConnectionString,
        bool enableMessagePack,
        Action<IServiceCollection>? configureServices)
    {
        return Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder
                    .UseUrls(url)
                    .ConfigureServices(services =>
                    {
                        var signalRBuilder = services.AddSignalR(options =>
                        {
                            options.EnableDetailedErrors = true;
                            options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
                            options.KeepAliveInterval = TimeSpan.FromSeconds(15);
                        });

                        if (enableMessagePack)
                        {
                            signalRBuilder.AddMessagePackProtocol(options =>
                            {
                                options.SerializerOptions = MessagePack.MessagePackSerializerOptions.Standard
                                    .WithResolver(MessagePack.Resolvers.StandardResolver.Instance)
                                    .WithSecurity(MessagePack.MessagePackSecurity.UntrustedData);
                            });
                        }

                        // Configure Redis backplane
                        if (!string.IsNullOrEmpty(redisConnectionString))
                        {
                            signalRBuilder.AddStackExchangeRedis(redisConnectionString, options =>
                            {
                                options.Configuration.ChannelPrefix = RedisChannel.Literal("test_signalr:");
                                options.Configuration.DefaultDatabase = 3; // Test database
                                options.Configuration.AbortOnConnectFail = false;
                                options.Configuration.ReconnectRetryPolicy = new LinearRetry(500);
                            });
                        }

                        configureServices?.Invoke(services);
                    })
                    .Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapHub<TestBroadcastHub>("/testhub");
                            endpoints.MapHub<TestGroupHub>("/grouphub");
                        });
                    });
            })
            .Build();
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var host in _hosts)
        {
            try
            {
                await host.StopAsync(TimeSpan.FromSeconds(5));
            }
            catch
            {
                // Ignore errors during cleanup
            }
            host.Dispose();
        }
        _hosts.Clear();
        _serverUrls.Clear();
    }
}

/// <summary>
/// Test hub for broadcast and multi-client testing.
/// </summary>
public class TestBroadcastHub : Hub
{
    public Task<string> Echo(string message) => Task.FromResult($"Echo: {message}");

    public async Task BroadcastMessage(string message)
    {
        await Clients.All.SendAsync("ReceiveMessage", message, Context.ConnectionId);
    }

    public async Task SendToOthers(string message)
    {
        await Clients.Others.SendAsync("ReceiveMessage", message, Context.ConnectionId);
    }

    public string GetConnectionId() => Context.ConnectionId;
}

/// <summary>
/// Test hub for group management testing.
/// </summary>
public class TestGroupHub : Hub
{
    public async Task JoinGroup(string groupName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        await Clients.Caller.SendAsync("GroupJoined", groupName);
    }

    public async Task LeaveGroup(string groupName)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        await Clients.Caller.SendAsync("GroupLeft", groupName);
    }

    public async Task SendToGroup(string groupName, string message)
    {
        await Clients.Group(groupName).SendAsync("GroupMessage", message, Context.ConnectionId);
    }

    public async Task SendToOthersInGroup(string groupName, string message)
    {
        await Clients.OthersInGroup(groupName).SendAsync("GroupMessage", message, Context.ConnectionId);
    }

    public string GetConnectionId() => Context.ConnectionId;
}
