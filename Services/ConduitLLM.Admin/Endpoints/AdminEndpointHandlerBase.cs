using System.Diagnostics;
using System.Security.Claims;

using ConduitLLM.Admin.Auditing;
using ConduitLLM.Configuration.Messaging;
using ConduitLLM.Core.Metrics;

namespace ConduitLLM.Admin.Endpoints;

/// <summary>Shared non-MVC support for stateful Admin endpoint handlers.</summary>
public abstract class AdminEndpointHandlerBase
{
    private readonly IEventBus? _eventBus;
    private readonly IHttpContextAccessor _httpContextAccessor;

    protected AdminEndpointHandlerBase(
        IEventBus? eventBus,
        IHttpContextAccessor httpContextAccessor,
        ILogger logger)
    {
        _eventBus = eventBus;
        _httpContextAccessor = httpContextAccessor;
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected ILogger Logger { get; }
    protected HttpContext HttpContext => _httpContextAccessor.HttpContext
        ?? throw new InvalidOperationException("No active HTTP context is available.");
    protected ClaimsPrincipal User => HttpContext.User;

    protected void LogAdminAudit(string operation, string entityType, object? entityId = null, string? detail = null) =>
        AdminAudit.Log(HttpContext, Logger, operation, entityType, entityId, detail);

    protected void LogAdminAuditWithChanges(
        string entityType,
        object? entityId,
        IReadOnlyList<(string Property, string? OldValue, string? NewValue)> changes,
        string? detail = null) =>
        AdminAudit.LogWithChanges(HttpContext, Logger, entityType, entityId, changes, detail);

    protected void LogAdminAuditBulk(string operation, string entityType, int successCount, int failureCount) =>
        AdminAudit.LogBulk(HttpContext, Logger, operation, entityType, successCount, failureCount);

    protected void LogAdminAuditStateChange(string entityType, object? entityId, string property, object newValue) =>
        AdminAudit.LogStateChange(HttpContext, Logger, entityType, entityId, property, newValue);

    protected void PublishEventFireAndForget<TEvent>(TEvent domainEvent, string operationName, object? contextData = null)
        where TEvent : class
    {
        if (_eventBus is null)
        {
            Logger.LogDebug("Event publishing not configured - skipping {EventType} for {Operation}", typeof(TEvent).Name, operationName);
            EventPublishingMetrics.RecordSkipped(typeof(TEvent).Name);
            return;
        }

        _ = Task.Run(async () =>
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                await _eventBus.PublishAsync(domainEvent);
                EventPublishingMetrics.RecordSuccess(typeof(TEvent).Name, stopwatch.Elapsed.TotalSeconds);
                Logger.LogDebug("Published {EventType} event for {Operation} with context {ContextData}",
                    typeof(TEvent).Name, operationName, contextData);
            }
            catch (Exception exception)
            {
                EventPublishingMetrics.RecordFailure(typeof(TEvent).Name);
                Logger.LogWarning(exception,
                    "Failed to publish {EventType} event for {Operation} - operation completed but event not sent",
                    typeof(TEvent).Name, operationName);
            }
        });
    }

    protected static IResult Ok<T>(T value) => Results.Ok(value);
    protected static IResult BadRequest<T>(T value) => Results.BadRequest(value);
    protected static IResult NotFound<T>(T value) => Results.NotFound(value);
    protected static IResult Conflict<T>(T value) => Results.Conflict(value);
    protected static IResult NoContent() => Results.NoContent();
    protected static IResult StatusCode<T>(int statusCode, T value) => Results.Json(value, statusCode: statusCode);
}
