using ConduitLLM.Core.OpenApi;

using Microsoft.OpenApi;

namespace ConduitLLM.Gateway.OpenApi;

/// <summary>
/// Transforms the OpenAPI document for the Gateway API
/// </summary>
public sealed class CoreApiDocumentTransformer : ApiDocumentTransformer
{
    public CoreApiDocumentTransformer()
        : base(
            "Conduit Gateway API",
            "OpenAI-compatible API for multi-provider LLM access - Requires Bearer token authentication",
            VirtualKeySecurityOperationTransformer.SecuritySchemeName,
            new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "opaque virtual key",
                Description = "ConduitLLM virtual key supplied as an opaque Bearer token."
            })
    {
    }
}
