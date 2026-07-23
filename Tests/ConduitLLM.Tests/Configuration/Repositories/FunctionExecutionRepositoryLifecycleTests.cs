using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Functions.Entities;
using ConduitLLM.Functions.Enums;
using ConduitLLM.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ConduitLLM.Tests.Configuration.Repositories;

public sealed class FunctionExecutionRepositoryLifecycleTests : IAsyncLifetime
{
    private readonly FailingSaveChangesInterceptor _saveFailure = new();
    private SqliteTestDatabase _database = null!;
    private FunctionExecutionRepository _repository = null!;

    public async Task InitializeAsync()
    {
        _database = new SqliteTestDatabase(_saveFailure);
        await _database.SeedAsync(DurableLifecycleTestData.SeedRequiredGraphAsync);
        _repository = new FunctionExecutionRepository(
            _database.CreateDbContextFactory(),
            NullLogger<FunctionExecutionRepository>.Instance);
    }

    public Task DisposeAsync() => _database.DisposeAsync().AsTask();

    [Fact]
    public async Task CreateAndReadAsync_PersistValidGraphAndIncludeConfiguration()
    {
        var execution = DurableLifecycleTestData.NewFunctionExecution(
            DateTime.UtcNow.AddMinutes(-1), id: Guid.Empty);

        var id = await _repository.CreateAsync(execution);
        var loaded = await _repository.GetByIdAsync(id);

        Assert.NotEqual(Guid.Empty, id);
        Assert.NotNull(loaded);
        Assert.Equal(DurableLifecycleTestData.VirtualKeyId, loaded.VirtualKeyId);
        Assert.Equal("""{"query":"test"}""", loaded.RequestJson);
        Assert.NotNull(loaded.FunctionConfiguration);
        Assert.Equal("Durable lifecycle test function",
            loaded.FunctionConfiguration.ConfigurationName);
    }

    [Fact]
    public async Task LeaseNextPendingAsync_SelectsOldestEligibleAndReclaimsExpiredLease()
    {
        var now = DateTime.UtcNow;
        var activeOldest = DurableLifecycleTestData.NewFunctionExecution(now.AddHours(-3));
        activeOldest.LeasedBy = "active-worker";
        activeOldest.LeaseExpiryTime = now.AddHours(1);
        var expired = DurableLifecycleTestData.NewFunctionExecution(now.AddHours(-2));
        expired.LeasedBy = "dead-worker";
        expired.LeaseExpiryTime = now.AddMinutes(-10);
        expired.Version = 7;
        var unleased = DurableLifecycleTestData.NewFunctionExecution(now.AddHours(-1));
        await SeedExecutionsAsync(activeOldest, expired, unleased);

        var leased = await _repository.LeaseNextPendingAsync(
            "replacement", TimeSpan.FromMinutes(5));

        Assert.NotNull(leased);
        Assert.Equal(expired.Id, leased.Id);
        await using var verification = _database.CreateContext();
        var durable = await verification.FunctionExecutions.AsNoTracking()
            .SingleAsync(e => e.Id == expired.Id);
        Assert.Equal("replacement", durable.LeasedBy);
        Assert.True(durable.LeaseExpiryTime > now.AddMinutes(4));
        Assert.Equal(8, durable.Version);
    }

    [Fact]
    public async Task LeaseNextPendingAsync_ConcurrentWorkers_HaveOneWinner()
    {
        var execution = DurableLifecycleTestData.NewFunctionExecution(
            DateTime.UtcNow.AddMinutes(-5));
        await SeedExecutionsAsync(execution);

        var results = await Task.WhenAll(
            _repository.LeaseNextPendingAsync("worker-1", TimeSpan.FromMinutes(5)),
            _repository.LeaseNextPendingAsync("worker-2", TimeSpan.FromMinutes(5)));

        Assert.Single(results, result => result != null);
        await using var verification = _database.CreateContext();
        var durable = await verification.FunctionExecutions.AsNoTracking().SingleAsync();
        Assert.Contains(durable.LeasedBy, new[] { "worker-1", "worker-2" });
        Assert.Equal(1, durable.Version);
    }

    [Fact]
    public async Task UpdateAsync_FreshDetachedUpdateWinsAndStaleUpdateCannotOverwriteIt()
    {
        var execution = DurableLifecycleTestData.NewFunctionExecution(
            DateTime.UtcNow.AddMinutes(-5));
        execution.Version = 4;
        execution.StatusMessage = "seeded";
        await SeedExecutionsAsync(execution);

        FunctionExecution fresh;
        FunctionExecution stale;
        await using (var context = _database.CreateContext())
        {
            fresh = await context.FunctionExecutions.AsNoTracking().SingleAsync();
            stale = await context.FunctionExecutions.AsNoTracking().SingleAsync();
        }

        fresh.StatusMessage = "fresh";
        Assert.True(await _repository.UpdateAsync(fresh));
        stale.StatusMessage = "stale";
        Assert.False(await _repository.UpdateAsync(stale));

        await using var verification = _database.CreateContext();
        var durable = await verification.FunctionExecutions.AsNoTracking().SingleAsync();
        Assert.Equal("fresh", durable.StatusMessage);
        Assert.Equal(5, durable.Version);
    }

    [Fact]
    public async Task UpdateStateAsync_PopulatesLifecycleTimestampsDurationAndErrors()
    {
        var failed = DurableLifecycleTestData.NewFunctionExecution(
            DateTime.UtcNow.AddMinutes(-5));
        var timedOut = DurableLifecycleTestData.NewFunctionExecution(
            DateTime.UtcNow.AddMinutes(-10), ExecutionState.Running);
        timedOut.StartedAt = DateTime.UtcNow.AddMinutes(-2);
        await SeedExecutionsAsync(failed, timedOut);

        await _repository.UpdateStateAsync(failed.Id, ExecutionState.Running);
        await _repository.UpdateStateAsync(
            failed.Id, ExecutionState.Failed, "provider unavailable");
        await _repository.UpdateStateAsync(
            timedOut.Id, ExecutionState.TimedOut, "deadline exceeded");

        await using var verification = _database.CreateContext();
        var rows = await verification.FunctionExecutions.AsNoTracking().ToListAsync();
        var failedRow = rows.Single(e => e.Id == failed.Id);
        Assert.Equal(ExecutionState.Failed, failedRow.State);
        Assert.NotNull(failedRow.StartedAt);
        Assert.NotNull(failedRow.CompletedAt);
        Assert.NotNull(failedRow.Duration);
        Assert.True(failedRow.Duration >= TimeSpan.Zero);
        Assert.Equal("provider unavailable", failedRow.ErrorMessage);
        Assert.Equal(2, failedRow.Version);

        var timeoutRow = rows.Single(e => e.Id == timedOut.Id);
        Assert.NotNull(timeoutRow.CompletedAt);
        Assert.True(timeoutRow.Duration > TimeSpan.FromMinutes(1));
        Assert.Equal("deadline exceeded", timeoutRow.ErrorMessage);
    }

    [Fact]
    public async Task RetryAndExpiredLeaseQueries_EnforceStateAndTimeBoundaries()
    {
        var now = DateTime.UtcNow;
        var expiredPending = NewLeasedExecution(now.AddMinutes(-30), ExecutionState.Pending, now.AddMinutes(-5));
        var expiredRunning = NewLeasedExecution(now.AddMinutes(-29), ExecutionState.Running, now.AddMinutes(-4));
        var expiredCompleted = NewLeasedExecution(now.AddMinutes(-28), ExecutionState.Completed, now.AddMinutes(-3));
        var activePending = NewLeasedExecution(now.AddMinutes(-27), ExecutionState.Pending, now.AddMinutes(10));
        var noLeaseOwner = DurableLifecycleTestData.NewFunctionExecution(now.AddMinutes(-26));
        noLeaseOwner.LeaseExpiryTime = now.AddMinutes(-2);

        var dueRetry = DurableLifecycleTestData.NewFunctionExecution(
            now.AddMinutes(-20), ExecutionState.Failed);
        dueRetry.NextRetryAt = now.AddMinutes(-1);
        var futureRetry = DurableLifecycleTestData.NewFunctionExecution(
            now.AddMinutes(-19), ExecutionState.Failed);
        futureRetry.NextRetryAt = now.AddMinutes(10);
        var pendingWithRetry = DurableLifecycleTestData.NewFunctionExecution(
            now.AddMinutes(-18), ExecutionState.Pending);
        pendingWithRetry.NextRetryAt = now.AddMinutes(-1);
        await SeedExecutionsAsync(
            expiredPending, expiredRunning, expiredCompleted, activePending, noLeaseOwner,
            dueRetry, futureRetry, pendingWithRetry);

        var expired = await _repository.GetExpiredLeasesAsync();
        var retries = await _repository.GetReadyForRetryAsync();

        Assert.Equal(
            new[] { expiredPending.Id, expiredRunning.Id }.OrderBy(id => id),
            expired.Select(e => e.Id).OrderBy(id => id));
        Assert.Equal(new[] { dueRetry.Id }, retries.Select(e => e.Id));
        Assert.All(expired.Concat(retries), e => Assert.NotNull(e.FunctionConfiguration));
    }

    [Fact]
    public async Task UpdateProgressAsync_PersistsProgressMessageAndVersion()
    {
        var execution = DurableLifecycleTestData.NewFunctionExecution(
            DateTime.UtcNow.AddMinutes(-5));
        execution.Version = 2;
        await SeedExecutionsAsync(execution);

        await _repository.UpdateProgressAsync(execution.Id, 55, "halfway");

        await using var verification = _database.CreateContext();
        var durable = await verification.FunctionExecutions.AsNoTracking().SingleAsync();
        Assert.Equal(55, durable.ProgressPercentage);
        Assert.Equal("halfway", durable.StatusMessage);
        Assert.Equal(3, durable.Version);
    }

    [Fact]
    public async Task UpdateProgressAsync_SaveFailureLeavesDurableStateUnchanged()
    {
        var execution = DurableLifecycleTestData.NewFunctionExecution(
            DateTime.UtcNow.AddMinutes(-5));
        execution.Version = 6;
        execution.ProgressPercentage = 20;
        execution.StatusMessage = "before";
        await SeedExecutionsAsync(execution);
        _saveFailure.Arm();

        await _repository.UpdateProgressAsync(execution.Id, 80, "after");
        _saveFailure.Disarm();

        await using var verification = _database.CreateContext();
        var durable = await verification.FunctionExecutions.AsNoTracking().SingleAsync();
        Assert.Equal(20, durable.ProgressPercentage);
        Assert.Equal("before", durable.StatusMessage);
        Assert.Equal(6, durable.Version);
    }

    [Fact]
    public async Task DeleteOldExecutionsAsync_UsesStrictCutoffAndPreservesNewerRows()
    {
        var cutoff = DateTime.UtcNow.AddDays(-7);
        var old = DurableLifecycleTestData.NewFunctionExecution(cutoff.AddHours(-1));
        var boundary = DurableLifecycleTestData.NewFunctionExecution(cutoff);
        var recent = DurableLifecycleTestData.NewFunctionExecution(cutoff.AddHours(1));
        await SeedExecutionsAsync(old, boundary, recent);

        Assert.Equal(1, await _repository.DeleteOldExecutionsAsync(cutoff));

        await using var verification = _database.CreateContext();
        var ids = await verification.FunctionExecutions.AsNoTracking()
            .Select(e => e.Id)
            .ToListAsync();
        Assert.DoesNotContain(old.Id, ids);
        Assert.Contains(boundary.Id, ids);
        Assert.Contains(recent.Id, ids);
        Assert.True(await verification.FunctionConfigurations.AnyAsync(
            c => c.Id == DurableLifecycleTestData.FunctionConfigurationId));
    }

    private async Task SeedExecutionsAsync(params FunctionExecution[] executions)
    {
        await _database.SeedAsync(async context =>
        {
            context.FunctionExecutions.AddRange(executions);
            await context.SaveChangesAsync();
        });
    }

    private static FunctionExecution NewLeasedExecution(
        DateTime requestedAt,
        ExecutionState state,
        DateTime leaseExpiry)
    {
        var execution = DurableLifecycleTestData.NewFunctionExecution(requestedAt, state);
        execution.LeasedBy = "worker";
        execution.LeaseExpiryTime = leaseExpiry;
        return execution;
    }
}
