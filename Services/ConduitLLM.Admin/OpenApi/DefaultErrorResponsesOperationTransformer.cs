using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using ConduitLLM.Configuration.DTOs;

namespace ConduitLLM.Admin.OpenApi;

/// <summary>
/// Adds a default "500 Internal Server Error" response to every operation in the OpenAPI document,
/// documenting the Admin API's universal error behavior (the global <c>AdminExceptionMiddleware</c>
/// maps unhandled exceptions to a standardized <c>ErrorResponseDto</c>).
/// </summary>
/// <remarks>
/// This lets endpoint mappings omit repeated 500-response metadata — every endpoint can return
/// 500, so it is documented once here.
/// </remarks>
public sealed class DefaultErrorResponsesOperationTransformer : IOpenApiOperationTransformer
{
    /// <inheritdoc/>
    public async Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        operation.Responses ??= new OpenApiResponses();

        if (!operation.Responses.ContainsKey("500"))
        {
            var schema = await context.GetOrCreateSchemaAsync(
                typeof(ErrorResponseDto),
                parameterDescription: null,
                cancellationToken);
            operation.Responses["500"] = new OpenApiResponse
            {
                Description = "Internal server error. Returns a standardized ErrorResponseDto.",
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new() { Schema = schema }
                }
            };
        }
    }
}
