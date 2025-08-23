using MassTransit;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Controllers
{
    /// <summary>
    /// Base class for controllers that publish domain events using MassTransit.
    /// Provides fire-and-forget event publishing patterns with consistent error handling and logging.
    /// </summary>
    public abstract class EventPublishingControllerBase : ControllerBase
    {
        private readonly IPublishEndpoint? _publishEndpoint;
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventPublishingControllerBase"/> class.
        /// </summary>
        /// <param name="publishEndpoint">The optional MassTransit publish endpoint for event publishing.</param>
        /// <param name="logger">The logger instance for the derived controller.</param>
        protected EventPublishingControllerBase(
            IPublishEndpoint? publishEndpoint,
            ILogger logger)
        {
            _publishEndpoint = publishEndpoint;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets a value indicating whether event publishing is configured.
        /// </summary>
        protected bool IsEventPublishingEnabled => _publishEndpoint != null;

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
                    typeof(TEvent).Name, operationName);
                return;
            }

            if (_publishEndpoint == null)
            {
                _logger.LogWarning(
                    "Event publishing not configured - skipping {EventType} for {Operation}",
                    typeof(TEvent).Name, operationName);
                return;
            }

            // Fire and forget - don't await
            _ = Task.Run(async () =>
            {
                try
                {
                    await _publishEndpoint.Publish(domainEvent);
                    _logger.LogDebug(
                        "Published {EventType} event for {Operation}",
                        typeof(TEvent).Name, operationName);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Failed to publish {EventType} event for {Operation} - operation completed but event not sent",
                        typeof(TEvent).Name, operationName);
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
                    typeof(TEvent).Name, operationName, contextData);
                return;
            }

            if (_publishEndpoint == null)
            {
                _logger.LogDebug(
                    "Event publishing not configured - skipping {EventType} for {Operation} with context {ContextData}",
                    typeof(TEvent).Name, operationName, contextData);
                return;
            }

            // Fire and forget - don't await
            _ = Task.Run(async () =>
            {
                try
                {
                    await _publishEndpoint.Publish(domainEvent);
                    _logger.LogDebug(
                        "Published {EventType} event for {Operation} with context {ContextData}",
                        typeof(TEvent).Name, operationName, contextData);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Failed to publish {EventType} event for {Operation} with context {ContextData} - operation completed but event not sent",
                        typeof(TEvent).Name, operationName, contextData);
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
            if (_publishEndpoint != null)
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
    }
}