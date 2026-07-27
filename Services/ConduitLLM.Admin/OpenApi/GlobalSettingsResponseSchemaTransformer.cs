using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Core.Models;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ConduitLLM.Admin.OpenApi;

/// <summary>Marks fields that Global Settings response objects always emit as required.</summary>
public sealed class GlobalSettingsResponseSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        var type = context.JsonTypeInfo.Type;
        if (type == typeof(GlobalSettingDto) || type == typeof(CacheStats))
        {
            schema.Required ??= new HashSet<string>();
            foreach (var propertyName in schema.Properties?.Keys ?? [])
                schema.Required.Add(propertyName);
        }

        return Task.CompletedTask;
    }
}
