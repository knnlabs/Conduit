using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ConduitLLM.Tests.Configuration.Repositories;

public sealed class AsyncTaskRepositoryLifecycleTests : IAsyncLifetime
{
    private readonly SqliteTestDatabase _database = new();
    private AsyncTaskRepository _repository = null!;

    public async Task InitializeAsync()
    {
        await _database.SeedAsync(DurableLifecycleTestData.SeedRequiredGraphAsync);
        _repository = new AsyncTaskRepository(
            _database.CreateDbContextFactory(),
            NullLogger<AsyncTaskRepository>.Instance);
    }

    public Task DisposeAsync() => _database.DisposeAsync().AsTask();

    [Fact]
    public async Task GetPendingTasksAsync_FiltersAndOrdersEligibleTasks()
    {
        var now = DateTime.UtcNow;
        var oldest = DurableLifecycleTestData.NewAsyncTask("oldest", now.AddMinutes(-20));
        var expiredLease = DurableLifecycleTestData.NewAsyncTask("expired-lease", now.AddMinutes(-15));
        expiredLease.LeasedBy = "dead-worker";
        expiredLease.LeaseExpiryTime = now.AddMinutes(-5);
        expiredLease.NextRetryAt = now.AddMinutes(-1);

        var later = DurableLifecycleTestData.NewAsyncTask("later", now.AddMinutes(-10));
        var wrongType = DurableLifecycleTestData.NewAsyncTask(
            "wrong-type", now.AddMinutes(-30), "video_generation");
        var archived = DurableLifecycleTestData.NewAsyncTask("archived", now.AddMinutes(-40));
        archived.IsArchived = true;
        var activeLease = DurableLifecycleTestData.NewAsyncTask("active-lease", now.AddMinutes(-50));
        activeLease.LeasedBy = "worker";
        activeLease.LeaseExpiryTime = now.AddMinutes(10);
        var futureRetry = DurableLifecycleTestData.NewAsyncTask("future-retry", now.AddMinutes(-60));
        futureRetry.NextRetryAt = now.AddMinutes(10);
        var completed = DurableLifecycleTestData.NewAsyncTask(
            "completed", now.AddMinutes(-70), state: 2);

        await SeedTasksAsync(
            oldest, expiredLease, later, wrongType, archived, activeLease, futureRetry, completed);

        var selected = await _repository.GetPendingTasksAsync("image_generation", limit: 2);

        Assert.Equal(new[] { "oldest", "expired-lease" }, selected.Select(t => t.Id));
    }

    [Fact]
    public async Task LeaseNextPendingTaskAsync_LeasesOldestEligibleAndPersistsVersion()
    {
        var now = DateTime.UtcNow;
        var deferred = DurableLifecycleTestData.NewAsyncTask("deferred", now.AddHours(-2));
        deferred.NextRetryAt = now.AddHours(1);
        var expected = DurableLifecycleTestData.NewAsyncTask("expected", now.AddHours(-1));
        expected.Version = 4;
        await SeedTasksAsync(deferred, expected);

        var leased = await _repository.LeaseNextPendingTaskAsync(
            "worker-a", TimeSpan.FromMinutes(5));

        Assert.NotNull(leased);
        Assert.Equal("expected", leased.Id);
        await using var verification = _database.CreateContext();
        var durable = await verification.AsyncTasks.AsNoTracking().SingleAsync(t => t.Id == "expected");
        Assert.Equal("worker-a", durable.LeasedBy);
        Assert.True(durable.LeaseExpiryTime > now.AddMinutes(4));
        Assert.Equal(5, durable.Version);
    }

    [Fact]
    public async Task LeaseNextPendingTaskAsync_ConcurrentWorkers_HaveOneWinner()
    {
        await SeedTasksAsync(DurableLifecycleTestData.NewAsyncTask(
            "lease-race", DateTime.UtcNow.AddMinutes(-5)));

        var results = await Task.WhenAll(
            _repository.LeaseNextPendingTaskAsync("worker-1", TimeSpan.FromMinutes(5)),
            _repository.LeaseNextPendingTaskAsync("worker-2", TimeSpan.FromMinutes(5)));

        Assert.Single(results, result => result != null);
        await using var verification = _database.CreateContext();
        var durable = await verification.AsyncTasks.AsNoTracking().SingleAsync();
        Assert.Contains(durable.LeasedBy, new[] { "worker-1", "worker-2" });
        Assert.Equal(1, durable.Version);
    }

    [Fact]
    public async Task ReleaseAndExtendLease_RequireOwnerAndUnexpiredLease()
    {
        var now = DateTime.UtcNow;
        var active = DurableLifecycleTestData.NewAsyncTask("active", now.AddMinutes(-2), state: 1);
        active.LeasedBy = "owner";
        active.LeaseExpiryTime = now.AddMinutes(10);
        var expired = DurableLifecycleTestData.NewAsyncTask("expired", now.AddMinutes(-3), state: 1);
        expired.LeasedBy = "owner";
        expired.LeaseExpiryTime = now.AddMinutes(-10);
        await SeedTasksAsync(active, expired);

        Assert.False(await _repository.ExtendLeaseAsync(
            "active", "other", TimeSpan.FromMinutes(20)));
        Assert.True(await _repository.ExtendLeaseAsync(
            "active", "owner", TimeSpan.FromMinutes(20)));
        Assert.False(await _repository.ReleaseLeaseAsync("active", "other"));
        Assert.True(await _repository.ReleaseLeaseAsync("active", "owner"));
        Assert.False(await _repository.ExtendLeaseAsync(
            "expired", "owner", TimeSpan.FromMinutes(20)));
        Assert.False(await _repository.ReleaseLeaseAsync("expired", "owner"));

        await using var verification = _database.CreateContext();
        var durable = await verification.AsyncTasks.AsNoTracking()
            .OrderBy(t => t.Id)
            .ToListAsync();
        Assert.Null(durable.Single(t => t.Id == "active").LeasedBy);
        Assert.Equal("owner", durable.Single(t => t.Id == "expired").LeasedBy);
    }

    [Fact]
    public async Task UpdateWithVersionCheckAsync_FreshUpdateWinsAndStaleUpdatesDoNotMutate()
    {
        var seeded = DurableLifecycleTestData.NewAsyncTask(
            "versioned", DateTime.UtcNow.AddMinutes(-1));
        seeded.Version = 3;
        seeded.Progress = 10;
        await SeedTasksAsync(seeded);

        AsyncTask fresh;
        AsyncTask stale;
        await using (var context = _database.CreateContext())
        {
            fresh = await context.AsyncTasks.AsNoTracking().SingleAsync();
            stale = await context.AsyncTasks.AsNoTracking().SingleAsync();
        }

        fresh.Progress = 40;
        Assert.True(await _repository.UpdateWithVersionCheckAsync(fresh, 3));
        stale.Progress = 90;
        Assert.False(await _repository.UpdateWithVersionCheckAsync(stale, 3));
        var missing = DurableLifecycleTestData.NewAsyncTask(
            "missing", DateTime.UtcNow);
        missing.Progress = 100;
        Assert.False(await _repository.UpdateWithVersionCheckAsync(missing, 0));

        await using var verification = _database.CreateContext();
        var durable = await verification.AsyncTasks.AsNoTracking().SingleAsync();
        Assert.Equal(40, durable.Progress);
        Assert.Equal(4, durable.Version);
        Assert.False(await verification.AsyncTasks.AnyAsync(t => t.Id == "missing"));
    }

    [Fact]
    public async Task ProviderPhases_RequireOwningWorkerAndPersistProviderData()
    {
        var task = DurableLifecycleTestData.NewAsyncTask(
            "provider-phase", DateTime.UtcNow.AddMinutes(-1), state: 1);
        task.LeasedBy = "owner";
        task.LeaseExpiryTime = DateTime.UtcNow.AddMinutes(10);
        await SeedTasksAsync(task);

        Assert.False(await _repository.MarkProviderInvocationStartedAsync(
            "provider-phase", "other"));
        Assert.True(await _repository.MarkProviderInvocationStartedAsync(
            "provider-phase", "owner"));
        Assert.False(await _repository.MarkProviderInvocationCompletedAsync(
            "provider-phase", "other", "wrong-operation"));
        Assert.True(await _repository.MarkProviderInvocationCompletedAsync(
            "provider-phase", "owner", "provider-operation-42"));

        await using var verification = _database.CreateContext();
        var durable = await verification.AsyncTasks.AsNoTracking().SingleAsync();
        Assert.NotNull(durable.ProviderInvocationStartedAt);
        Assert.NotNull(durable.ProviderInvocationCompletedAt);
        Assert.Equal("provider-operation-42", durable.ProviderOperationId);
    }

    [Fact]
    public async Task TryClaimTaskAsync_ReturnsOutcomeForEveryLifecycleCategory()
    {
        var now = DateTime.UtcNow;
        var terminal = DurableLifecycleTestData.NewAsyncTask("terminal", now, state: 2);
        var claimed = DurableLifecycleTestData.NewAsyncTask("claimed", now, state: 1);
        claimed.LeasedBy = "worker";
        claimed.LeaseExpiryTime = now.AddMinutes(10);
        var indeterminate = DurableLifecycleTestData.NewAsyncTask("indeterminate", now, state: 6);
        await SeedTasksAsync(terminal, claimed, indeterminate);

        Assert.Equal(AsyncTaskClaimResult.Missing,
            await _repository.TryClaimTaskAsync("missing", "new-worker", TimeSpan.FromMinutes(5)));
        Assert.Equal(AsyncTaskClaimResult.Terminal,
            await _repository.TryClaimTaskAsync("terminal", "new-worker", TimeSpan.FromMinutes(5)));
        Assert.Equal(AsyncTaskClaimResult.AlreadyClaimed,
            await _repository.TryClaimTaskAsync("claimed", "new-worker", TimeSpan.FromMinutes(5)));
        Assert.Equal(AsyncTaskClaimResult.Indeterminate,
            await _repository.TryClaimTaskAsync("indeterminate", "new-worker", TimeSpan.FromMinutes(5)));
    }

    [Fact]
    public async Task ResolveIndeterminateTaskAsync_ResetsRetryOrRecordsTerminalOutcome()
    {
        var now = DateTime.UtcNow;
        var retry = NewIndeterminateTask("retry", now);
        var terminal = NewIndeterminateTask("terminal", now);
        await SeedTasksAsync(retry, terminal);

        Assert.True(await _repository.ResolveIndeterminateTaskAsync(
            "retry", targetState: 0, isRetryable: true, reason: "safe to retry",
            providerOperationId: "replacement-id"));
        Assert.True(await _repository.ResolveIndeterminateTaskAsync(
            "terminal", targetState: 3, isRetryable: false, reason: "provider failed",
            providerOperationId: "final-id"));

        await using var verification = _database.CreateContext();
        var rows = await verification.AsyncTasks.AsNoTracking().ToListAsync();
        var retryRow = rows.Single(t => t.Id == "retry");
        Assert.Equal(0, retryRow.State);
        Assert.True(retryRow.IsRetryable);
        Assert.Equal(3, retryRow.RetryCount);
        Assert.Null(retryRow.LeasedBy);
        Assert.Null(retryRow.LeaseExpiryTime);
        Assert.Null(retryRow.ProviderInvocationStartedAt);
        Assert.Null(retryRow.ProviderInvocationCompletedAt);
        Assert.Null(retryRow.ProviderOperationId);
        Assert.Null(retryRow.CompletedAt);
        Assert.Null(retryRow.NextRetryAt);

        var terminalRow = rows.Single(t => t.Id == "terminal");
        Assert.Equal(3, terminalRow.State);
        Assert.False(terminalRow.IsRetryable);
        Assert.Equal("final-id", terminalRow.ProviderOperationId);
        Assert.NotNull(terminalRow.CompletedAt);
        Assert.Equal("provider failed", terminalRow.Error);
    }

    [Fact]
    public async Task GetExpiredLeaseTasksAsync_EnforcesStateTimeAndLimit()
    {
        var now = DateTime.UtcNow;
        var first = NewLeasedTask("first", now.AddMinutes(-20), 1, now.AddMinutes(-10));
        var second = NewLeasedTask("second", now.AddMinutes(-15), 1, now.AddMinutes(-5));
        var pending = NewLeasedTask("pending", now.AddMinutes(-30), 0, now.AddMinutes(-20));
        var active = NewLeasedTask("active", now.AddMinutes(-40), 1, now.AddMinutes(20));
        await SeedTasksAsync(first, second, pending, active);

        var expired = await _repository.GetExpiredLeaseTasksAsync(limit: 1);

        Assert.Equal(new[] { "first" }, expired.Select(t => t.Id));
    }

    [Fact]
    public async Task ArchiveCleanupAndBulkDelete_OnlyAffectEligibleRows()
    {
        var now = DateTime.UtcNow;
        var oldCompleted = DurableLifecycleTestData.NewAsyncTask("old-completed", now.AddDays(-10), state: 2);
        oldCompleted.CompletedAt = now.AddDays(-5);
        var recentCompleted = DurableLifecycleTestData.NewAsyncTask("recent-completed", now.AddDays(-2), state: 2);
        recentCompleted.CompletedAt = now.AddHours(-2);
        var oldProcessing = DurableLifecycleTestData.NewAsyncTask("old-processing", now.AddDays(-10), state: 1);
        oldProcessing.CompletedAt = now.AddDays(-5);
        var cleanup = DurableLifecycleTestData.NewAsyncTask("cleanup", now.AddDays(-20), state: 2);
        cleanup.CompletedAt = now.AddDays(-20);
        cleanup.IsArchived = true;
        cleanup.ArchivedAt = now.AddDays(-10);
        var recentArchive = DurableLifecycleTestData.NewAsyncTask("recent-archive", now.AddDays(-2), state: 2);
        recentArchive.CompletedAt = now.AddDays(-2);
        recentArchive.IsArchived = true;
        recentArchive.ArchivedAt = now.AddHours(-2);
        await SeedTasksAsync(oldCompleted, recentCompleted, oldProcessing, cleanup, recentArchive);

        Assert.Equal(1, await _repository.ArchiveOldTasksAsync(TimeSpan.FromDays(1)));
        var cleanupRows = await _repository.GetTasksForCleanupAsync(
            TimeSpan.FromDays(1), limit: 10);
        Assert.Equal(new[] { "cleanup" }, cleanupRows.Select(t => t.Id));
        Assert.Equal(1, await _repository.BulkDeleteAsync(
            cleanupRows.Select(t => t.Id).Append("missing")));

        await using var verification = _database.CreateContext();
        var rows = await verification.AsyncTasks.AsNoTracking().ToListAsync();
        Assert.DoesNotContain(rows, t => t.Id == "cleanup");
        Assert.True(rows.Single(t => t.Id == "old-completed").IsArchived);
        Assert.False(rows.Single(t => t.Id == "recent-completed").IsArchived);
        Assert.False(rows.Single(t => t.Id == "old-processing").IsArchived);
        Assert.True(rows.Single(t => t.Id == "recent-archive").IsArchived);
    }

    private async Task SeedTasksAsync(params AsyncTask[] tasks)
    {
        await _database.SeedAsync(async context =>
        {
            context.AsyncTasks.AddRange(tasks);
            await context.SaveChangesAsync();
        });
    }

    private static AsyncTask NewIndeterminateTask(string id, DateTime now)
    {
        var task = DurableLifecycleTestData.NewAsyncTask(id, now.AddMinutes(-10), state: 6);
        task.RetryCount = 2;
        task.LeasedBy = "dead-worker";
        task.LeaseExpiryTime = now.AddMinutes(-5);
        task.ProviderInvocationStartedAt = now.AddMinutes(-8);
        task.ProviderInvocationCompletedAt = now.AddMinutes(-7);
        task.ProviderOperationId = "original-id";
        task.CompletedAt = now.AddMinutes(-5);
        task.NextRetryAt = now.AddMinutes(10);
        return task;
    }

    private static AsyncTask NewLeasedTask(
        string id,
        DateTime createdAt,
        int state,
        DateTime expiry)
    {
        var task = DurableLifecycleTestData.NewAsyncTask(id, createdAt, state: state);
        task.LeasedBy = "worker";
        task.LeaseExpiryTime = expiry;
        return task;
    }
}
