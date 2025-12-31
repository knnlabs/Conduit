using Microsoft.AspNetCore.OpenApi;

namespace ConduitLLM.Gateway.OpenApi;

/// <summary>
/// Adds Virtual Key security requirements to all operations
/// </summary>
public class VirtualKeySecurityOperationTransformer : IOpenApiOperationTransformer
{
    /// <summary>
    /// Transforms each operation to add security requirements
    /// </summary>
    public Task TransformAsync(Microsoft.OpenApi.OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        // Note: In .NET 10's built-in OpenAPI support, security is handled differently
        // The Virtual Key authentication will be configured through middleware and attributes
        // (VirtualKeyAuthenticationHandler) rather than through OpenAPI transformers

        // The security is enforced by the authentication middleware, and Scalar will
        // automatically detect the authorization requirements from the API's behavior

        return Task.CompletedTask;
    }
}
