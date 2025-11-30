using Microsoft.AspNetCore.OpenApi;
using System.Reflection;

namespace ConduitLLM.Gateway.OpenApi;

/// <summary>
/// Transforms the OpenAPI document for the Core API
/// </summary>
public class CoreApiDocumentTransformer : IOpenApiDocumentTransformer
{
    /// <summary>
    /// Transforms the OpenAPI document to add custom metadata and configurations
    /// </summary>
    public Task TransformAsync(Microsoft.OpenApi.OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        // Set the API information
        document.Info.Title = "Conduit Core API";
        document.Info.Version = "v1";
        document.Info.Description = "OpenAI-compatible API for multi-provider LLM access - Requires Bearer token authentication";

        // Note: With Microsoft.AspNetCore.OpenApi in .NET 10, security schemes are handled differently
        // The authentication is enforced by middleware (VirtualKeyAuthenticationHandler)
        // Scalar will detect the authentication requirements from the OpenAPI document

        return Task.CompletedTask;
    }
}
