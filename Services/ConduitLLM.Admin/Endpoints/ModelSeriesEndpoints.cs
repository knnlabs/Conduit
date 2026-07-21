using ConduitLLM.Admin.Auditing;
using ConduitLLM.Admin.Extensions;
using ConduitLLM.Admin.Models.ModelSeries;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Core.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace ConduitLLM.Admin.Endpoints;

public static class ModelSeriesEndpoints
{
    public static IEndpointRouteBuilder MapModelSeriesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/ModelSeries")
            .RequireAuthorization("MasterKeyPolicy")
            .AddEndpointFilter<OperationLoggingEndpointFilter>()
            .AddEndpointFilter<ValidationEndpointFilter>()
            .WithTags("ModelSeries");

        group.MapGet("/", GetAll).WithName("ModelSeries_GetAll")
            .Produces<IEnumerable<ModelSeriesDto>>(StatusCodes.Status200OK);
        group.MapGet("/{id}", GetById).WithName("ModelSeries_GetById")
            .Produces<ModelSeriesDto>(StatusCodes.Status200OK)
            .Produces<ErrorResponseDto>(StatusCodes.Status404NotFound);
        group.MapGet("/{id}/models", GetModelsInSeries).WithName("ModelSeries_GetModels")
            .Produces<IEnumerable<SeriesSimpleModelDto>>(StatusCodes.Status200OK)
            .Produces<ErrorResponseDto>(StatusCodes.Status404NotFound);
        group.MapPost("/", Create).WithName("ModelSeries_Create")
            .Produces<ModelSeriesDto>(StatusCodes.Status201Created)
            .Produces<ErrorResponseDto>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponseDto>(StatusCodes.Status409Conflict);
        group.MapPut("/{id}", Update).WithName("ModelSeries_Update")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErrorResponseDto>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponseDto>(StatusCodes.Status404NotFound)
            .Produces<ErrorResponseDto>(StatusCodes.Status409Conflict);
        group.MapDelete("/{id}", Delete).WithName("ModelSeries_Delete")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErrorResponseDto>(StatusCodes.Status404NotFound)
            .Produces<ErrorResponseDto>(StatusCodes.Status409Conflict);
        return app;
    }

    private static async Task<IResult> GetAll([FromServices] IModelSeriesRepository repository) =>
        Results.Ok((await repository.GetAllWithAuthorAsync()).Select(series => series.ToDto()));

    private static async Task<IResult> GetById(int id, [FromServices] IModelSeriesRepository repository)
    {
        var series = await repository.GetByIdWithAuthorAsync(id);
        return series is null ? AdminResults.NotFoundEntity("Model series", id) : Results.Ok(series.ToDto());
    }

    private static async Task<IResult> GetModelsInSeries(int id, [FromServices] IModelSeriesRepository repository)
    {
        var models = await repository.GetModelsInSeriesAsync(id);
        if (models is null)
        {
            return AdminResults.NotFoundEntity("Model series", id);
        }
        return Results.Ok(models.Select(model => new SeriesSimpleModelDto
        {
            Id = model.Id,
            Name = model.Name,
            Version = model.Version,
            IsActive = model.IsActive
        }));
    }

    private static async Task<IResult> Create(
        CreateModelSeriesDto dto,
        [FromServices] IModelSeriesRepository repository,
        HttpContext context,
        ILoggerFactory loggerFactory)
    {
        if (await repository.GetByNameAndAuthorAsync(dto.Name, dto.AuthorId) is not null)
        {
            throw new InvalidOperationException(
                $"A model series with name '{dto.Name}' already exists for this author");
        }
        var series = new ModelSeries
        {
            AuthorId = dto.AuthorId,
            Name = dto.Name,
            Description = dto.Description,
            TokenizerType = dto.TokenizerType,
            Parameters = dto.Parameters ?? "{}"
        };
        await repository.CreateAsync(series);
        var reloaded = await repository.GetByIdWithAuthorAsync(series.Id)
            ?? throw new InvalidOperationException("Failed to reload created series");
        AdminAudit.Log(context, Logger(loggerFactory), "Created", "ModelSeries", reloaded.Id,
            $"Name: {LoggingSanitizer.S(reloaded.Name)}");
        return Results.Created($"/api/ModelSeries/{reloaded.Id}", reloaded.ToDto());
    }

    private static async Task<IResult> Update(
        int id,
        UpdateModelSeriesDto dto,
        [FromServices] IModelSeriesRepository repository,
        HttpContext context,
        ILoggerFactory loggerFactory)
    {
        if (id != dto.Id)
        {
            return AdminResults.BadRequest("ID mismatch");
        }
        var series = await repository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Model series with ID {id} not found");
        if (!string.IsNullOrEmpty(dto.Name) && dto.Name != series.Name)
        {
            var existing = await repository.GetByNameAndAuthorAsync(dto.Name, series.AuthorId);
            if (existing is not null && existing.Id != id)
            {
                throw new InvalidOperationException(
                    $"A model series with name '{dto.Name}' already exists for this author");
            }
            series.Name = dto.Name;
        }
        if (dto.Description is not null) series.Description = dto.Description;
        if (dto.TokenizerType.HasValue) series.TokenizerType = dto.TokenizerType.Value;
        if (dto.Parameters is not null) series.Parameters = dto.Parameters;
        await repository.UpdateAsync(series);
        AdminAudit.Log(context, Logger(loggerFactory), "Updated", "ModelSeries", id,
            $"Name: {LoggingSanitizer.S(series.Name)}");
        return Results.NoContent();
    }

    private static async Task<IResult> Delete(
        int id,
        [FromServices] IModelSeriesRepository repository,
        HttpContext context,
        ILoggerFactory loggerFactory)
    {
        var series = await repository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Model series with ID {id} not found");
        var models = await repository.GetModelsInSeriesAsync(id);
        if (models is not null && models.Any())
        {
            throw new InvalidOperationException(
                $"Cannot delete model series with {models.Count()} associated models. Delete the models first.");
        }
        await repository.DeleteAsync(id);
        AdminAudit.Log(context, Logger(loggerFactory), "Deleted", "ModelSeries", id,
            $"Name: {LoggingSanitizer.S(series.Name)}");
        return Results.NoContent();
    }

    private static ILogger Logger(ILoggerFactory factory) =>
        factory.CreateLogger("ConduitLLM.Admin.Endpoints.ModelSeries");
}
