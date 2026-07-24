using ConduitLLM.Core.Services;

using Microsoft.Extensions.Logging.Abstractions;

using StackExchange.Redis;

using Xunit;

namespace ConduitLLM.Tests.Core.Services;

/// <summary>
/// Executes the limiter's Lua script against a real Redis. Everything here was previously
/// covered only by asserting on the script's source text, which cannot catch a logic error.
/// </summary>
/// <remarks>
/// Time is passed in rather than read from the clock, so window boundaries are tested at the
/// millisecond instead of being approximated by deleting keys or sleeping.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "SlidingWindowRateLimiter")]
public class SlidingWindowRateLimiterRedisTests : IAsyncLifetime
{
    private const long T0 = 1_700_000_000_000;
    private const int Minute = 60_000;

    private readonly string _prefix = RedisTestServer.NewKeyPrefix("sliding-window");

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => RedisTestServer.CleanupAsync(_prefix);

    [SkippableFact]
    public async Task CheckAsync_AtWindowBoundary_DeniesJustInsideAndAllowsJustOutside()
    {
        var limiter = NewLimiter();
        var window = Window("boundary", limit: 1);

        Assert.True((await limiter.CheckAsync(new[] { window }, T0)).IsAllowed);

        // 100ms short of the window: the first request is still counted.
        Assert.False((await limiter.CheckAsync(new[] { window }, T0 + Minute - 100)).IsAllowed);

        // 100ms past it: the first request has aged out.
        Assert.True((await limiter.CheckAsync(new[] { window }, T0 + Minute + 100)).IsAllowed);
    }

    [SkippableFact]
    public async Task CheckAsync_Denied_ReportsWhenTheOldestEntryAgesOut()
    {
        var limiter = NewLimiter();
        var window = Window("oldest", limit: 2);

        await limiter.CheckAsync(new[] { window }, T0);
        await limiter.CheckAsync(new[] { window }, T0 + 5_000);

        var denied = await limiter.CheckAsync(new[] { window }, T0 + 10_000);

        Assert.False(denied.IsAllowed);
        // Capacity returns when the T0 request leaves — not at "now + 60s", which would be
        // 50 seconds too late.
        Assert.Equal(
            DateTimeOffset.FromUnixTimeMilliseconds(T0 + Minute).UtcDateTime,
            denied.DeniedWindow!.ResetsAt);
    }

    [SkippableFact]
    public async Task CheckAsync_RetryingAtTheAdvertisedInstant_Succeeds()
    {
        // The whole point of the reset instant: a client that waits exactly that long and
        // retries once must get through. Driving `now` makes that exact rather than approximate.
        var limiter = NewLimiter();
        var window = Window("retry-at", limit: 3);

        await limiter.CheckAsync(new[] { window }, T0);
        await limiter.CheckAsync(new[] { window }, T0 + 7_000);
        await limiter.CheckAsync(new[] { window }, T0 + 12_000);

        var denied = await limiter.CheckAsync(new[] { window }, T0 + 20_000);
        Assert.False(denied.IsAllowed);

        var retryAt = new DateTimeOffset(denied.DeniedWindow!.ResetsAt).ToUnixTimeMilliseconds();

        // One millisecond early is still too early — the boundary is not fudged.
        Assert.False((await limiter.CheckAsync(new[] { window }, retryAt - 1)).IsAllowed);
        Assert.True((await limiter.CheckAsync(new[] { window }, retryAt)).IsAllowed);
    }

    [SkippableFact]
    public async Task CheckAsync_DeniedByALaterWindow_DoesNotConsumeTheEarlierOne()
    {
        var limiter = NewLimiter();
        var minute = Window("all-or-nothing-rpm", limit: 10);
        var day = new RateLimitWindow(_prefix + "all-or-nothing-rpd", "RPD", 86_400_000, 1);

        Assert.True((await limiter.CheckAsync(new[] { minute, day }, T0)).IsAllowed);

        var denied = await limiter.CheckAsync(new[] { minute, day }, T0 + 1_000);
        Assert.False(denied.IsAllowed);
        Assert.Equal("RPD", denied.DeniedWindow!.Scope);

        // The minute window must still hold exactly one entry: the rejected request was not
        // admitted anywhere, so it cost no per-minute quota.
        Assert.Equal(1, await Db().SortedSetLengthAsync(minute.Key));
        Assert.Equal(1, (long)await Db().StringGetAsync(minute.Key + ":sum"));
    }

    [SkippableFact]
    public async Task CheckAsync_WeightedWindow_AdmitsOnlyWhatFitsInTheBudget()
    {
        var limiter = NewLimiter();

        static RateLimitWindow Tpm(string key, long weight) =>
            new(key, "TPM", Minute, 100_000, weight, UnitWeight: false);

        var key = _prefix + "tpm";
        Assert.True((await limiter.CheckAsync(new[] { Tpm(key, 40_000) }, T0)).IsAllowed);
        Assert.True((await limiter.CheckAsync(new[] { Tpm(key, 40_000) }, T0 + 1_000)).IsAllowed);

        var denied = await limiter.CheckAsync(new[] { Tpm(key, 40_000) }, T0 + 2_000);

        Assert.False(denied.IsAllowed);
        Assert.Equal(80_000, denied.DeniedWindow!.Current);
        Assert.Equal(20_000, denied.DeniedWindow.Remaining);

        // 20k of the 40k needed is freed by the first entry alone, so that is the wait.
        Assert.Equal(
            DateTimeOffset.FromUnixTimeMilliseconds(T0 + Minute).UtcDateTime,
            denied.DeniedWindow.ResetsAt);
    }

    [SkippableFact]
    public async Task CheckAsync_WeightedWindow_WaitsForEnoughWeightNotJustTheOldestEntry()
    {
        var limiter = NewLimiter();
        var key = _prefix + "tpm-walk";

        static RateLimitWindow Tpm(string key, long weight) =>
            new(key, "TPM", Minute, 100_000, weight, UnitWeight: false);

        // 60k of the 100k budget is in flight across three reservations.
        await limiter.CheckAsync(new[] { Tpm(key, 20_000) }, T0);
        await limiter.CheckAsync(new[] { Tpm(key, 20_000) }, T0 + 5_000);
        await limiter.CheckAsync(new[] { Tpm(key, 20_000) }, T0 + 9_000);

        // A 60k request overshoots by 20k, which the oldest reservation alone covers.
        var denied = await limiter.CheckAsync(new[] { Tpm(key, 60_000) }, T0 + 10_000);

        Assert.False(denied.IsAllowed);
        Assert.Equal(
            DateTimeOffset.FromUnixTimeMilliseconds(T0 + Minute).UtcDateTime,
            denied.DeniedWindow!.ResetsAt);

        // A 100k request overshoots by the full 60k, so it must wait for all three to age out —
        // reporting the oldest entry here would send the client back far too early.
        var deniedLarger = await limiter.CheckAsync(new[] { Tpm(key, 100_000) }, T0 + 10_000);
        Assert.False(deniedLarger.IsAllowed);
        Assert.Equal(
            DateTimeOffset.FromUnixTimeMilliseconds(T0 + 9_000 + Minute).UtcDateTime,
            deniedLarger.DeniedWindow!.ResetsAt);
    }

    [SkippableFact]
    public async Task ReconcileAsync_LoweringAReservationReturnsTheDifference()
    {
        var limiter = NewLimiter();
        var key = _prefix + "reconcile";
        var reservation = new RateLimitWindow(key, "TPM", Minute, 100_000, 90_000, UnitWeight: false);

        var reserved = await limiter.CheckAsync(new[] { reservation }, T0);
        Assert.True(reserved.IsAllowed);
        Assert.NotNull(reserved.EntryId);

        // The request actually used 10k of the 90k reserved.
        Assert.True(await limiter.ReconcileAsync(key, reserved.EntryId!, 90_000, 10_000));

        var next = new RateLimitWindow(key, "TPM", Minute, 100_000, 50_000, UnitWeight: false);
        var second = await limiter.CheckAsync(new[] { next }, T0 + 1_000);

        Assert.True(second.IsAllowed);
        Assert.Equal(60_000, second.Windows[0].Current);
    }

    [SkippableFact]
    public async Task ReconcileAsync_AfterTheWindowSlidPast_LeavesTheTotalAlone()
    {
        var limiter = NewLimiter();
        var key = _prefix + "reconcile-expired";
        var reservation = new RateLimitWindow(key, "TPM", Minute, 100_000, 30_000, UnitWeight: false);

        var reserved = await limiter.CheckAsync(new[] { reservation }, T0);

        // A later check evicts the reservation; reconciling it now must not drive the total negative.
        var fresh = new RateLimitWindow(key, "TPM", Minute, 100_000, 1_000, UnitWeight: false);
        await limiter.CheckAsync(new[] { fresh }, T0 + Minute + 1_000);

        Assert.False(await limiter.ReconcileAsync(key, reserved.EntryId!, 30_000, 5_000));
        Assert.Equal(1_000, (long)await Db().StringGetAsync(key + ":sum"));
    }

    [SkippableFact]
    public async Task ReleaseAsync_FreesTheEntryAndIsIdempotent()
    {
        var limiter = NewLimiter();
        var key = _prefix + "release";
        var slot = new RateLimitWindow(key, "concurrency", Minute, 1);

        var first = await limiter.CheckAsync(new[] { slot }, T0);
        Assert.True(first.IsAllowed);
        Assert.False((await limiter.CheckAsync(new[] { slot }, T0 + 100)).IsAllowed);

        Assert.True(await limiter.ReleaseAsync(key, first.EntryId!, 1));

        var second = await limiter.CheckAsync(new[] { slot }, T0 + 200);
        Assert.True(second.IsAllowed);

        // A double release must report that it did nothing rather than free the new holder's slot.
        Assert.False(await limiter.ReleaseAsync(key, first.EntryId!, 1));
        Assert.Equal(1, await Db().SortedSetLengthAsync(key));
    }

    [SkippableFact]
    public async Task CheckAsync_TotalLeftOverFromAnExpiredWindow_SelfHeals()
    {
        var limiter = NewLimiter();
        var key = _prefix + "drift";
        var window = new RateLimitWindow(key, "RPM", Minute, 10);

        // Simulate the sorted set expiring while its companion total survived.
        await Db().StringSetAsync(key + ":sum", 9_999);

        var result = await limiter.CheckAsync(new[] { window }, T0);

        Assert.True(result.IsAllowed);
        Assert.Equal(1, result.Windows[0].Current);
    }

    [SkippableFact]
    public async Task CheckAsync_ExpiresTheWindowAndItsTotalTogether()
    {
        var limiter = NewLimiter();
        var key = _prefix + "ttl";

        await limiter.CheckAsync(new[] { new RateLimitWindow(key, "RPM", Minute, 10) }, T0);

        var windowTtl = await Db().KeyTimeToLiveAsync(key);
        var sumTtl = await Db().KeyTimeToLiveAsync(key + ":sum");

        Assert.NotNull(windowTtl);
        Assert.NotNull(sumTtl);
        // window seconds + 60s of slack, on both keys — a total that outlived its window would
        // over-count the next one.
        Assert.InRange(windowTtl!.Value.TotalSeconds, 115, 121);
        Assert.InRange(sumTtl!.Value.TotalSeconds, 115, 121);
    }

    [SkippableFact]
    public async Task CheckAsync_ConcurrentBurstAtTheLimit_AdmitsExactlyTheLimit()
    {
        var limiter = NewLimiter();
        var window = Window("burst", limit: 20);

        // 100 simultaneous callers against a ceiling of 20. Check-then-act would let extras
        // through; the script's single round-trip must not.
        var attempts = Enumerable.Range(0, 100)
            .Select(i => limiter.CheckAsync(new[] { window }, T0 + i))
            .ToArray();

        var results = await Task.WhenAll(attempts);

        Assert.Equal(20, results.Count(r => r.IsAllowed));
        Assert.Equal(20, await Db().SortedSetLengthAsync(window.Key));
        Assert.Equal(20, (long)await Db().StringGetAsync(window.Key + ":sum"));
    }

    [SkippableFact]
    public async Task CheckAsync_Allowed_ReportsWhenThisRequestsOwnSlotFrees()
    {
        var limiter = NewLimiter();
        var window = Window("reset-on-allow", limit: 5);

        var result = await limiter.CheckAsync(new[] { window }, T0);

        Assert.True(result.IsAllowed);
        Assert.Equal(
            DateTimeOffset.FromUnixTimeMilliseconds(T0 + Minute).UtcDateTime,
            result.Windows[0].ResetsAt);
        Assert.Equal(4, result.Windows[0].Remaining);
    }

    private RateLimitWindow Window(string suffix, int limit) =>
        new(_prefix + suffix, "RPM", Minute, limit);

    private static IDatabase Db() => RedisTestServer.Require().GetDatabase();

    private static SlidingWindowRateLimiter NewLimiter() =>
        new(RedisTestServer.Require(), NullLogger.Instance);
}
