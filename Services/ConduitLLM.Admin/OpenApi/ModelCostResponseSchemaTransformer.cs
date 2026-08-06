using ConduitLLM.Configuration.DTOs;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ConduitLLM.Admin.OpenApi;

/// <summary>Marks fields that model-cost response objects always emit as required.</summary>
public sealed class ModelCostResponseSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        var type = context.JsonTypeInfo.Type;
        if (type == typeof(ModelCostDto) || type == typeof(ModelCostOverviewDto) ||
            type == typeof(BulkImportResult) || type == typeof(PagedResult<ModelCostDto>))
        {
            schema.Required ??= new HashSet<string>();
            foreach (var propertyName in schema.Properties?.Keys ?? [])
                schema.Required.Add(propertyName);
        }

        return Task.CompletedTask;
    }
}
