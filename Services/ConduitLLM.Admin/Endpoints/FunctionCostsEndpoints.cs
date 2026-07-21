using ConduitLLM.Admin.Auditing;
using ConduitLLM.Admin.DTOs;
using ConduitLLM.Admin.Extensions;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Core.Extensions;
using ConduitLLM.Functions.DTOs;
using ConduitLLM.Functions.Entities;
using ConduitLLM.Functions.Interfaces;

using Microsoft.AspNetCore.Mvc;

namespace ConduitLLM.Admin.Endpoints;

public static class FunctionCostsEndpoints
{
    public static IEndpointRouteBuilder MapFunctionCostsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/FunctionCosts")
            .RequireAuthorization("MasterKeyPolicy")
            .AddEndpointFilter<OperationLoggingEndpointFilter>()
            .WithTags("FunctionCosts");

        group.MapGet("/", List).WithName("FunctionCosts_List").Produces<List<FunctionCostDto>>();
        group.MapGet("/{id:int}", GetById).WithName("FunctionCosts_GetById")
            .Produces<FunctionCostDto>().Produces<ErrorResponseDto>(StatusCodes.Status404NotFound);
        group.MapGet("/configuration/{functionConfigurationId:int}", GetByConfiguration)
            .WithName("FunctionCosts_GetByConfiguration")
            .Produces<FunctionCostDto>().Produces<ErrorResponseDto>(StatusCodes.Status404NotFound);
        group.MapPost("/", Create).WithName("FunctionCosts_Create")
            .Produces<FunctionCostDto>(StatusCodes.Status201Created)
            .Produces<ErrorResponseDto>(StatusCodes.Status400BadRequest);
        group.MapPut("/{id:int}", Update).WithName("FunctionCosts_Update")
            .Produces<FunctionCostDto>().Produces<ErrorResponseDto>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponseDto>(StatusCodes.Status404NotFound);
        group.MapDelete("/{id:int}", Delete).WithName("FunctionCosts_Delete")
            .Produces(StatusCodes.Status204NoContent).Produces<ErrorResponseDto>(StatusCodes.Status404NotFound);
        group.MapPost("/cache/clear", ClearCache).WithName("FunctionCosts_ClearCache")
            .Produces<FunctionCostCacheClearResultDto>();
        return app;
    }

    private static async Task<IResult> List([FromServices] IFunctionCostService service) =>
        Results.Ok((await service.ListCostsAsync()).Select(cost => cost.ToDto()).ToList());

    private static async Task<IResult> GetById(int id, [FromServices] IFunctionCostService service)
    {
        var dto = (await service.GetCostByIdAsync(id))?.ToDto();
        return dto is null ? AdminResults.NotFoundEntity("Function cost", id) : Results.Ok(dto);
    }

    private static async Task<IResult> GetByConfiguration(
        int functionConfigurationId,
        [FromServices] IFunctionCostService service)
    {
        var dto = (await service.GetCostForConfigurationAsync(functionConfigurationId))?.ToDto();
        return dto is null
            ? AdminResults.NotFoundEntity("Function cost for configuration", functionConfigurationId)
            : Results.Ok(dto);
    }

    private static async Task<IResult> Create(
        [FromBody] CreateFunctionCostDto createDto,
        [FromServices] IFunctionCostService service,
        HttpContext httpContext,
        ILoggerFactory loggerFactory)
    {
        var id = await service.CreateCostAsync(MapToEntity(createDto));
        var dto = (await service.GetCostByIdAsync(id))?.ToDto();
        Audit(httpContext, loggerFactory, "Created", id, createDto.CostName);
        return Results.Created($"/api/FunctionCosts/{id}", dto);
    }

    private static async Task<IResult> Update(
        int id,
        [FromBody] UpdateFunctionCostDto updateDto,
        [FromServices] IFunctionCostService service,
        HttpContext httpContext,
        ILoggerFactory loggerFactory)
    {
        if (id != updateDto.Id)
        {
            return AdminResults.BadRequest("ID mismatch");
        }

        var existing = await service.GetCostByIdAsync(id) ?? throw new KeyNotFoundException();
        await service.UpdateCostAsync(MapToEntity(updateDto, existing));
        var updated = await service.GetCostByIdAsync(id);
        Audit(httpContext, loggerFactory, "Updated", id, updateDto.CostName);
        return Results.Ok(updated?.ToDto());
    }

    private static async Task<IResult> Delete(
        int id,
        [FromServices] IFunctionCostService service,
        HttpContext httpContext,
        ILoggerFactory loggerFactory)
    {
        var existing = await service.GetCostByIdAsync(id);
        await service.DeleteCostAsync(id);
        Audit(httpContext, loggerFactory, "Deleted", id, existing?.CostName);
        return Results.NoContent();
    }

    private static async Task<IResult> ClearCache(
        [FromServices] IFunctionCostService service,
        HttpContext httpContext,
        ILoggerFactory loggerFactory)
    {
        await service.ClearCacheAsync();
        AdminAudit.Log(httpContext, Logger(loggerFactory), "Cleared", "FunctionCostCache");
        return Results.Ok(new { message = "Function cost cache cleared successfully" });
    }

    private static void Audit(
        HttpContext context,
        ILoggerFactory loggerFactory,
        string operation,
        int id,
        string? costName) =>
        AdminAudit.Log(context, Logger(loggerFactory), operation, "FunctionCost", id,
            costName is null ? null : $"CostName: {LoggingSanitizer.S(costName)}");

    private static ILogger Logger(ILoggerFactory factory) =>
        factory.CreateLogger("ConduitLLM.Admin.Endpoints.FunctionCosts");

    private static FunctionCost MapToEntity(CreateFunctionCostDto dto) => new()
    {
        CostName = dto.CostName,
        ProviderType = dto.ProviderType,
        Purpose = dto.Purpose,
        Description = dto.Description,
        BaseCost = dto.BaseCost,
        PricingModel = dto.PricingModel,
        PricingConfiguration = dto.PricingConfiguration,
        IsActive = dto.IsActive,
        Priority = dto.Priority,
        EffectiveDate = dto.EffectiveDate,
        ExpiryDate = dto.ExpiryDate,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static FunctionCost MapToEntity(UpdateFunctionCostDto dto, FunctionCost existing)
    {
        existing.CostName = dto.CostName;
        existing.Purpose = dto.Purpose;
        existing.Description = dto.Description;
        existing.BaseCost = dto.BaseCost;
        existing.PricingModel = dto.PricingModel;
        existing.PricingConfiguration = dto.PricingConfiguration;
        existing.IsActive = dto.IsActive;
        existing.Priority = dto.Priority;
        existing.EffectiveDate = dto.EffectiveDate;
        existing.ExpiryDate = dto.ExpiryDate;
        existing.UpdatedAt = DateTime.UtcNow;
        return existing;
    }
}
