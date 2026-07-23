using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Gateway.Services;
using ConduitLLM.Gateway.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace ConduitLLM.Gateway.Endpoints;

/// <summary>Minimal-API model discovery endpoints.</summary>
public static class ModelsEndpoints
{
    public static IEndpointRouteBuilder MapModelsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1")
            .RequireAuthorization("VirtualKeyAuthentication")
            .AddEndpointFilter<OperationLoggingEndpointFilter>()
            .WithTags("Models");

        group.MapGet("/models", ListModels)
            .WithName("Models_ListModels")
            .Produces<ModelListResponse>(StatusCodes.Status200OK)
            .Produces<OpenAIErrorResponse>(StatusCodes.Status500InternalServerError);

        group.MapGet("/models/{modelId}/metadata", GetModelMetadata)
            .WithName("Models_GetModelMetadata")
            .Produces<ModelMetadataResponse>(StatusCodes.Status200OK)
            .Produces<OpenAIErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<OpenAIErrorResponse>(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> ListModels(
        [FromServices] IModelProviderMappingRepository repository,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("ConduitLLM.Gateway.Endpoints.Models");
        logger.LogInformation("Getting available models");

        var allMappings = new List<Configuration.Entities.ModelProviderMapping>();
        var pageNumber = 1;
        const int pageSize = 100;
        while (true)
        {
            var (mappings, totalCount) = await repository.GetPaginatedAsync(
                pageNumber, pageSize, cancellationToken);
            allMappings.AddRange(mappings);
            if (allMappings.Count >= totalCount || mappings.Count == 0)
            {
                break;
            }
            pageNumber++;
        }

        var data = allMappings
            .Select(mapping => mapping.ModelAlias)
            .Distinct()
            .Select(alias => new ModelListItemDto(alias, "model"))
            .ToList();
        logger.LogDebug("Returning {ModelCount} available models", data.Count);
        return Results.Ok(new ModelListResponse(data, "list"));
    }

    private static async Task<IResult> GetModelMetadata(
        string modelId,
        [FromServices] IModelMetadataService metadataService,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("ConduitLLM.Gateway.Endpoints.Models");
        logger.LogInformation("Getting metadata for model {ModelId}", modelId);
        var metadata = await metadataService.GetModelMetadataAsync(modelId);
        return metadata is null
            ? GatewayResults.OpenAIError(
                StatusCodes.Status404NotFound,
                $"No metadata found for model '{modelId}'",
                "model_not_found")
            : Results.Ok(new ModelMetadataResponse(modelId, metadata));
    }
}
