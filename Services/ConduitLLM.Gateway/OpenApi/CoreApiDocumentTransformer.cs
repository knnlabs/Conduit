using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ConduitLLM.Gateway.OpenApi;

/// <summary>
/// Transforms the OpenAPI document for the Gateway API
/// </summary>
public class CoreApiDocumentTransformer : IOpenApiDocumentTransformer
{
    /// <summary>
    /// Transforms the OpenAPI document to add custom metadata and configurations
    /// </summary>
    public Task TransformAsync(Microsoft.OpenApi.OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        // Set the API information
        document.Info.Title = "Conduit Gateway API";
        document.Info.Version = "v1";
        document.Info.Description = "OpenAI-compatible API for multi-provider LLM access - Requires Bearer token authentication";

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[VirtualKeySecurityOperationTransformer.SecuritySchemeName] =
            new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "opaque virtual key",
                Description = "ConduitLLM virtual key supplied as an opaque Bearer token."
            };

        return Task.CompletedTask;
    }
}
