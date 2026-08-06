using ConduitLLM.Core.OpenApi;

using Microsoft.OpenApi;

namespace ConduitLLM.Admin.OpenApi;

/// <summary>
/// Transforms the OpenAPI document for the Admin API
/// </summary>
public sealed class AdminApiDocumentTransformer : ApiDocumentTransformer
{
    public AdminApiDocumentTransformer()
        : base(
            "ConduitLLM Admin API",
            "Administrative API for ConduitLLM - Requires X-Master-Key header for authentication",
            ApiKeySecurityOperationTransformer.SecuritySchemeName,
            new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.ApiKey,
                Name = "X-Master-Key",
                In = ParameterLocation.Header,
                Description = "ConduitLLM Admin API master key."
            })
    {
    }
}
