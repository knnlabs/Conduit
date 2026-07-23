using System.Diagnostics;
using System.Security.Claims;

using ConduitLLM.Configuration.Messaging;
using ConduitLLM.Core.Metrics;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Gateway.Endpoints;

/// <summary>Shared non-MVC support for stateful Gateway endpoint handlers.</summary>
public abstract class GatewayEndpointHandlerBase
{
    private readonly IEventBus? _eventBus;
    private readonly IHttpContextAccessor _httpContextAccessor;

    protected GatewayEndpointHandlerBase(IEventBus? eventBus, IHttpContextAccessor httpContextAccessor, ILogger logger)
    {
        _eventBus = eventBus;
        _httpContextAccessor = httpContextAccessor;
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected ILogger Logger { get; }
    protected HttpContext HttpContext => _httpContextAccessor.HttpContext
        ?? throw new InvalidOperationException("No active HTTP context is available.");
    protected HttpRequest Request => HttpContext.Request;
    protected HttpResponse Response => HttpContext.Response;
    protected ClaimsPrincipal User => HttpContext.User;

    protected int? CurrentVirtualKeyId
    {
        get
        {
            if (HttpContext.Items.TryGetValue("VirtualKeyId", out var value) && value is int id) return id;
            return int.TryParse(User.FindFirst("VirtualKeyId")?.Value, out var parsed) ? parsed : null;
        }
    }

    protected string? CurrentVirtualKey =>
        HttpContext.Items.TryGetValue("VirtualKey", out var value) && value is string key && key.Length > 0
            ? key
            : User.FindFirst("VirtualKey")?.Value;

    protected static IResult Ok<T>(T value) => Results.Ok(value);
    protected static IResult Ok() => Results.Ok();
    protected static IResult BadRequest<T>(T value) => Results.BadRequest(value);
    protected static IResult NotFound<T>(T value) => Results.NotFound(value);
    protected static IResult NotFound() =>
        GatewayResults.OpenAIError(StatusCodes.Status404NotFound, "Resource not found", "not_found", "not_found_error");
    protected static IResult Unauthorized<T>(T value) => Results.Json(value, statusCode: StatusCodes.Status401Unauthorized);
    protected static IResult Conflict<T>(T value) => Results.Conflict(value);
    protected static IResult Accepted<T>(T value) => Results.Json(value, statusCode: StatusCodes.Status202Accepted);
    protected static IResult Forbid(params string[] messages) =>
        GatewayResults.OpenAIError(
            StatusCodes.Status403Forbidden,
            messages.FirstOrDefault() ?? "Access forbidden",
            "forbidden",
            "permission_error");
    protected static IResult NoContent() => Results.NoContent();
    protected static IResult File(Stream stream, string contentType, string? fileDownloadName = null, bool enableRangeProcessing = false) =>
        Results.Stream(stream, contentType, fileDownloadName, enableRangeProcessing: enableRangeProcessing);
    protected static IResult File(byte[] contents, string contentType, string? fileDownloadName = null, bool enableRangeProcessing = false) =>
        Results.File(contents, contentType, fileDownloadName, enableRangeProcessing: enableRangeProcessing);
    protected static IResult Content(string content, string contentType) => Results.Text(content, contentType);
    protected static IResult StatusCode<T>(int statusCode, T value) => Results.Json(value, statusCode: statusCode);
    protected static IResult OpenAIError(int statusCode, string message, string code, string type = "invalid_request_error") =>
        GatewayResults.OpenAIError(statusCode, message, code, type);

    protected void PublishEventFireAndForget<TEvent>(TEvent domainEvent, string operationName, object? contextData = null)
        where TEvent : class
    {
        if (_eventBus is null)
        {
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
            }
            catch (Exception exception)
            {
                EventPublishingMetrics.RecordFailure(typeof(TEvent).Name);
                Logger.LogWarning(exception, "Failed to publish {EventType} for {Operation}", typeof(TEvent).Name, operationName);
            }
        });
    }
}
