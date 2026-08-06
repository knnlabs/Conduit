using System.Text.Json;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ConduitLLM.Admin.OpenApi;

/// <summary>Describes structured JSON object dictionaries as open-ended objects for SDKs.</summary>
public sealed class StructuredJsonSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (context.JsonTypeInfo.Type == typeof(Dictionary<string, JsonElement>) ||
            context.JsonPropertyInfo?.PropertyType == typeof(Dictionary<string, JsonElement>))
        {
            schema.Type = JsonSchemaType.Object;
            schema.AdditionalPropertiesAllowed = true;
            schema.AdditionalProperties = new OpenApiSchema();
        }

        return Task.CompletedTask;
    }
}
