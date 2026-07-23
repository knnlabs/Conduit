using ConduitLLM.Admin.DTOs;
using ConduitLLM.Admin.Services;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Options;
using ConduitLLM.Core.Interfaces;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace ConduitLLM.Tests.Admin.Services;

[Trait("Category", "Unit")]
[Trait("Component", "MediaLifecycle")]
public sealed class MediaCleanupStatusServiceTests : IDisposable
{
    private readonly ConduitDbContext _context;
    private readonly ServiceProvider _serviceProvider;

    public MediaCleanupStatusServiceTests()
    {
        var dbOptions = new DbContextOptionsBuilder<ConduitDbContext>()
            .UseInMemoryDatabase($"MediaCleanupStatus_{Guid.NewGuid()}")
            .Options;
        _context = new ConduitDbContext(dbOptions);

        var budgetService = new Mock<IMediaDeletionBudgetService>();
        budgetService
            .Setup(service => service.GetMonthlyDeleteCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(12);
        budgetService
            .Setup(service => service.GetRemainingBudgetAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(88);

        var settingRepository = new Mock<IGlobalSettingRepository>();
        settingRepository
            .Setup(repository => repository.GetByKeyAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GlobalSetting?)null);

        var services = new ServiceCollection();
        services.AddSingleton<IConfigurationDbContext>(_context);
        services.AddSingleton(budgetService.Object);
        services.AddSingleton(settingRepository.Object);
        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public async Task GetStatusAsync_WithoutRedis_ReturnsLastOutcomeForEveryCleanupType()
    {
        var options = new MediaLifecycleOptions
        {
            Enabled = true,
            MonthlyDeleteBudget = 100,
            EnableExpirationCleanup = true,
            EnableOrphanCleanup = false,
            EnableRetentionCleanup = true
        };
        var service = new MediaCleanupStatusService(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(options),
            Mock.Of<ILogger<MediaCleanupStatusService>>());

        await service.RecordOperationCompletionAsync(
            MediaCleanupTypes.Expiration, 2, 4096, 1.25, "Completed", "test-leader");
        await service.RecordOperationCompletionAsync(
            MediaCleanupTypes.Retention, 1, 1024, 0.5, "Completed with errors", "test-leader");

        var status = await service.GetStatusAsync();

        status.OperationStatuses.Should().HaveCount(3);
        status.OperationStatuses.Single(item => item.CleanupType == MediaCleanupTypes.Expiration)
            .Should().BeEquivalentTo(new
            {
                IsEnabled = true,
                LastRunStatus = "Completed",
                LastRunFilesDeleted = 2,
                LastRunBytesFreed = 4096L
            });
        status.OperationStatuses.Single(item => item.CleanupType == MediaCleanupTypes.Orphan)
            .Should().Match<MediaCleanupOperationStatusDto>(item =>
                !item.IsEnabled && item.LastRunTimeUtc == null);
        status.OperationStatuses.Single(item => item.CleanupType == MediaCleanupTypes.Retention)
            .LastRunStatus.Should().Be("Completed with errors");
        status.CurrentLeaderInstanceId.Should().Be("test-leader");
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
        _context.Dispose();
    }
}
