using ConduitLLM.Admin.Auditing;
using ConduitLLM.Admin.DTOs;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ConduitLLM.Admin.Endpoints;

public static class TasksEndpoints
{
    public static IEndpointRouteBuilder MapAdminTasksEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/admin/tasks")
            .RequireAuthorization("MasterKeyPolicy")
            .AddEndpointFilter<OperationLoggingEndpointFilter>()
            .AddEndpointFilter<ValidationEndpointFilter>()
            .WithTags("Tasks");
        group.MapPost("/cleanup", Cleanup).WithName("Tasks_Cleanup")
            .Produces<TaskCleanupResponseDto>(StatusCodes.Status200OK);
        group.MapPost("/{taskId}/resolve", Resolve).WithName("Tasks_ResolveIndeterminate")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<AdminProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces<AdminProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json");
        return app;
    }

    private static async Task<IResult> Cleanup(
        int olderThanHours,
        [FromServices] IAsyncTaskService taskService,
        HttpContext context,
        ILoggerFactory loggerFactory)
    {
        olderThanHours = olderThanHours == 0 ? 24 : Math.Max(olderThanHours, 1);
        var result = await taskService.CleanupOldTasksAsync(new AsyncTaskRetentionPolicy(
            TimeSpan.FromHours(olderThanHours),
            TimeSpan.FromDays(30),
            TimeSpan.FromDays(7)));
        AdminAudit.Log(context, Logger(loggerFactory), "CleanedUp", "Tasks", detail:
            $"Archived {result.Archived} and deleted {result.Deleted} tasks");
        return Results.Ok(new TaskCleanupResponseDto
        {
            CleanedUp = result.Total,
            Archived = result.Archived,
            Deleted = result.Deleted,
            OlderThanHours = olderThanHours
        });
    }

    private static async Task<IResult> Resolve(
        string taskId,
        ResolveIndeterminateTaskDto request,
        [FromServices] IAsyncTaskService taskService,
        HttpContext context,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<IndeterminateTaskResolution>(
                request.Resolution.Replace("_", string.Empty), true, out var resolution))
        {
            return AdminResults.BadRequest("Resolution must be safe_to_retry, failed, or completed");
        }
        var updated = await taskService.ResolveIndeterminateTaskAsync(
            taskId, resolution, request.Reason, request.ProviderOperationId, cancellationToken);
        if (!updated) return AdminResults.NotFound("Indeterminate task was not found", "not_found");
        AdminAudit.Log(context, Logger(loggerFactory), "Resolved", "AsyncTask", detail:
            $"TaskId: {taskId}, Resolution: {resolution}, Reason: {request.Reason}");
        return Results.NoContent();
    }

    private static ILogger Logger(ILoggerFactory factory) =>
        factory.CreateLogger("ConduitLLM.Admin.Endpoints.Tasks");
}
