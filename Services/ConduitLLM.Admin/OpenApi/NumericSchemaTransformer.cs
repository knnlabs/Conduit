using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ConduitLLM.Admin.OpenApi;

/// <summary>
/// Normalizes numeric schemas to plain integer/number types.
/// </summary>
/// <remarks>
/// ASP.NET Core's web defaults allow reading numbers from JSON strings
/// (<c>JsonNumberHandling.AllowReadingFromString</c>), so the built-in OpenAPI
/// generator emits numeric properties as <c>"type": ["integer", "string"]</c>
/// with a numeric pattern. The API always writes numbers, and typed clients
/// always send numbers, so the published contract should be numeric only —
/// otherwise every generated TypeScript property widens to <c>number | string</c>.
/// </remarks>
public sealed class NumericSchemaTransformer : IOpenApiSchemaTransformer
{
    /// <summary>
    /// Removes the string variant (and its numeric pattern) from integer/number schemas.
    /// </summary>
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        if (schema.Type is { } type &&
            (type & JsonSchemaType.String) != 0 &&
            (type & (JsonSchemaType.Integer | JsonSchemaType.Number)) != 0)
        {
            schema.Type = type & ~JsonSchemaType.String;
            schema.Pattern = null;
        }

        return Task.CompletedTask;
    }
}
