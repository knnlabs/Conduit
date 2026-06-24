using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MassTransit;
using ConduitLLM.Admin.Filters;
using ConduitLLM.Configuration.Events;
using ConduitLLM.Core.Extensions;

namespace ConduitLLM.Admin.Controllers
{
    /// <summary>
    /// Administrative controller for managing batch spending operations.
    ///
    /// This controller provides endpoints for administrators to:
    /// - Trigger immediate flushing of pending batch spend updates
    /// - Monitor batch spending service status and statistics
    /// - Perform operational maintenance on the spending system
    ///
    /// All endpoints require master key authentication for security.
    /// Operations are performed via event-driven architecture for proper decoupling.
    /// </summary>
    [ApiController]
    [Route("api/batch-spending")]
    [Authorize(Policy = "MasterKeyPolicy")]
    [ServiceFilter(typeof(OperationLoggingFilter))]
    public class BatchSpendingController : AdminControllerBase
    {
        private readonly IPublishEndpoint _publishEndpoint;

        /// <summary>
        /// Initializes a new instance of the BatchSpendingController.
        /// </summary>
        /// <param name="publishEndpoint">MassTransit publish endpoint for sending events</param>
        /// <param name="logger">Logger instance for operational tracking</param>
        public BatchSpendingController(
            IPublishEndpoint publishEndpoint,
            ILogger<BatchSpendingController> logger)
            : base(publishEndpoint, logger)
        {
            _publishEndpoint = publishEndpoint ?? throw new ArgumentNullException(nameof(publishEndpoint));
        }

        /// <summary>
        /// Triggers immediate flushing of all pending batch spend updates.
        ///
        /// This endpoint publishes a BatchSpendFlushRequestedEvent which is consumed by the Gateway API
        /// to immediately process all queued spending charges instead of waiting for the scheduled
        /// batch interval. This is essential for:
        ///
        /// - Integration testing (deterministic billing verification)
        /// - Administrative operations (manual reconciliation)
        /// - Maintenance scenarios (pre-deployment charge processing)
        /// - Emergency operations (immediate financial updates)
        ///
        /// The operation is asynchronous and event-driven for proper architectural decoupling.
        /// </summary>
        /// <param name="reason">Optional reason for the flush operation (for audit trail)</param>
        /// <param name="priority">Priority level: Normal (default) or High for urgent operations</param>
        /// <param name="timeoutSeconds">Optional timeout in seconds (default: service timeout)</param>
        /// <param name="includeStatistics">Whether to include detailed statistics in logs (default: true)</param>
        /// <returns>Flush operation details including request ID for tracking</returns>
        [HttpPost("flush")]
        [ProducesResponseType(typeof(object), 202)]
        [ProducesResponseType(400)]
        [ProducesResponseType(500)]
        public async Task<IActionResult> FlushPendingUpdates(
            [FromQuery] string? reason = null,
            [FromQuery] FlushPriority priority = FlushPriority.Normal,
            [FromQuery] int? timeoutSeconds = null,
            [FromQuery] bool includeStatistics = true)
        {
            // Generate unique request ID for tracking
            var requestId = Guid.NewGuid().ToString();

            Logger.LogInformation(
                "Admin requesting batch spend flush - RequestId: {RequestId}, Reason: {Reason}, Priority: {Priority}",
                requestId, reason ?? "Administrative operation", priority);

            // Validate timeout parameter
            if (timeoutSeconds.HasValue && (timeoutSeconds.Value < 1 || timeoutSeconds.Value > 300))
            {
                throw new ArgumentException("Timeout must be between 1 and 300 seconds");
            }

            // Create and publish flush request event
            var flushEvent = new BatchSpendFlushRequestedEvent
            {
                RequestId = requestId,
                RequestedBy = "Admin",
                RequestedAt = DateTime.UtcNow,
                Reason = reason ?? "Administrative flush operation",
                Source = "Admin API",
                Priority = priority,
                TimeoutSeconds = timeoutSeconds,
                IncludeStatistics = includeStatistics
            };

            // Publish event to Gateway API for processing
            await _publishEndpoint.Publish(flushEvent);

            LogAdminAudit("Flushed", "BatchSpending",
                detail: $"RequestId: {requestId}, Priority: {priority}, Reason: {LoggingSanitizer.S(reason ?? "Administrative flush operation")}");

            // Return accepted response with tracking information
            return Accepted(new
            {
                success = true,
                message = "Batch spend flush request submitted successfully",
                requestId = requestId,
                requestedAt = flushEvent.RequestedAt,
                priority = priority.ToString(),
                estimatedProcessingTime = timeoutSeconds.HasValue
                    ? $"Up to {timeoutSeconds} seconds"
                    : "Based on service configuration",
                note = "This is an asynchronous operation. Monitor logs for completion status."
            });
        }

        /// <summary>
        /// Gets information about the batch spending system status.
        ///
        /// This endpoint provides operational visibility into:
        /// - Event publishing capability
        /// - System readiness for flush operations
        /// - Configuration details
        ///
        /// Note: This endpoint checks the Admin API's ability to publish events,
        /// not the Gateway API's batch spending service status (which is internal).
        /// </summary>
        /// <returns>System status and configuration information</returns>
        [HttpGet("status")]
        [ProducesResponseType(typeof(object), 200)]
        public async Task<IActionResult> GetStatus()
        {
            var isEventBusAvailable = IsEventPublishingEnabled;

            var status = new
            {
                success = true,
                adminApiStatus = "healthy",
                eventBusAvailable = isEventBusAvailable,
                canPublishFlushRequests = isEventBusAvailable,
                supportedOperations = new[]
                {
                    "flush - Trigger immediate batch spend processing",
                    "status - Get system status information"
                },
                architecture = new
                {
                    pattern = "Event-driven with MassTransit",
                    adminRole = "Publishes BatchSpendFlushRequestedEvent",
                    coreRole = "Consumes events and performs actual flush operations",
                    decoupling = "Admin and Gateway APIs communicate via events only"
                },
                timestamp = DateTime.UtcNow
            };

            await Task.CompletedTask;
            return Ok(status);
        }

        /// <summary>
        /// Gets operational information about the batch spending flush capability.
        ///
        /// This endpoint provides documentation and operational guidance for administrators
        /// without exposing internal Gateway API details.
        /// </summary>
        /// <returns>Operational information and usage guidance</returns>
        [HttpGet("info")]
        [ProducesResponseType(typeof(object), 200)]
        public IActionResult GetInformation()
        {
            return Ok(new
            {
                service = "Batch Spending Administration",
                description = "Administrative interface for managing batch spend update operations",

                endpoints = new
                {
                    flush = new
                    {
                        method = "POST",
                        path = "/api/batch-spending/flush",
                        description = "Triggers immediate processing of pending spend updates",
                        parameters = new
                        {
                            reason = "Optional reason for audit trail",
                            priority = "Normal (default) or High for urgent operations",
                            timeoutSeconds = "Optional timeout (1-300 seconds)",
                            includeStatistics = "Include detailed stats in logs (default: true)"
                        },
                        useCases = new[]
                        {
                            "Integration testing - Deterministic billing verification",
                            "Administrative reconciliation - Manual financial updates",
                            "Maintenance operations - Pre-deployment charge processing",
                            "Emergency scenarios - Immediate spending updates"
                        }
                    },
                    status = new
                    {
                        method = "GET",
                        path = "/api/batch-spending/status",
                        description = "Gets system status and event publishing capability"
                    }
                },

                architecture = new
                {
                    pattern = "Event-driven architecture with MassTransit",
                    security = "Master key authentication required",
                    reliability = "Asynchronous processing with error handling and retry policies",
                    monitoring = "Full audit trail via structured logging"
                },

                operationalNotes = new[]
                {
                    "All operations are asynchronous and event-driven",
                    "Flush requests are processed by the Gateway API batch spending service",
                    "Monitor application logs for detailed operation results",
                    "High priority requests are processed with elevated logging",
                    "Failed operations include detailed error information in logs"
                },

                timestamp = DateTime.UtcNow
            });
        }
    }
}
