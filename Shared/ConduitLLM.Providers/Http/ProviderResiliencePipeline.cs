using System.Net;

using ConduitLLM.Providers.Configuration;

using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;

using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace ConduitLLM.Providers.Http;

/// <summary>
/// Composes the resilience pipeline applied to every provider named client:
/// total timeout → retry → circuit breaker → per-attempt timeout (outermost → innermost).
/// </summary>
/// <remarks>
/// <para>
/// Ordering rationale: the per-attempt timeout bounds a single hung connection (and, for
/// streaming requests sent with <c>ResponseHeadersRead</c>, time-to-first-token); the retry
/// strategy sits above it so a timed-out attempt can be retried; the total timeout caps the sum
/// of attempts and backoff delays. The circuit breaker sits between them so breaker state is fed
/// by attempt outcomes and open-circuit failures are NOT retried against the same host (the
/// retry ShouldHandle excludes <see cref="BrokenCircuitException"/>).
/// </para>
/// <para>
/// The pipeline never cancels an in-progress response stream: all strategies act on the
/// <c>SendAsync</c> call, which completes at response headers for streaming requests. Stream
/// lifetime is governed by the streaming idle watchdog, not by this pipeline.
/// </para>
/// <para>
/// Timeouts are resolved per request from the operation class
/// (<see cref="ConduitHttpOptions.OperationClass"/> request option, falling back to the named
/// client's default class). The retry count comes from the client's default class budget — it is
/// fixed per pipeline instance.
/// </para>
/// </remarks>
public static class ProviderResiliencePipeline
{
    public static void Configure(
        ResiliencePipelineBuilder<HttpResponseMessage> builder,
        string defaultOperationClass,
        ProviderResilienceOptions options,
        ProviderErrorTrackingRetryHook? errorTrackingHook,
        ILogger? logger)
    {
        var defaultBudget = options.BudgetFor(defaultOperationClass);
        var retryAfterCap = TimeSpan.FromSeconds(options.Retry.RetryAfterCapSeconds);

        // --- 1. Total timeout (outermost): caps all attempts + backoff delays ---
        builder.AddTimeout(new TimeoutStrategyOptions
        {
            Name = "conduit-total-timeout",
            TimeoutGenerator = args => new ValueTask<TimeSpan>(TimeSpan.FromSeconds(
                options.BudgetFor(ResolveOperationClass(args.Context, defaultOperationClass)).TotalTimeoutSeconds)),
        });

        // --- 2. Retry: transient errors + 429, honoring Retry-After up to a cap ---
        // (Polly rejects MaxRetryAttempts = 0, so zero-retry budgets — auth, video — simply
        // omit the strategy.)
        if (defaultBudget.MaxRetryAttempts > 0)
        {
            builder.AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                Name = "conduit-retry",
                MaxRetryAttempts = defaultBudget.MaxRetryAttempts,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = TimeSpan.FromSeconds(options.Retry.BaseDelaySeconds),
                MaxDelay = TimeSpan.FromSeconds(options.Retry.MaxDelaySeconds),
                ShouldHandle = args =>
                {
                    if (!IsTransient(args.Outcome))
                    {
                        return new ValueTask<bool>(false);
                    }

                    // A Retry-After beyond the cap means the provider wants us gone for a while —
                    // fail fast so the caller (and, later, the failover layer) can react instead of
                    // holding the user's request open.
                    if (args.Outcome.Result is { } response &&
                        TryGetRetryAfter(response, out var retryAfter) &&
                        retryAfter > retryAfterCap)
                    {
                        return new ValueTask<bool>(false);
                    }

                    return new ValueTask<bool>(true);
                },
                DelayGenerator = args =>
                {
                    if (args.Outcome.Result is { } response &&
                        TryGetRetryAfter(response, out var retryAfter) &&
                        retryAfter <= retryAfterCap)
                    {
                        return new ValueTask<TimeSpan?>(retryAfter);
                    }

                    // null → use the configured exponential backoff
                    return new ValueTask<TimeSpan?>((TimeSpan?)null);
                },
                OnRetry = async args =>
                {
                    var retryAttempt = args.AttemptNumber + 1; // 1-based, parity with legacy policy
                    logger?.LogWarning(
                        "Retry {RetryAttempt}/{MaxRetries} after {DelayMs}ms due to {StatusCode}. Error: {Error}",
                        retryAttempt,
                        defaultBudget.MaxRetryAttempts,
                        args.RetryDelay.TotalMilliseconds,
                        args.Outcome.Result?.StatusCode,
                        args.Outcome.Exception?.Message);

                    if (errorTrackingHook != null)
                    {
                        await errorTrackingHook.OnRetryAsync(
                            args.Outcome.Result, retryAttempt, defaultBudget.MaxRetryAttempts);
                    }
                },
            });
        }

        // --- 3. Circuit breaker: outage signals only (NOT 429 — rate limiting is not an
        // outage, and breaking on it would amplify quota incidents across tenants) ---
        if (options.CircuitBreaker.Enabled)
        {
            builder.AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
            {
                Name = "conduit-circuit-breaker",
                FailureRatio = options.CircuitBreaker.FailureRatio,
                MinimumThroughput = options.CircuitBreaker.MinimumThroughput,
                SamplingDuration = TimeSpan.FromSeconds(options.CircuitBreaker.SamplingDurationSeconds),
                BreakDuration = TimeSpan.FromSeconds(options.CircuitBreaker.BreakDurationSeconds),
                ShouldHandle = args => new ValueTask<bool>(IsOutageSignal(args.Outcome)),
                OnOpened = args =>
                {
                    logger?.LogWarning(
                        "Provider circuit OPENED for {BreakDuration}s after failures (last: {StatusCode} {Error})",
                        args.BreakDuration.TotalSeconds,
                        args.Outcome.Result?.StatusCode,
                        args.Outcome.Exception?.Message);
                    return default;
                },
                OnClosed = _ =>
                {
                    logger?.LogInformation("Provider circuit closed — traffic restored");
                    return default;
                },
            });
        }

        // --- 4. Per-attempt timeout (innermost): bounds a single hung connection ---
        builder.AddTimeout(new TimeoutStrategyOptions
        {
            Name = "conduit-attempt-timeout",
            TimeoutGenerator = args => new ValueTask<TimeSpan>(TimeSpan.FromSeconds(
                options.BudgetFor(ResolveOperationClass(args.Context, defaultOperationClass)).AttemptTimeoutSeconds)),
        });
    }

    private static string ResolveOperationClass(ResilienceContext context, string defaultOperationClass)
    {
        var request = context.GetRequestMessage();
        if (request != null &&
            request.Options.TryGetValue(ConduitHttpOptions.OperationClass, out var operationClass) &&
            !string.IsNullOrEmpty(operationClass))
        {
            return operationClass;
        }

        return defaultOperationClass;
    }

    private static bool IsTransient(Outcome<HttpResponseMessage> outcome)
    {
        if (outcome.Exception is HttpRequestException or TimeoutRejectedException)
        {
            return true;
        }

        if (outcome.Result is { } response)
        {
            return (int)response.StatusCode >= 500
                || response.StatusCode == HttpStatusCode.RequestTimeout
                || response.StatusCode == HttpStatusCode.TooManyRequests;
        }

        return false;
    }

    private static bool IsOutageSignal(Outcome<HttpResponseMessage> outcome)
    {
        if (outcome.Exception is HttpRequestException or TimeoutRejectedException)
        {
            return true;
        }

        if (outcome.Result is { } response)
        {
            // 5xx and 408 only — never 429 (rate limit) or auth-class 4xx (key problems belong
            // to error tracking / failover, not the breaker).
            return (int)response.StatusCode >= 500
                || response.StatusCode == HttpStatusCode.RequestTimeout;
        }

        return false;
    }

    private static bool TryGetRetryAfter(HttpResponseMessage response, out TimeSpan retryAfter)
    {
        var header = response.Headers.RetryAfter;
        if (header?.Delta is { } delta && delta > TimeSpan.Zero)
        {
            retryAfter = delta;
            return true;
        }

        if (header?.Date is { } date)
        {
            var until = date - DateTimeOffset.UtcNow;
            if (until > TimeSpan.Zero)
            {
                retryAfter = until;
                return true;
            }
        }

        retryAfter = default;
        return false;
    }
}
