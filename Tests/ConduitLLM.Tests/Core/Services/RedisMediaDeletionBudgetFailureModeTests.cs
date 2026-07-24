using ConduitLLM.Configuration.Options;
using ConduitLLM.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;

namespace ConduitLLM.Tests.Core.Services;

[Trait("Category", "Unit")]
[Trait("Component", "MediaLifecycle")]
public sealed class RedisMediaDeletionBudgetFailureModeTests
{
    [Theory]
    [InlineData(MediaBudgetFailureMode.FailClosed, 0)]
    [InlineData(MediaBudgetFailureMode.FailOpen, 4)]
    public async Task ReserveAsync_WhenRedisFails_AppliesConfiguredMode(
        MediaBudgetFailureMode failureMode,
        int expectedGrant)
    {
        var database = new Mock<IDatabase>();
        database
            .Setup(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(
                ConnectionFailureType.UnableToConnect,
                "test outage"));
        var redis = new Mock<IConnectionMultiplexer>();
        redis
            .Setup(connection => connection.GetDatabase(
                It.IsAny<int>(),
                It.IsAny<object>()))
            .Returns(database.Object);
        var service = new RedisMediaDeletionBudgetService(
            redis.Object,
            Mock.Of<ILogger<RedisMediaDeletionBudgetService>>(),
            Options.Create(new MediaLifecycleOptions
            {
                BudgetFailureMode = failureMode
            }));

        var reservation = await service.ReserveAsync(4, 100);

        reservation.Granted.Should().Be(expectedGrant);
        reservation.StoreFailed.Should().BeTrue();
        reservation.FailureMode.Should().Be(failureMode.ToString());
        service.LastFailureAtUtc.Should().NotBeNull();
    }
}
