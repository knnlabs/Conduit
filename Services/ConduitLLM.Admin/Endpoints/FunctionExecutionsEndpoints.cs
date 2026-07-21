using ConduitLLM.Admin.Auditing;
using ConduitLLM.Admin.DTOs;
using ConduitLLM.Admin.Extensions;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Functions.DTOs;
using ConduitLLM.Functions.Enums;
using ConduitLLM.Functions.Interfaces;
using ConduitLLM.Core.Extensions;

using Microsoft.AspNetCore.Mvc;

namespace ConduitLLM.Admin.Endpoints;

public static class FunctionExecutionsEndpoints
{
    public static IEndpointRouteBuilder MapFunctionExecutionsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/FunctionExecutions")
            .RequireAuthorization("MasterKeyPolicy")
            .AddEndpointFilter<OperationLoggingEndpointFilter>()
            .WithTags("FunctionExecutions");

        group.MapGet("/{id:guid}", GetById).WithName("FunctionExecutions_GetById")
            .Produces<FunctionExecutionDto>().Produces<ErrorResponseDto>(StatusCodes.Status404NotFound);
        group.MapGet("/virtualkey/{virtualKeyId:int}", GetByVirtualKey)
            .WithName("FunctionExecutions_GetByVirtualKey").Produces<List<FunctionExecutionDto>>();
        group.MapGet("/configuration/{functionConfigurationId:int}", GetByConfiguration)
            .WithName("FunctionExecutions_GetByConfiguration").Produces<List<FunctionExecutionDto>>();
        group.MapGet("/state/{state}", GetByState).WithName("FunctionExecutions_GetByState")
            .Produces<List<FunctionExecutionDto>>().Produces<ErrorResponseDto>(StatusCodes.Status400BadRequest);
        group.MapGet("/expired-leases", GetExpiredLeases).WithName("FunctionExecutions_GetExpiredLeases")
            .Produces<List<FunctionExecutionDto>>();
        group.MapGet("/ready-for-retry", GetReadyForRetry).WithName("FunctionExecutions_GetReadyForRetry")
            .Produces<List<FunctionExecutionDto>>();
        group.MapDelete("/cleanup", Cleanup).WithName("FunctionExecutions_Cleanup")
            .Produces<FunctionExecutionCleanupResultDto>().Produces<ErrorResponseDto>(StatusCodes.Status400BadRequest);
        return app;
    }

    private static async Task<IResult> GetById(Guid id, [FromServices] IFunctionExecutionRepository repository)
    {
        var execution = await repository.GetByIdAsync(id);
        return execution is null
            ? AdminResults.NotFoundEntity("Function execution", id)
            : Results.Ok(execution.ToDto());
    }

    private static async Task<IResult> GetByVirtualKey(int virtualKeyId, [FromServices] IFunctionExecutionRepository repository)
    {
        var executions = await repository.GetByVirtualKeyIdAsync(virtualKeyId);
        return Results.Ok(executions.Select(execution => execution.ToDto()).ToList());
    }

    private static async Task<IResult> GetByConfiguration(
        int functionConfigurationId,
        [FromServices] IFunctionExecutionRepository repository)
    {
        var executions = await repository.GetByFunctionConfigurationIdAsync(functionConfigurationId);
        return Results.Ok(executions.Select(execution => execution.ToDto()).ToList());
    }

    private static async Task<IResult> GetByState(string state, [FromServices] IFunctionExecutionRepository repository)
    {
        if (!Enum.TryParse<ExecutionState>(state, true, out var parsedState))
        {
            return AdminResults.BadRequest($"Invalid execution state: {state}");
        }

        var executions = await repository.GetByStateAsync(parsedState);
        return Results.Ok(executions.Select(execution => execution.ToDto()).ToList());
    }

    private static async Task<IResult> GetExpiredLeases([FromServices] IFunctionExecutionRepository repository)
    {
        var executions = await repository.GetExpiredLeasesAsync();
        return Results.Ok(executions.Select(execution => execution.ToDto()).ToList());
    }

    private static async Task<IResult> GetReadyForRetry([FromServices] IFunctionExecutionRepository repository)
    {
        var executions = await repository.GetReadyForRetryAsync();
        return Results.Ok(executions.Select(execution => execution.ToDto()).ToList());
    }

    private static async Task<IResult> Cleanup(
        [FromServices] IFunctionExecutionRepository repository,
        HttpContext httpContext,
        ILoggerFactory loggerFactory,
        [FromQuery] int olderThanDays = 30)
    {
        if (olderThanDays < 1)
        {
            return AdminResults.BadRequest("olderThanDays must be at least 1");
        }

        var deletedCount = await repository.DeleteOldExecutionsAsync(DateTime.UtcNow.AddDays(-olderThanDays));
        AdminAudit.Log(
            httpContext,
            loggerFactory.CreateLogger("ConduitLLM.Admin.Endpoints.FunctionExecutions"),
            "Cleanup",
            "FunctionExecution",
            detail: $"OlderThanDays: {olderThanDays}, DeletedCount: {deletedCount}");
        return Results.Ok(new
        {
            deletedCount,
            message = $"Deleted {deletedCount} executions older than {olderThanDays} days"
        });
    }
}
