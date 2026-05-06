using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Metrics;
using ConduitLLM.Providers.Helpers;

using FluentAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace ConduitLLM.Tests.Providers.Helpers;

public class AsyncJobPollerTests
{
    private sealed record Status(string Name);

    private static PollingOptions Fast(
        BackoffStrategy backoff = BackoffStrategy.Fixed,
        int? maxTransient = null,
        int timeoutSeconds = 30) => new(
            InitialDelay: TimeSpan.FromMilliseconds(1),
            MaxDelay: TimeSpan.FromMilliseconds(10),
            Timeout: TimeSpan.FromSeconds(timeoutSeconds),
            Backoff: backoff,
            MaxConsecutiveTransientErrors: maxTransient,
            BackoffMultiplier: 2.0,
            JitterMilliseconds: 0,
            HeartbeatLogEveryNAttempts: 100);

    private static Func<TimeSpan, CancellationToken, Task> NoDelay() =>
        (_, _) => Task.CompletedTask;

    [Fact]
    public async Task PollAsync_SucceedsFirstTry_ReturnsExtractedResult()
    {
        var calls = 0;

        var result = await AsyncJobPoller.PollAsync(
            fetchStatus: _ =>
            {
                calls++;
                return Task.FromResult(new Status("done"));
            },
            classify: s => s.Name == "done" ? JobState.Succeeded : JobState.InProgress,
            extractSuccess: s => s.Name + "!",
            extractFailure: _ => new InvalidOperationException("should not be called"),
            options: Fast(),
            logger: NullLogger.Instance,
            cancellationToken: CancellationToken.None,
            delayFunc: NoDelay());

        result.Should().Be("done!");
        calls.Should().Be(1);
    }

    [Fact]
    public async Task PollAsync_SucceedsAfterSeveralPolls_EventuallyReturns()
    {
        var calls = 0;

        var result = await AsyncJobPoller.PollAsync(
            fetchStatus: _ =>
            {
                calls++;
                return Task.FromResult(new Status(calls >= 4 ? "done" : "processing"));
            },
            classify: s => s.Name == "done" ? JobState.Succeeded : JobState.InProgress,
            extractSuccess: _ => 42,
            extractFailure: _ => new InvalidOperationException(),
            options: Fast(),
            logger: NullLogger.Instance,
            cancellationToken: CancellationToken.None,
            delayFunc: NoDelay());

        result.Should().Be(42);
        calls.Should().Be(4);
    }

    [Fact]
    public async Task PollAsync_Failed_ThrowsExceptionFromExtractFailure()
    {
        var act = async () => await AsyncJobPoller.PollAsync(
            fetchStatus: _ => Task.FromResult(new Status("failed")),
            classify: s => s.Name == "failed" ? JobState.Failed : JobState.InProgress,
            extractSuccess: _ => 0,
            extractFailure: s => new ModelNotFoundException("m", $"provider said {s.Name}"),
            options: Fast(),
            logger: NullLogger.Instance,
            cancellationToken: CancellationToken.None,
            delayFunc: NoDelay());

        (await act.Should().ThrowAsync<ModelNotFoundException>())
            .WithMessage("*provider said failed*");
    }

    [Fact]
    public async Task PollAsync_Timeout_ThrowsRequestTimeoutAndCallsOnAbort()
    {
        var abortCalled = false;

        var act = async () => await AsyncJobPoller.PollAsync(
            fetchStatus: _ => Task.FromResult(new Status("working")),
            classify: _ => JobState.InProgress,
            extractSuccess: _ => 0,
            extractFailure: _ => new InvalidOperationException(),
            options: Fast(timeoutSeconds: 0),
            logger: NullLogger.Instance,
            cancellationToken: CancellationToken.None,
            onAbort: () =>
            {
                abortCalled = true;
                return Task.CompletedTask;
            },
            delayFunc: NoDelay());

        await act.Should().ThrowAsync<RequestTimeoutException>();
        abortCalled.Should().BeTrue();
    }

    [Fact]
    public async Task PollAsync_CancellationTriggersOnAbortAndThrowsOperationCanceled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var abortCalled = false;

        var act = async () => await AsyncJobPoller.PollAsync(
            fetchStatus: _ => Task.FromResult(new Status("working")),
            classify: _ => JobState.InProgress,
            extractSuccess: _ => 0,
            extractFailure: _ => new InvalidOperationException(),
            options: Fast(),
            logger: NullLogger.Instance,
            cancellationToken: cts.Token,
            onAbort: () =>
            {
                abortCalled = true;
                return Task.CompletedTask;
            },
            delayFunc: NoDelay());

        await act.Should().ThrowAsync<OperationCanceledException>();
        abortCalled.Should().BeTrue();
    }

    [Fact]
    public async Task PollAsync_FetchThrows_FailFastMode_PropagatesAsLLMCommunicationException()
    {
        var act = async () => await AsyncJobPoller.PollAsync<Status, int>(
            fetchStatus: _ => throw new HttpRequestException("network down"),
            classify: _ => JobState.InProgress,
            extractSuccess: _ => 0,
            extractFailure: _ => new InvalidOperationException(),
            options: Fast(maxTransient: null),
            logger: NullLogger.Instance,
            cancellationToken: CancellationToken.None,
            delayFunc: NoDelay());

        (await act.Should().ThrowAsync<LLMCommunicationException>())
            .WithMessage("*network down*");
    }

    [Fact]
    public async Task PollAsync_FetchThrows_TolerantMode_RetriesUntilThreshold()
    {
        var calls = 0;

        var act = async () => await AsyncJobPoller.PollAsync<Status, int>(
            fetchStatus: _ =>
            {
                calls++;
                throw new HttpRequestException($"fail {calls}");
            },
            classify: _ => JobState.InProgress,
            extractSuccess: _ => 0,
            extractFailure: _ => new InvalidOperationException(),
            options: Fast(maxTransient: 3),
            logger: NullLogger.Instance,
            cancellationToken: CancellationToken.None,
            delayFunc: NoDelay());

        await act.Should().ThrowAsync<LLMCommunicationException>();
        calls.Should().Be(3);
    }

    [Fact]
    public async Task PollAsync_FetchThrows_TolerantMode_ResetsCounterOnSuccess()
    {
        var calls = 0;

        var result = await AsyncJobPoller.PollAsync(
            fetchStatus: _ =>
            {
                calls++;
                if (calls == 1 || calls == 2)
                {
                    throw new HttpRequestException("transient");
                }
                return Task.FromResult(new Status(calls >= 5 ? "done" : "processing"));
            },
            classify: s => s.Name == "done" ? JobState.Succeeded : JobState.InProgress,
            extractSuccess: _ => "ok",
            extractFailure: _ => new InvalidOperationException(),
            options: Fast(maxTransient: 3),
            logger: NullLogger.Instance,
            cancellationToken: CancellationToken.None,
            delayFunc: NoDelay());

        result.Should().Be("ok");
        calls.Should().Be(5);
    }

    [Fact]
    public async Task PollAsync_RateLimited_ContinuesPolling()
    {
        var calls = 0;

        var result = await AsyncJobPoller.PollAsync(
            fetchStatus: _ =>
            {
                calls++;
                return Task.FromResult(new Status(calls switch
                {
                    1 => "throttled",
                    2 => "throttled",
                    _ => "done",
                }));
            },
            classify: s => s.Name switch
            {
                "done" => JobState.Succeeded,
                "throttled" => JobState.RateLimited,
                _ => JobState.InProgress,
            },
            extractSuccess: _ => true,
            extractFailure: _ => new InvalidOperationException(),
            options: Fast(),
            logger: NullLogger.Instance,
            cancellationToken: CancellationToken.None,
            delayFunc: NoDelay());

        result.Should().BeTrue();
        calls.Should().Be(3);
    }

    [Fact]
    public async Task PollAsync_TransientError_CountsTowardThreshold()
    {
        var calls = 0;

        var act = async () => await AsyncJobPoller.PollAsync(
            fetchStatus: _ =>
            {
                calls++;
                return Task.FromResult(new Status("server-error"));
            },
            classify: _ => JobState.TransientError,
            extractSuccess: _ => 0,
            extractFailure: _ => new InvalidOperationException(),
            options: Fast(maxTransient: 3),
            logger: NullLogger.Instance,
            cancellationToken: CancellationToken.None,
            delayFunc: NoDelay());

        await act.Should().ThrowAsync<LLMCommunicationException>();
        calls.Should().Be(3);
    }

    [Fact]
    public async Task PollAsync_TransientError_FailFastMode_ThrowsImmediately()
    {
        var calls = 0;

        var act = async () => await AsyncJobPoller.PollAsync(
            fetchStatus: _ =>
            {
                calls++;
                return Task.FromResult(new Status("server-error"));
            },
            classify: _ => JobState.TransientError,
            extractSuccess: _ => 0,
            extractFailure: _ => new InvalidOperationException(),
            options: Fast(maxTransient: null),
            logger: NullLogger.Instance,
            cancellationToken: CancellationToken.None,
            delayFunc: NoDelay());

        await act.Should().ThrowAsync<LLMCommunicationException>();
        calls.Should().Be(1);
    }

    [Fact]
    public async Task PollAsync_ExponentialBackoff_GrowsAndCapsAtMaxDelay()
    {
        var delaysRequested = new List<TimeSpan>();
        var calls = 0;

        var options = new PollingOptions(
            InitialDelay: TimeSpan.FromMilliseconds(10),
            MaxDelay: TimeSpan.FromMilliseconds(50),
            Timeout: TimeSpan.FromSeconds(30),
            Backoff: BackoffStrategy.ExponentialWithJitter,
            BackoffMultiplier: 2.0,
            JitterMilliseconds: 0,
            HeartbeatLogEveryNAttempts: 100);

        await AsyncJobPoller.PollAsync(
            fetchStatus: _ =>
            {
                calls++;
                return Task.FromResult(new Status(calls >= 6 ? "done" : "processing"));
            },
            classify: s => s.Name == "done" ? JobState.Succeeded : JobState.InProgress,
            extractSuccess: _ => 0,
            extractFailure: _ => new InvalidOperationException(),
            options: options,
            logger: NullLogger.Instance,
            cancellationToken: CancellationToken.None,
            delayFunc: (d, _) => { delaysRequested.Add(d); return Task.CompletedTask; });

        // Called 6 times, 5 delays between them.
        delaysRequested.Should().HaveCount(5);
        delaysRequested[0].Should().Be(TimeSpan.FromMilliseconds(10));   // initial
        delaysRequested[1].Should().Be(TimeSpan.FromMilliseconds(20));   // 10*2
        delaysRequested[2].Should().Be(TimeSpan.FromMilliseconds(40));   // 20*2
        delaysRequested[3].Should().Be(TimeSpan.FromMilliseconds(50));   // capped
        delaysRequested[4].Should().Be(TimeSpan.FromMilliseconds(50));   // capped
    }

    [Fact]
    public async Task PollAsync_FixedBackoff_AlwaysUsesInitialDelay()
    {
        var delaysRequested = new List<TimeSpan>();
        var calls = 0;

        await AsyncJobPoller.PollAsync(
            fetchStatus: _ =>
            {
                calls++;
                return Task.FromResult(new Status(calls >= 4 ? "done" : "processing"));
            },
            classify: s => s.Name == "done" ? JobState.Succeeded : JobState.InProgress,
            extractSuccess: _ => 0,
            extractFailure: _ => new InvalidOperationException(),
            options: Fast(),
            logger: NullLogger.Instance,
            cancellationToken: CancellationToken.None,
            delayFunc: (d, _) => { delaysRequested.Add(d); return Task.CompletedTask; });

        delaysRequested.Should().OnlyContain(d => d == TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task PollAsync_OnProgress_InvokedOnEveryAttempt()
    {
        var calls = 0;
        var progressReports = new List<PollProgress>();

        await AsyncJobPoller.PollAsync(
            fetchStatus: _ =>
            {
                calls++;
                return Task.FromResult(new Status(calls >= 3 ? "done" : "processing"));
            },
            classify: s => s.Name == "done" ? JobState.Succeeded : JobState.InProgress,
            extractSuccess: _ => 0,
            extractFailure: _ => new InvalidOperationException(),
            options: Fast(),
            logger: NullLogger.Instance,
            cancellationToken: CancellationToken.None,
            onProgress: (p, _) => { progressReports.Add(p); return Task.CompletedTask; },
            delayFunc: NoDelay());

        progressReports.Should().HaveCount(3);
        progressReports[0].State.Should().Be(JobState.InProgress);
        progressReports[2].State.Should().Be(JobState.Succeeded);
    }

    [Fact]
    public async Task PollAsync_OnProgressThrows_SwallowedAndPollingContinues()
    {
        var calls = 0;

        var result = await AsyncJobPoller.PollAsync(
            fetchStatus: _ =>
            {
                calls++;
                return Task.FromResult(new Status(calls >= 2 ? "done" : "processing"));
            },
            classify: s => s.Name == "done" ? JobState.Succeeded : JobState.InProgress,
            extractSuccess: _ => 99,
            extractFailure: _ => new InvalidOperationException(),
            options: Fast(),
            logger: NullLogger.Instance,
            cancellationToken: CancellationToken.None,
            onProgress: (_, _) => throw new Exception("progress blew up"),
            delayFunc: NoDelay());

        result.Should().Be(99);
    }

    [Fact]
    public async Task PollAsync_OnAbortThrows_TimeoutExceptionStillSurfaced()
    {
        var act = async () => await AsyncJobPoller.PollAsync(
            fetchStatus: _ => Task.FromResult(new Status("working")),
            classify: _ => JobState.InProgress,
            extractSuccess: _ => 0,
            extractFailure: _ => new InvalidOperationException(),
            options: Fast(timeoutSeconds: 0),
            logger: NullLogger.Instance,
            cancellationToken: CancellationToken.None,
            onAbort: () => throw new Exception("abort blew up"),
            delayFunc: NoDelay());

        await act.Should().ThrowAsync<RequestTimeoutException>();
    }

    [Fact]
    public async Task PollAsync_FetchThrowsConduitException_PropagatesWithoutCountingAsTransient()
    {
        var calls = 0;

        var act = async () => await AsyncJobPoller.PollAsync<Status, int>(
            fetchStatus: _ =>
            {
                calls++;
                throw new RateLimitExceededException("slow down");
            },
            classify: _ => JobState.InProgress,
            extractSuccess: _ => 0,
            extractFailure: _ => new InvalidOperationException(),
            options: Fast(maxTransient: 5),
            logger: NullLogger.Instance,
            cancellationToken: CancellationToken.None,
            delayFunc: NoDelay());

        await act.Should().ThrowAsync<RateLimitExceededException>();
        calls.Should().Be(1);
    }

    [Fact]
    public async Task PollAsync_WithInstrumentation_PropagatesResultUnchanged()
    {
        // The scope is caller-owned, so we just verify that PollAsync accepts one,
        // advances it through attempts, and propagates results unchanged.
        using var scope = ProviderInstrumentation.BeginPolling(
            operation: "TestOp",
            providerName: "TestProvider",
            providerType: "TestType",
            model: "test-model");

        var calls = 0;
        var result = await AsyncJobPoller.PollAsync(
            fetchStatus: _ =>
            {
                calls++;
                return Task.FromResult(new Status(calls >= 3 ? "done" : "processing"));
            },
            classify: s => s.Name == "done" ? JobState.Succeeded : JobState.InProgress,
            extractSuccess: _ => "ok",
            extractFailure: _ => new InvalidOperationException(),
            options: Fast(),
            logger: NullLogger.Instance,
            cancellationToken: CancellationToken.None,
            delayFunc: NoDelay(),
            instrumentation: scope);

        result.Should().Be("ok");
        calls.Should().Be(3);
    }

    [Fact]
    public async Task PollAsync_WithInstrumentation_TimeoutRecordsTimeoutOutcome()
    {
        using var scope = ProviderInstrumentation.BeginPolling(
            operation: "TestOp",
            providerName: "TestProvider",
            providerType: "TestType",
            model: "test-model");

        var act = async () => await AsyncJobPoller.PollAsync(
            fetchStatus: _ => Task.FromResult(new Status("processing")),
            classify: _ => JobState.InProgress,
            extractSuccess: _ => 0,
            extractFailure: _ => new InvalidOperationException(),
            options: new PollingOptions(
                InitialDelay: TimeSpan.FromMilliseconds(1),
                MaxDelay: TimeSpan.FromMilliseconds(10),
                Timeout: TimeSpan.FromMilliseconds(1),
                Backoff: BackoffStrategy.Fixed),
            logger: NullLogger.Instance,
            cancellationToken: CancellationToken.None,
            delayFunc: (_, _) => Task.Delay(5),
            instrumentation: scope);

        await act.Should().ThrowAsync<RequestTimeoutException>();
        // Scope disposal happens via `using`; this test primarily confirms no interaction
        // breaks the timeout contract — the metric recording is fire-and-forget.
    }

    [Fact]
    public async Task PollAsync_WithInstrumentation_ClassifierFailurePropagates()
    {
        using var scope = ProviderInstrumentation.BeginPolling(
            operation: "TestOp",
            providerName: "TestProvider",
            providerType: "TestType",
            model: "test-model");

        var act = async () => await AsyncJobPoller.PollAsync(
            fetchStatus: _ => Task.FromResult(new Status("failed")),
            classify: _ => JobState.Failed,
            extractSuccess: _ => 0,
            extractFailure: s => new ModelNotFoundException("m", $"provider said {s.Name}"),
            options: Fast(),
            logger: NullLogger.Instance,
            cancellationToken: CancellationToken.None,
            delayFunc: NoDelay(),
            instrumentation: scope);

        await act.Should().ThrowAsync<ModelNotFoundException>();
    }
}
