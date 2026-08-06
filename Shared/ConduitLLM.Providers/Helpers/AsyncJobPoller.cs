using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Metrics;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.Helpers
{
    /// <summary>
    /// Classification of an async job's current state as determined by the caller's status classifier.
    /// </summary>
    public enum JobState
    {
        /// <summary>Still running. Wait and poll again with normal backoff.</summary>
        InProgress,

        /// <summary>Completed successfully. Poller returns the extracted result.</summary>
        Succeeded,

        /// <summary>Terminally failed. Poller throws the exception from <c>extractFailure</c>.</summary>
        Failed,

        /// <summary>Rate-limited. Wait and poll again, forcing delay to <see cref="PollingOptions.MaxDelay"/>.</summary>
        RateLimited,

        /// <summary>Transient error (e.g. upstream 5xx, in-response system error). Counts toward <see cref="PollingOptions.MaxConsecutiveTransientErrors"/>.</summary>
        TransientError,
    }

    /// <summary>Backoff strategy between poll attempts.</summary>
    public enum BackoffStrategy
    {
        /// <summary>Use <see cref="PollingOptions.InitialDelay"/> between every poll.</summary>
        Fixed,

        /// <summary>Grow the delay by <see cref="PollingOptions.BackoffMultiplier"/> each attempt with jitter, capped at <see cref="PollingOptions.MaxDelay"/>.</summary>
        ExponentialWithJitter,
    }

    /// <summary>Options controlling polling cadence, timeout, and transient-error tolerance.</summary>
    /// <param name="InitialDelay">Delay before the first retry (and every retry when <see cref="BackoffStrategy.Fixed"/>).</param>
    /// <param name="MaxDelay">Upper bound on per-attempt delay. Also used when <see cref="JobState.RateLimited"/> is reported.</param>
    /// <param name="Timeout">Maximum total polling duration. Exceeding this throws <see cref="RequestTimeoutException"/>.</param>
    /// <param name="Backoff">Backoff strategy.</param>
    /// <param name="MaxConsecutiveTransientErrors">
    /// Maximum consecutive transient failures (network errors, <see cref="JobState.TransientError"/>) before giving up.
    /// When <c>null</c>, any transient failure aborts immediately (fail-fast).
    /// </param>
    /// <param name="BackoffMultiplier">Multiplier applied per attempt under <see cref="BackoffStrategy.ExponentialWithJitter"/>.</param>
    /// <param name="JitterMilliseconds">Maximum random jitter added per attempt under <see cref="BackoffStrategy.ExponentialWithJitter"/>.</param>
    /// <param name="HeartbeatLogEveryNAttempts">Emit an informational heartbeat log every N attempts when the status is unchanged.</param>
    public sealed record PollingOptions(
        TimeSpan InitialDelay,
        TimeSpan MaxDelay,
        TimeSpan Timeout,
        BackoffStrategy Backoff = BackoffStrategy.Fixed,
        int? MaxConsecutiveTransientErrors = null,
        double BackoffMultiplier = 1.5,
        int JitterMilliseconds = 500,
        int HeartbeatLogEveryNAttempts = 15);

    /// <summary>Progress report passed to the <c>onProgress</c> callback of <see cref="AsyncJobPoller.PollAsync"/>.</summary>
    public sealed record PollProgress(int AttemptCount, TimeSpan Elapsed, JobState State, string? StatusText);

    /// <summary>
    /// Provider-agnostic helper for polling a long-running async job (submit → poll → extract).
    /// Used by providers like Replicate (predictions) and MiniMax (video generation) that follow
    /// the same loop structure but differ in backoff, status shapes, and failure classification.
    /// </summary>
    public static class AsyncJobPoller
    {
        /// <summary>
        /// Polls <paramref name="fetchStatus"/> until the classifier reports success, failure, or the timeout elapses.
        /// </summary>
        /// <typeparam name="TStatus">Provider-specific status response type.</typeparam>
        /// <typeparam name="TResult">Return type on success.</typeparam>
        /// <param name="fetchStatus">Fetches the current status. May throw transient exceptions (e.g. <see cref="HttpRequestException"/>); these count toward the consecutive-error budget.</param>
        /// <param name="classify">Maps a status response to a <see cref="JobState"/>.</param>
        /// <param name="extractSuccess">Invoked once when <paramref name="classify"/> returns <see cref="JobState.Succeeded"/>. Its return value is the final result.</param>
        /// <param name="extractFailure">Invoked once when <paramref name="classify"/> returns <see cref="JobState.Failed"/>. The returned exception is thrown.</param>
        /// <param name="options">Polling cadence + tolerance.</param>
        /// <param name="logger">Logger for status-change and heartbeat messages.</param>
        /// <param name="cancellationToken">Cancellation token. On cancel, <paramref name="onAbort"/> runs (best-effort) and <see cref="OperationCanceledException"/> is thrown.</param>
        /// <param name="onProgress">Optional per-attempt callback (e.g., for SignalR progress emission). Exceptions from the callback are logged but swallowed.</param>
        /// <param name="onAbort">Optional best-effort remote-cancel callback, invoked on timeout or caller cancellation. Exceptions are logged but swallowed.</param>
        /// <param name="operationName">Short label used in log/exception messages (e.g. "Replicate prediction", "MiniMax video").</param>
        /// <param name="delayFunc">Delay implementation. Defaults to <see cref="Task.Delay(TimeSpan, CancellationToken)"/>. Tests may inject a no-op.</param>
        /// <param name="instrumentation">Optional polling scope to record attempt count, transient errors, and terminal outcome. Caller owns disposal.</param>
        public static async Task<TResult> PollAsync<TStatus, TResult>(
            Func<CancellationToken, Task<TStatus>> fetchStatus,
            Func<TStatus, JobState> classify,
            Func<TStatus, TResult> extractSuccess,
            Func<TStatus, Exception> extractFailure,
            PollingOptions options,
            ILogger logger,
            CancellationToken cancellationToken,
            Func<PollProgress, TStatus, Task>? onProgress = null,
            Func<Task>? onAbort = null,
            string operationName = "async job",
            Func<TimeSpan, CancellationToken, Task>? delayFunc = null,
            ProviderInstrumentation.PollingScope? instrumentation = null)
        {
            ArgumentNullException.ThrowIfNull(fetchStatus);
            ArgumentNullException.ThrowIfNull(classify);
            ArgumentNullException.ThrowIfNull(extractSuccess);
            ArgumentNullException.ThrowIfNull(extractFailure);
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(logger);

            var delay = delayFunc ?? Task.Delay;
            var start = DateTime.UtcNow;
            var currentDelay = options.InitialDelay;
            var attempt = 0;
            var consecutiveTransient = 0;
            JobState? lastState = null;

            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    var elapsed = DateTime.UtcNow - start;
                    logger.LogWarning("{Operation} polling canceled after {Elapsed:F1}s and {Attempts} attempts",
                        operationName, elapsed.TotalSeconds, attempt);
                    instrumentation?.RecordCancelled();
                    await SafeAbortAsync(onAbort, logger, operationName);
                    throw new OperationCanceledException($"{operationName} polling was canceled", cancellationToken);
                }

                var totalElapsed = DateTime.UtcNow - start;
                if (totalElapsed > options.Timeout)
                {
                    logger.LogError("{Operation} timed out after {Elapsed:F1}s and {Attempts} attempts (last state: {LastState})",
                        operationName, totalElapsed.TotalSeconds, attempt, lastState?.ToString() ?? "none");
                    instrumentation?.RecordTimeout();
                    await SafeAbortAsync(onAbort, logger, operationName);
                    throw new RequestTimeoutException(
                        $"{operationName} timed out after {options.Timeout.TotalSeconds:F0}s (last state: {lastState?.ToString() ?? "unknown"})",
                        (int)options.Timeout.TotalSeconds,
                        operationName);
                }

                attempt++;

                TStatus status;
                try
                {
                    status = await fetchStatus(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    instrumentation?.RecordCancelled();
                    throw;
                }
                catch (ConduitException ex)
                {
                    // Caller-classified domain exceptions propagate immediately.
                    instrumentation?.RecordFailure(ex.GetType().Name);
                    throw;
                }
                catch (Exception ex)
                {
                    consecutiveTransient++;
                    instrumentation?.RecordAttempt(state: null);
                    instrumentation?.RecordTransientError(ex.GetType().Name);
                    logger.LogWarning(ex,
                        "{Operation} transient fetch error on attempt {Attempt} (consecutive: {Count})",
                        operationName, attempt, consecutiveTransient);

                    if (options.MaxConsecutiveTransientErrors is null)
                    {
                        instrumentation?.RecordFailure(ex.GetType().Name);
                        throw new LLMCommunicationException(
                            $"{operationName} fetch failed: {ex.Message}", ex);
                    }
                    if (consecutiveTransient >= options.MaxConsecutiveTransientErrors.Value)
                    {
                        instrumentation?.RecordFailure(ex.GetType().Name);
                        throw new LLMCommunicationException(
                            $"{operationName} failed after {consecutiveTransient} consecutive transient errors: {ex.Message}", ex);
                    }

                    await delay(options.MaxDelay, cancellationToken);
                    continue;
                }

                var state = classify(status);
                instrumentation?.RecordAttempt(state.ToString());

                if (state != JobState.TransientError)
                {
                    consecutiveTransient = 0;
                }

                if (state != lastState)
                {
                    logger.LogInformation("{Operation} state {Old} → {New} on attempt {Attempt} ({Elapsed:F1}s)",
                        operationName, lastState?.ToString() ?? "initial", state, attempt, totalElapsed.TotalSeconds);
                    lastState = state;
                }
                else if (attempt % options.HeartbeatLogEveryNAttempts == 0)
                {
                    logger.LogInformation("{Operation} still {State} after {Attempts} attempts ({Elapsed:F1}s)",
                        operationName, state, attempt, totalElapsed.TotalSeconds);
                }

                if (onProgress is not null)
                {
                    try
                    {
                        await onProgress(new PollProgress(attempt, totalElapsed, state, state.ToString()), status);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "{Operation} onProgress callback threw", operationName);
                    }
                }

                switch (state)
                {
                    case JobState.Succeeded:
                        logger.LogInformation("{Operation} succeeded after {Attempts} attempts ({Elapsed:F1}s)",
                            operationName, attempt, totalElapsed.TotalSeconds);
                        return extractSuccess(status);

                    case JobState.Failed:
                        {
                            var failure = extractFailure(status);
                            instrumentation?.RecordFailure(failure.GetType().Name);
                            throw failure;
                        }

                    case JobState.RateLimited:
                        await delay(options.MaxDelay, cancellationToken);
                        continue;

                    case JobState.TransientError:
                        consecutiveTransient++;
                        instrumentation?.RecordTransientError(nameof(JobState.TransientError));
                        if (options.MaxConsecutiveTransientErrors is null)
                        {
                            instrumentation?.RecordFailure(nameof(JobState.TransientError));
                            throw new LLMCommunicationException(
                                $"{operationName} reported a transient error and fail-fast is enabled");
                        }
                        if (consecutiveTransient >= options.MaxConsecutiveTransientErrors.Value)
                        {
                            instrumentation?.RecordFailure(nameof(JobState.TransientError));
                            throw new LLMCommunicationException(
                                $"{operationName} failed after {consecutiveTransient} consecutive transient errors");
                        }
                        await delay(options.MaxDelay, cancellationToken);
                        continue;

                    case JobState.InProgress:
                    default:
                        await delay(currentDelay, cancellationToken);
                        currentDelay = NextDelay(currentDelay, options);
                        continue;
                }
            }
        }

        private static TimeSpan NextDelay(TimeSpan current, PollingOptions options)
        {
            if (options.Backoff == BackoffStrategy.Fixed)
            {
                return options.InitialDelay;
            }

            var jitter = options.JitterMilliseconds > 0
                ? TimeSpan.FromMilliseconds(Random.Shared.Next(options.JitterMilliseconds))
                : TimeSpan.Zero;
            var next = TimeSpan.FromMilliseconds(current.TotalMilliseconds * options.BackoffMultiplier) + jitter;
            return next > options.MaxDelay ? options.MaxDelay : next;
        }

        private static async Task SafeAbortAsync(Func<Task>? onAbort, ILogger logger, string operationName)
        {
            if (onAbort is null)
            {
                return;
            }
            try
            {
                await onAbort();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "{Operation} onAbort callback threw", operationName);
            }
        }
    }
}
