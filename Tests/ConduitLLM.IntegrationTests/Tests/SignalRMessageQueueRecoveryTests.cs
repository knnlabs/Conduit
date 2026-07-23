using ConduitLLM.Configuration.Options;
using ConduitLLM.Configuration.Services;
using ConduitLLM.Core.Constants;
using ConduitLLM.Gateway.Hubs;
using ConduitLLM.Gateway.Models;
using ConduitLLM.Gateway.Services;
using ConduitLLM.IntegrationTests.Infrastructure;

using FluentAssertions;

using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using StackExchange.Redis;

using Xunit;

using CoreTaskProgressMessage = ConduitLLM.Core.Models.SignalR.TaskProgressMessage;
using SignalRMessage = ConduitLLM.Core.Models.SignalR.SignalRMessage;

namespace ConduitLLM.IntegrationTests.Tests;

[Collection("SignalR Redis Collection")]
[Trait("Category", "Integration")]
[Trait("Component", "SignalR")]
public sealed class SignalRMessageQueueRecoveryTests : IAsyncLifetime
{
    private const string ConsumerGroup = "signalr-processors";

    private readonly RedisTestContainerFixture _redisFixture;
    private readonly RecordingHubContext<TaskHub> _hubContext = new();
    private ILoggerFactory _loggerFactory = null!;
    private ServiceProvider _serviceProvider = null!;
    private RedisConnectionFactory _redisConnectionFactory = null!;
    private SignalRMessageQueueService _queueService = null!;
    private IDatabase _database = null!;

    public SignalRMessageQueueRecoveryTests(RedisTestContainerFixture redisFixture)
    {
        _redisFixture = redisFixture;
    }

    public async Task InitializeAsync()
    {
        await _redisFixture.FlushAllAsync();

        _loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Warning));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SignalR:MessageQueue:MaxRetryAttempts"] = "2",
                ["SignalR:MessageQueue:InitialRetryDelayMs"] = "10",
                ["SignalR:MessageQueue:MaxRetryDelayMs"] = "10",
                ["SignalR:MessageQueue:ProcessingBatchSize"] = "20",
                ["SignalR:MessageQueue:ProcessingIntervalMs"] = "60000",
                ["SignalR:MessageQueue:PendingMessageIdleTimeoutMs"] = "25",
                ["SignalR:MessageQueue:CircuitBreakerFailureThreshold"] = "100"
            })
            .Build();

        _serviceProvider = new ServiceCollection()
            .AddSingleton<IHubContext<TaskHub>>(_hubContext)
            .BuildServiceProvider();

        _redisConnectionFactory = new RedisConnectionFactory(
            Options.Create(new CacheOptions { RedisConnectionString = _redisFixture.ConnectionString }),
            _loggerFactory.CreateLogger<RedisConnectionFactory>());

        _queueService = new SignalRMessageQueueService(
            _loggerFactory.CreateLogger<SignalRMessageQueueService>(),
            configuration,
            _serviceProvider,
            new StubAcknowledgmentService(),
            _redisConnectionFactory);

        await _queueService.StartAsync(CancellationToken.None);
        _database = (await _redisConnectionFactory.GetConnectionAsync()).GetDatabase();
    }

    public async Task DisposeAsync()
    {
        await _queueService.StopAsync(CancellationToken.None);
        _queueService.Dispose();
        await _serviceProvider.DisposeAsync();
        _redisConnectionFactory.Dispose();
        _loggerFactory.Dispose();
    }

    [Fact]
    public async Task FutureDatedMessage_IsNotReadEarly_AndIsDeliveredWhenDue()
    {
        var message = CreateMessage(DateTime.UtcNow.AddMilliseconds(200));

        await _queueService.EnqueueMessageAsync(message);
        await _queueService.ProcessMessagesAsync();

        _hubContext.Proxy.DeliveryCount.Should().Be(0);
        (await _database.SortedSetLengthAsync(RedisKeys.SignalR.DelayedMessages)).Should().Be(1);
        (await _database.StreamPendingAsync(RedisKeys.SignalR.MessageStream, ConsumerGroup))
            .PendingMessageCount.Should().Be(0);

        await Task.Delay(250);
        await _queueService.ProcessMessagesAsync();

        _hubContext.Proxy.DeliveryCount.Should().Be(1);
        (await _database.SortedSetLengthAsync(RedisKeys.SignalR.DelayedMessages)).Should().Be(0);
        (await _database.StreamPendingAsync(RedisKeys.SignalR.MessageStream, ConsumerGroup))
            .PendingMessageCount.Should().Be(0);
    }

    [Fact]
    public async Task MessageReadByCrashedConsumer_IsClaimedByLiveConsumer()
    {
        await _queueService.EnqueueMessageAsync(CreateMessage());
        var abandoned = await _database.StreamReadGroupAsync(
            RedisKeys.SignalR.MessageStream,
            ConsumerGroup,
            "crashed-consumer",
            ">",
            count: 1);

        abandoned.Should().ContainSingle();
        await Task.Delay(50);

        var pendingStatistics = _queueService.GetStatistics();
        pendingStatistics.PendingMessages.Should().Be(1);
        pendingStatistics.OldestPendingAgeSeconds.Should().BeGreaterThan(0,
            $"pending stream entry {abandoned[0].Id} should expose its age");

        await _queueService.ProcessMessagesAsync();

        _hubContext.Proxy.DeliveryCount.Should().Be(1);
        _queueService.GetStatistics().ClaimedMessages.Should().Be(1);
        (await _database.StreamPendingAsync(RedisKeys.SignalR.MessageStream, ConsumerGroup))
            .PendingMessageCount.Should().Be(0);
    }

    [Fact]
    public async Task FailedDelivery_IsDelayed_ThenMovedToDeadLetterAfterRetryExhaustion()
    {
        _hubContext.Proxy.ThrowOnSend = true;
        await _queueService.EnqueueMessageAsync(CreateMessage());

        await _queueService.ProcessMessagesAsync();

        (await _database.SortedSetLengthAsync(RedisKeys.SignalR.DelayedMessages)).Should().Be(1);
        _queueService.GetStatistics().RetriedMessages.Should().Be(1);

        await Task.Delay(30);
        await _queueService.ProcessMessagesAsync();

        (await _database.SortedSetLengthAsync(RedisKeys.SignalR.DelayedMessages)).Should().Be(0);
        (await _database.StreamLengthAsync(RedisKeys.SignalR.DeadLetterStream)).Should().Be(1);
        (await _database.StreamPendingAsync(RedisKeys.SignalR.MessageStream, ConsumerGroup))
            .PendingMessageCount.Should().Be(0);
        _queueService.GetDeadLetterMessages().Should().ContainSingle()
            .Which.DeliveryAttempts.Should().Be(2);
    }

    [Fact]
    public async Task ReclaimAfterCrash_CanRedeliverMessage_AndThenAcknowledgesIt()
    {
        await _queueService.EnqueueMessageAsync(CreateMessage());
        var abandoned = await _database.StreamReadGroupAsync(
            RedisKeys.SignalR.MessageStream,
            ConsumerGroup,
            "consumer-that-crashed-after-send",
            ">",
            count: 1);

        abandoned.Should().ContainSingle();
        _hubContext.Proxy.RecordExternalDelivery();
        await Task.Delay(50);

        await _queueService.ProcessMessagesAsync();

        _hubContext.Proxy.DeliveryCount.Should().Be(2,
            "at-least-once recovery may repeat a delivery completed before the crashed consumer acknowledged it");
        (await _database.StreamPendingAsync(RedisKeys.SignalR.MessageStream, ConsumerGroup))
            .PendingMessageCount.Should().Be(0);
    }

    [Fact]
    public async Task PoisonEntry_IsDeadLetteredAndAcknowledged()
    {
        await _database.StreamAddAsync(
            RedisKeys.SignalR.MessageStream,
            new NameValueEntry[] { new("data", "{not-json") });

        await _queueService.ProcessMessagesAsync();

        (await _database.StreamLengthAsync(RedisKeys.SignalR.DeadLetterStream)).Should().Be(1);
        (await _database.StreamPendingAsync(RedisKeys.SignalR.MessageStream, ConsumerGroup))
            .PendingMessageCount.Should().Be(0);
    }

    [Fact]
    public async Task MessageThatExpiresWhileDelayed_IsMovedToDeadLetterWithoutDelivery()
    {
        var message = CreateMessage(DateTime.UtcNow.AddMilliseconds(150));
        message.Message.ExpiresAt = DateTime.UtcNow.AddMilliseconds(50);
        await _queueService.EnqueueMessageAsync(message);

        await Task.Delay(200);
        await _queueService.ProcessMessagesAsync();

        _hubContext.Proxy.DeliveryCount.Should().Be(0);
        (await _database.StreamLengthAsync(RedisKeys.SignalR.DeadLetterStream)).Should().Be(1);
    }

    private static QueuedMessage CreateMessage(DateTime? nextDeliveryAt = null)
    {
        return new QueuedMessage
        {
            Message = new CoreTaskProgressMessage
            {
                TaskId = Guid.NewGuid().ToString("N"),
                Status = "running",
                ProgressPercentage = 50
            },
            ConnectionId = "test-connection",
            HubName = nameof(TaskHub),
            MethodName = "TaskProgress",
            NextDeliveryAt = nextDeliveryAt ?? DateTime.UtcNow
        };
    }

    private sealed class StubAcknowledgmentService : ISignalRAcknowledgmentService
    {
        public Task<PendingAcknowledgment> RegisterMessageAsync(
            SignalRMessage message,
            string connectionId,
            string hubName,
            string methodName,
            TimeSpan? timeout = null) => throw new NotSupportedException();

        public Task<bool> AcknowledgeMessageAsync(string messageId, string connectionId) => Task.FromResult(false);
        public Task<bool> NackMessageAsync(string messageId, string connectionId, string? errorMessage = null) => Task.FromResult(false);
        public Task<AcknowledgmentStatus?> GetMessageStatusAsync(string messageId) => Task.FromResult<AcknowledgmentStatus?>(null);
        public Task<IEnumerable<PendingAcknowledgment>> GetPendingAcknowledgmentsAsync(string connectionId) =>
            Task.FromResult(Enumerable.Empty<PendingAcknowledgment>());
        public Task CleanupConnectionAsync(string connectionId) => Task.CompletedTask;
    }

    private sealed class RecordingHubContext<THub> : IHubContext<THub>
        where THub : Hub
    {
        public RecordingClientProxy Proxy { get; } = new();
        public IHubClients Clients { get; }
        public IGroupManager Groups { get; } = new NoOpGroupManager();

        public RecordingHubContext()
        {
            Clients = new RecordingHubClients(Proxy);
        }
    }

    private sealed class RecordingHubClients : IHubClients
    {
        private readonly IClientProxy _proxy;

        public RecordingHubClients(IClientProxy proxy)
        {
            _proxy = proxy;
        }

        public IClientProxy All => _proxy;
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => _proxy;
        public IClientProxy Client(string connectionId) => _proxy;
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => _proxy;
        public IClientProxy Group(string groupName) => _proxy;
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => _proxy;
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => _proxy;
        public IClientProxy User(string userId) => _proxy;
        public IClientProxy Users(IReadOnlyList<string> userIds) => _proxy;
    }

    private sealed class RecordingClientProxy : IClientProxy
    {
        private int _deliveryCount;

        public bool ThrowOnSend { get; set; }
        public int DeliveryCount => Volatile.Read(ref _deliveryCount);

        public Task SendCoreAsync(
            string method,
            object?[] args,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _deliveryCount);
            return ThrowOnSend
                ? Task.FromException(new InvalidOperationException("Simulated SignalR send failure"))
                : Task.CompletedTask;
        }

        public void RecordExternalDelivery()
        {
            Interlocked.Increment(ref _deliveryCount);
        }
    }

    private sealed class NoOpGroupManager : IGroupManager
    {
        public Task AddToGroupAsync(
            string connectionId,
            string groupName,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RemoveFromGroupAsync(
            string connectionId,
            string groupName,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
