using ConduitLLM.Core.Extensions;
using ConduitLLM.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;

namespace ConduitLLM.Tests.Core.Extensions;

public sealed class DistributedLockExtensionsTests
{
    [Fact]
    public async Task RunWithOptionalLockAsync_ReleasesAcquiredLock()
    {
        var handle = new Mock<IDistributedLock>();
        handle.Setup(lockHandle => lockHandle.ReleaseAsync()).Returns(Task.CompletedTask);
        var service = new Mock<IDistributedLockService>();
        service
            .Setup(lockService => lockService.AcquireLockWithRetryAsync(
                "work",
                It.IsAny<TimeSpan>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(handle.Object);

        var result = await service.Object.RunWithOptionalLockAsync(
            "work",
            TimeSpan.FromMinutes(1),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromMilliseconds(10),
            acquired => Task.FromResult(acquired ? 42 : 0),
            Mock.Of<ILogger>());

        Assert.True(result.Executed);
        Assert.Equal(42, result.Value);
        handle.Verify(lockHandle => lockHandle.ReleaseAsync(), Times.Once);
    }

    [Fact]
    public async Task RunWithOptionalLockAsync_CanSkipOnTimeout()
    {
        var service = new Mock<IDistributedLockService>();
        service
            .Setup(lockService => lockService.AcquireLockWithRetryAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException());
        var operationCalled = false;

        var result = await service.Object.RunWithOptionalLockAsync(
            "work",
            TimeSpan.FromMinutes(1),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromMilliseconds(10),
            _ =>
            {
                operationCalled = true;
                return Task.FromResult(42);
            },
            Mock.Of<ILogger>(),
            skipOnTimeout: true);

        Assert.False(result.Executed);
        Assert.False(operationCalled);
    }
}
