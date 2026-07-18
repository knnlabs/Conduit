using System.Diagnostics;

using ConduitLLM.Configuration.Messaging;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Extensions;
using ConduitLLM.Core.Metrics;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Controllers
{
    /// <summary>
    /// Base class for controllers that publish domain events through the Conduit-owned
    /// <see cref="IEventBus"/> abstraction (epic #909).
    /// Provides fire-and-forget event publishing patterns, shared utility methods,
    /// and consistent error handling and logging.
    /// </summary>
    public abstract class EventPublishingControllerBase : ControllerBase
    {
        private readonly IEventBus? _eventBus;
        private readonly ILogger _logger;

        /// <summary>
        /// Logger instance for derived controllers.
        /// </summary>
        protected ILogger Logger => _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventPublishingControllerBase"/> class.
        /// </summary>
        /// <param name="eventBus">The optional event bus for event publishing (null if messaging not configured).</param>
        /// <param name="logger">The logger instance for the derived controller.</param>
        protected EventPublishingControllerBase(
            IEventBus? eventBus,
            ILogger logger)
        {
            _eventBus = eventBus;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets a value indicating whether event publishing is configured.
        /// </summary>
        protected bool IsEventPublishingEnabled => _eventBus != null;

        /// <summary>
        /// Publishes a domain event using fire-and-forget pattern with standardized error handling.
        /// Does not wait for the publish operation to complete.
        /// </summary>
        /// <typeparam name="TEvent">The type of event to publish.</typeparam>
        /// <param name="domainEvent">The event to publish.</param>
        /// <param name="operationName">A descriptive name for the operation that triggered the event.</param>
        /// <remarks>
        /// This method provides graceful degradation when event publishing is not configured.
        /// Failures in event publishing do not cause the calling operation to fail.
        /// The event is published in the background without blocking the HTTP response.
        /// </remarks>
        protected void PublishEventFireAndForget<TEvent>(
            TEvent domainEvent,
            string operationName) where TEvent : class
        {
            if (domainEvent == null)
            {
                _logger.LogWarning(
                    "Attempted to publish null event of type {EventType} for {Operation}",
                    nameof(TEvent), operationName);
                return;
            }

            if (_eventBus == null)
            {
                _logger.LogWarning(
                    "Event publishing not configured - skipping {EventType} for {Operation}",
                    nameof(TEvent), operationName);
                EventPublishingMetrics.RecordSkipped(typeof(TEvent).Name);
                return;
            }

            // Fire and forget - don't await
            _ = Task.Run(async () =>
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    // Failures are swallowed below (fire-and-forget). Acceptable here:
                    // these call sites are non-financial (media requests, admin config).
                    // On the Wolverine backend an accepted publish is durable (#927);
                    // financial paths use TryPublishEventAsync + direct-write fallback.
                    await _eventBus.PublishAsync(domainEvent);
                    sw.Stop();
                    _logger.LogDebug(
                        "Published {EventType} event for {Operation}",
                        nameof(TEvent), operationName);
                    EventPublishingMetrics.RecordSuccess(typeof(TEvent).Name, sw.Elapsed.TotalSeconds);
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    _logger.LogWarning(ex,
                        "Failed to publish {EventType} event for {Operation} - operation completed but event not sent",
                        nameof(TEvent), operationName);
                    EventPublishingMetrics.RecordFailure(typeof(TEvent).Name);
                    // Don't rethrow - event publishing should not fail business operations
                }
            });
        }

        /// <summary>
        /// Publishes a domain event using fire-and-forget pattern with additional context data.
        /// Does not wait for the publish operation to complete.
        /// </summary>
        /// <typeparam name="TEvent">The type of event to publish.</typeparam>
        /// <param name="domainEvent">The event to publish.</param>
        /// <param name="operationName">A descriptive name for the operation that triggered the event.</param>
        /// <param name="contextData">Additional context data to include in log messages.</param>
        protected void PublishEventFireAndForget<TEvent>(
            TEvent domainEvent,
            string operationName,
            object contextData) where TEvent : class
        {
            if (domainEvent == null)
            {
                _logger.LogWarning(
                    "Attempted to publish null event of type {EventType} for {Operation} with context {ContextData}",
                    nameof(TEvent), operationName, contextData);
                return;
            }

            if (_eventBus == null)
            {
                _logger.LogDebug(
                    "Event publishing not configured - skipping {EventType} for {Operation} with context {ContextData}",
                    nameof(TEvent), operationName, contextData);
                EventPublishingMetrics.RecordSkipped(typeof(TEvent).Name);
                return;
            }

            // Fire and forget - don't await
            _ = Task.Run(async () =>
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    // Failures are swallowed below (fire-and-forget). Acceptable here:
                    // these call sites are non-financial (media requests, admin config).
                    // On the Wolverine backend an accepted publish is durable (#927);
                    // financial paths use TryPublishEventAsync + direct-write fallback.
                    await _eventBus.PublishAsync(domainEvent);
                    sw.Stop();
                    _logger.LogDebug(
                        "Published {EventType} event for {Operation} with context {ContextData}",
                        nameof(TEvent), operationName, contextData);
                    EventPublishingMetrics.RecordSuccess(typeof(TEvent).Name, sw.Elapsed.TotalSeconds);
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    _logger.LogWarning(ex,
                        "Failed to publish {EventType} event for {Operation} with context {ContextData} - operation completed but event not sent",
                        nameof(TEvent), operationName, contextData);
                    EventPublishingMetrics.RecordFailure(typeof(TEvent).Name);
                    // Don't rethrow - event publishing should not fail business operations
                }
            });
        }

        /// <summary>
        /// Logs the event publishing configuration status on controller initialization.
        /// </summary>
        /// <param name="controllerName">The name of the controller for logging context.</param>
        protected void LogEventPublishingConfiguration(string controllerName)
        {
            if (_eventBus != null)
            {
                _logger.LogInformation(
                    "{ControllerName}: Event bus configured - using event-driven architecture",
                    controllerName);
            }
            else
            {
                _logger.LogWarning(
                    "{ControllerName}: Event bus NOT configured - events will not be published",
                    controllerName);
            }
        }

        // ─── Shared Utility Methods ─────────────────────────────────────

        /// <summary>
        /// Returns true if the current HTTP request is a mutation (POST, PUT, PATCH, DELETE).
        /// </summary>
        protected bool IsMutationRequest()
        {
            var method = HttpContext?.Request?.Method;
            return method is "POST" or "PUT" or "PATCH" or "DELETE";
        }

        /// <summary>
        /// Logs an exception with the request body for mutation requests (fire-and-forget).
        /// Falls back to logging without body if capture fails.
        /// Used by both Admin and Gateway controller bases for consistent error diagnostics.
        /// </summary>
        protected async Task LogExceptionWithBodyAsync(
            ExceptionToResponseMapper.ExceptionMappingResult mapping,
            Exception ex,
            string logMessage)
        {
            string? requestBody = null;
            try
            {
                requestBody = await RequestBodyCapture.CaptureAsync(HttpContext);
            }
            catch
            {
                // Body capture should never prevent error logging
            }

            if (requestBody != null)
            {
                if (mapping.IncludeExceptionMessageInLog)
                {
                    _logger.Log(mapping.LogLevel, ex,
                        "{LogPrefix} in {Operation}: {Message}. RequestBody: {RequestBody}",
                        mapping.LogPrefix, logMessage, ex.Message, requestBody);
                }
                else if (mapping.LogLevel == LogLevel.Error)
                {
                    _logger.LogError(ex,
                        "{LogPrefix} in {Operation}. RequestBody: {RequestBody}",
                        mapping.LogPrefix, logMessage, requestBody);
                }
                else
                {
                    _logger.LogWarning(
                        "{LogPrefix} in {Operation}. RequestBody: {RequestBody}",
                        mapping.LogPrefix, logMessage, requestBody);
                }
            }
            else
            {
                if (mapping.IncludeExceptionMessageInLog)
                {
                    _logger.Log(mapping.LogLevel, ex,
                        "{LogPrefix} in {Operation}: {Message}",
                        mapping.LogPrefix, logMessage, ex.Message);
                }
                else if (mapping.LogLevel == LogLevel.Error)
                {
                    _logger.LogError(ex,
                        "{LogPrefix} in {Operation}",
                        mapping.LogPrefix, logMessage);
                }
                else
                {
                    _logger.LogWarning(
                        "{LogPrefix} in {Operation}",
                        mapping.LogPrefix, logMessage);
                }
            }
        }
    }
}