using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Extensions.Http;

namespace ConduitLLM.Http.EventHandlers
{
    /// <summary>
    /// Base class for resilient event handlers with built-in error handling,
    /// circuit breakers, and fallback mechanisms.
    /// </summary>
    /// <typeparam name="TEvent">The type of event this handler consumes</typeparam>
    public abstract class ResilientEventHandlerBase<TEvent> : IConsumer<TEvent>
        where TEvent : class
    {
        protected readonly ILogger Logger;
        private readonly IAsyncPolicy _resiliencePolicy;
        private readonly string _handlerName;
        
        /// <summary>
        /// Gets the circuit breaker state for monitoring
        /// </summary>
        protected CircuitState CircuitState => _circuitBreaker?.CircuitState ?? CircuitState.Closed;
        
        private readonly AsyncCircuitBreakerPolicy? _circuitBreaker;

        protected ResilientEventHandlerBase(ILogger logger)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _handlerName = GetType().Name;
            
            // Build resilience policy with circuit breaker
            var circuitBreakerPolicy = Policy
                .Handle<Exception>(ex => !IsTransientException(ex))
                .AdvancedCircuitBreakerAsync(
                    failureThreshold: 0.5, // 50% failure rate
                    samplingDuration: TimeSpan.FromMinutes(1),
                    minimumThroughput: GetCircuitBreakerThreshold(),
                    durationOfBreak: GetCircuitBreakerDuration(),
                    onBreak: (result, duration) => OnCircuitBreakerOpen(result, duration),
                    onReset: OnCircuitBreakerReset,
                    onHalfOpen: OnCircuitBreakerHalfOpen);

            // Store circuit breaker reference for state monitoring
            _circuitBreaker = circuitBreakerPolicy;

            // Combine with retry policy for transient errors
            var retryPolicy = Policy
                .Handle<Exception>(IsTransientException)
                .WaitAndRetryAsync(
                    retryCount: GetRetryCount(),
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        Logger.LogWarning(
                            "Retry {RetryCount} after {Delay}ms for {Handler}",
                            retryCount, timespan.TotalMilliseconds, _handlerName);
                    });

            // Wrap with timeout policy
            var timeoutPolicy = Policy.TimeoutAsync(GetTimeout());

            // Combine all policies
            _resiliencePolicy = Policy.WrapAsync(circuitBreakerPolicy, retryPolicy, timeoutPolicy);
        }

        /// <summary>
        /// Main consume method with resilience wrapper
        /// </summary>
        public async Task Consume(ConsumeContext<TEvent> context)
        {
            var stopwatch = Stopwatch.StartNew();
            var eventType = typeof(TEvent).Name;
            
            try
            {
                Logger.LogDebug(
                    "Processing {EventType} in {Handler}",
                    eventType, _handlerName);

                // Execute with resilience policy
                await _resiliencePolicy.ExecuteAsync(async () =>
                {
                    await HandleEventAsync(context.Message, context.CancellationToken);
                });

                stopwatch.Stop();
                Logger.LogDebug(
                    "Successfully processed {EventType} in {Handler} after {ElapsedMs}ms",
                    eventType, _handlerName, stopwatch.ElapsedMilliseconds);
            }
            catch (BrokenCircuitException ex)
            {
                stopwatch.Stop();
                Logger.LogWarning(ex,
                    "Circuit breaker is open for {Handler}. Falling back for {EventType}",
                    _handlerName, eventType);

                // Execute fallback when circuit is open
                try
                {
                    await HandleEventFallbackAsync(context.Message, context.CancellationToken);
                }
                catch (Exception fallbackEx)
                {
                    Logger.LogError(fallbackEx,
                        "Fallback failed for {EventType} in {Handler}",
                        eventType, _handlerName);
                    throw;
                }
            }
            catch (TimeoutException ex)
            {
                stopwatch.Stop();
                Logger.LogError(ex,
                    "Timeout processing {EventType} in {Handler} after {ElapsedMs}ms",
                    eventType, _handlerName, stopwatch.ElapsedMilliseconds);
                
                // Let MassTransit retry infrastructure handle it
                throw;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Logger.LogError(ex,
                    "Failed to process {EventType} in {Handler} after {ElapsedMs}ms",
                    eventType, _handlerName, stopwatch.ElapsedMilliseconds);
                
                // For non-transient errors, try fallback before failing
                if (!IsTransientException(ex))
                {
                    try
                    {
                        await HandleEventFallbackAsync(context.Message, context.CancellationToken);
                        return; // Fallback succeeded
                    }
                    catch (Exception fallbackEx)
                    {
                        Logger.LogError(fallbackEx,
                            "Fallback also failed for {EventType} in {Handler}",
                            eventType, _handlerName);
                    }
                }
                
                throw;
            }
        }

        /// <summary>
        /// Implement the main event handling logic
        /// </summary>
        protected abstract Task HandleEventAsync(TEvent message, CancellationToken cancellationToken);

        /// <summary>
        /// Implement fallback logic when main handler fails or circuit is open
        /// </summary>
        protected virtual Task HandleEventFallbackAsync(TEvent message, CancellationToken cancellationToken)
        {
            // Default: log and skip
            Logger.LogWarning(
                "No fallback implemented for {Handler}. Event will be skipped.",
                _handlerName);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Determine if an exception is transient and should be retried
        /// </summary>
        protected virtual bool IsTransientException(Exception ex)
        {
            return ex is TimeoutException ||
                   ex is TaskCanceledException ||
                   (ex.InnerException != null && IsTransientException(ex.InnerException));
        }

        /// <summary>
        /// Get the number of retries for transient errors
        /// </summary>
        protected virtual int GetRetryCount() => 3;

        /// <summary>
        /// Get the timeout for operations
        /// </summary>
        protected virtual TimeSpan GetTimeout() => TimeSpan.FromSeconds(30);

        /// <summary>
        /// Get the circuit breaker failure threshold
        /// </summary>
        protected virtual int GetCircuitBreakerThreshold() => 5;

        /// <summary>
        /// Get the circuit breaker open duration
        /// </summary>
        protected virtual TimeSpan GetCircuitBreakerDuration() => TimeSpan.FromMinutes(1);

        /// <summary>
        /// Called when circuit breaker opens
        /// </summary>
        protected virtual void OnCircuitBreakerOpen(DelegateResult<object> result, TimeSpan duration)
        {
            Logger.LogWarning(
                "Circuit breaker opened for {Handler} for {Duration}s due to: {Reason}",
                _handlerName, duration.TotalSeconds, result.Exception?.Message ?? "Unknown");
        }

        /// <summary>
        /// Called when circuit breaker resets
        /// </summary>
        protected virtual void OnCircuitBreakerReset()
        {
            Logger.LogInformation(
                "Circuit breaker reset for {Handler}",
                _handlerName);
        }

        /// <summary>
        /// Called when circuit breaker is half-open
        /// </summary>
        protected virtual void OnCircuitBreakerHalfOpen()
        {
            Logger.LogInformation(
                "Circuit breaker half-open for {Handler}, testing with next request",
                _handlerName);
        }
    }
}