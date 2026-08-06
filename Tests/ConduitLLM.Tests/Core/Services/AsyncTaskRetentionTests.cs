using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Extensions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;

namespace ConduitLLM.Tests.Core.Services;

public sealed class AsyncTaskRetentionTests
{
    [Fact]
    public void AddAsyncTaskServices_ResolvesHybridServiceFromHostContainer()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDistributedMemoryCache();
        services.AddSingleton(Mock.Of<IAsyncTaskRepository>());
        services.AddAsyncTaskServices();
        using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();

        var service = scope.ServiceProvider.GetRequiredService<IAsyncTaskService>();

        Assert.IsType<HybridAsyncTaskService>(service);
    }

    [Fact]
    public async Task CleanupOldTasks_DrainsEveryDeleteBatchAndReportsBothCounts()
    {
        var repository = new Mock<IAsyncTaskRepository>();
        repository
            .Setup(repo => repo.ArchiveOldTasksAsync(
                TimeSpan.FromDays(1),
                TimeSpan.FromDays(7),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);
        repository
            .SetupSequence(repo => repo.GetTasksForCleanupAsync(
                TimeSpan.FromDays(30),
                2,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([Task("one"), Task("two")])
            .ReturnsAsync([Task("three")])
            .ReturnsAsync([]);
        repository
            .Setup(repo => repo.BulkDeleteAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<string> ids, CancellationToken _) => ids.Count());

        var services = new ServiceCollection();
        services.AddDistributedMemoryCache();
        using var provider = services.BuildServiceProvider();
        var service = new HybridAsyncTaskService(
            repository.Object,
            provider.GetRequiredService<Microsoft.Extensions.Caching.Distributed.IDistributedCache>(),
            NullLogger<HybridAsyncTaskService>.Instance);

        var result = await service.CleanupOldTasksAsync(new AsyncTaskRetentionPolicy(
            TimeSpan.FromDays(1),
            TimeSpan.FromDays(30),
            TimeSpan.FromDays(7),
            BatchSize: 2));

        Assert.Equal(new AsyncTaskCleanupResult(3, 3), result);
        repository.Verify(
            repo => repo.GetTasksForCleanupAsync(
                TimeSpan.FromDays(30),
                2,
                It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    private static AsyncTask Task(string id) => new() { Id = id };
}
