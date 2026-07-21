using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

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

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[ApiKeySecurityOperationTransformer.SecuritySchemeName] =
            new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.ApiKey,
                Name = "X-Master-Key",
                In = ParameterLocation.Header,
                Description = "ConduitLLM Admin API master key."
            };

        return Task.CompletedTask;
    }
}
