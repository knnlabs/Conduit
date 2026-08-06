using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ConduitLLM.Admin.OpenApi;

/// <summary>
/// Restores the JSON Schema string type for temporal values handled by custom UTC converters.
/// ASP.NET preserves the date-time format for those converters but otherwise emits no type.
/// </summary>
public sealed class TemporalSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        var type = context.JsonTypeInfo.Type;
        var underlying = Nullable.GetUnderlyingType(type);
        if ((underlying ?? type) is { } temporal
            && (temporal == typeof(DateTime) || temporal == typeof(DateTimeOffset)))
        {
            schema.Type = JsonSchemaType.String |
                (underlying is null ? 0 : JsonSchemaType.Null);
            schema.Format = "date-time";
        }

        return Task.CompletedTask;
    }
}
