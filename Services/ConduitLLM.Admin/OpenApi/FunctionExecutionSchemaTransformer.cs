using ConduitLLM.Functions.DTOs;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ConduitLLM.Admin.OpenApi;

/// <summary>
/// Publishes the Admin execution resource as the canonical shared execution schema plus a scoped
/// Admin diagnostics property.
/// </summary>
public sealed class FunctionExecutionSchemaTransformer : IOpenApiSchemaTransformer
{
    public async Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (context.JsonTypeInfo.Type != typeof(AdminFunctionExecutionDto) ||
            schema.Properties is null ||
            !schema.Properties.TryGetValue("admin", out var adminSchema))
        {
            return;
        }

        var baseSchema = await context.GetOrCreateSchemaAsync(
            typeof(FunctionExecutionDto),
            parameterDescription: null,
            cancellationToken);

        schema.Properties = new Dictionary<string, IOpenApiSchema>
        {
            ["admin"] = adminSchema
        };
        schema.AllOf = [baseSchema];
    }
}
