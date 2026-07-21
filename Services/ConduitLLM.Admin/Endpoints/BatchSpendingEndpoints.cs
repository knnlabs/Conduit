using ConduitLLM.Admin.Auditing;
using ConduitLLM.Configuration.Events;
using ConduitLLM.Configuration.Messaging;
using ConduitLLM.Core.Extensions;

using Microsoft.AspNetCore.Mvc;

namespace ConduitLLM.Admin.Endpoints;

public static class BatchSpendingEndpoints
{
    public static IEndpointRouteBuilder MapBatchSpendingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/batch-spending")
            .RequireAuthorization("MasterKeyPolicy")
            .AddEndpointFilter<OperationLoggingEndpointFilter>()
            .WithTags("BatchSpending");
        group.MapPost("/flush", Flush).WithName("BatchSpending_Flush")
            .Produces<object>(StatusCodes.Status202Accepted)
            .Produces<ConduitLLM.Configuration.DTOs.ErrorResponseDto>(StatusCodes.Status400BadRequest);
        group.MapGet("/status", GetStatus).WithName("BatchSpending_GetStatus").Produces<object>();
        group.MapGet("/info", GetInformation).WithName("BatchSpending_GetInformation").Produces<object>();
        return app;
    }

    private static async Task<IResult> Flush(
        [FromServices] IEventBus eventBus,
        HttpContext httpContext,
        ILoggerFactory loggerFactory,
        [FromQuery] string? reason = null,
        [FromQuery] FlushPriority priority = FlushPriority.Normal,
        [FromQuery] int? timeoutSeconds = null,
        [FromQuery] bool includeStatistics = true)
    {
        var requestId = Guid.NewGuid().ToString();
        var logger = Logger(loggerFactory);
        logger.LogInformation(
            "Admin requesting batch spend flush - RequestId: {RequestId}, Reason: {Reason}, Priority: {Priority}",
            requestId, reason ?? "Administrative operation", priority);
        if (timeoutSeconds is < 1 or > 300)
        {
            throw new ArgumentException("Timeout must be between 1 and 300 seconds");
        }

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
        await eventBus.PublishAsync(flushEvent);
        AdminAudit.Log(httpContext, logger, "Flushed", "BatchSpending",
            detail: $"RequestId: {requestId}, Priority: {priority}, Reason: {LoggingSanitizer.S(flushEvent.Reason)}");
        return Results.Accepted(value: new
        {
            success = true,
            message = "Batch spend flush request submitted successfully",
            requestId,
            requestedAt = flushEvent.RequestedAt,
            priority = priority.ToString(),
            estimatedProcessingTime = timeoutSeconds.HasValue
                ? $"Up to {timeoutSeconds} seconds"
                : "Based on service configuration",
            note = "This is an asynchronous operation. Monitor logs for completion status."
        });
    }

    private static IResult GetStatus([FromServices] IEventBus eventBus) => Results.Ok(new
    {
        success = true,
        adminApiStatus = "healthy",
        eventBusAvailable = eventBus is not null,
        canPublishFlushRequests = eventBus is not null,
        supportedOperations = new[]
        {
            "flush - Trigger immediate batch spend processing",
            "status - Get system status information"
        },
        architecture = new
        {
            pattern = "Event-driven with Wolverine",
            adminRole = "Publishes BatchSpendFlushRequestedEvent",
            coreRole = "Consumes events and performs actual flush operations",
            decoupling = "Admin and Gateway APIs communicate via events only"
        },
        timestamp = DateTime.UtcNow
    });

    private static IResult GetInformation() => Results.Ok(new
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
            pattern = "Event-driven architecture with Wolverine",
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

    private static ILogger Logger(ILoggerFactory factory) =>
        factory.CreateLogger("ConduitLLM.Admin.Endpoints.BatchSpending");
}
