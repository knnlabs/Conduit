using ConduitLLM.Gateway.Services;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;

namespace ConduitLLM.Tests.Gateway.Services;

public sealed class DistributedAlertManagementServiceTests
{
    [Fact]
    public async Task StopAsync_CancelsAndJoinsAlertStreamProcessor()
    {
        var database = new Mock<IDatabase>();
        var redis = new Mock<IConnectionMultiplexer>();
        var streamRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        redis.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(database.Object);
        database
            .Setup(x => x.StreamReadAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<int?>(),
                It.IsAny<CommandFlags>()))
            .Callback(() => streamRead.TrySetResult())
            .ReturnsAsync([]);

        using var service = new DistributedAlertManagementService(
            redis.Object,
            Mock.Of<ILogger<DistributedAlertManagementService>>(),
            Mock.Of<IServiceProvider>());

        await service.StartAsync(CancellationToken.None);
        await streamRead.Task.WaitAsync(TimeSpan.FromSeconds(2));

        await service.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2));
        var readsAfterStop = database.Invocations.Count(x => x.Method.Name == nameof(IDatabase.StreamReadAsync));

        await Task.Delay(100);

        Assert.Equal(
            readsAfterStop,
            database.Invocations.Count(x => x.Method.Name == nameof(IDatabase.StreamReadAsync)));
    }
}
