using Microsoft.AspNetCore.OpenApi;
using System.Reflection;

namespace ConduitLLM.Admin.OpenApi;

/// <summary>
/// Transforms the OpenAPI document for the Admin API
/// </summary>
public class AdminApiDocumentTransformer : IOpenApiDocumentTransformer
{
    /// <summary>
    /// Transforms the OpenAPI document to add custom metadata and configurations
    /// </summary>
    public Task TransformAsync(Microsoft.OpenApi.OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        // Set the API information
        document.Info.Title = "ConduitLLM Admin API";
        document.Info.Version = "v1";
        document.Info.Description = "Administrative API for ConduitLLM - Requires X-Master-Key header for authentication";

        // Note: With Microsoft.AspNetCore.OpenApi in .NET 10, security schemes are handled differently
        // The authentication is enforced by middleware (MasterKeyAuthenticationHandler)
        // Scalar will detect the authentication requirements from the OpenAPI document

        return Task.CompletedTask;
    }
}