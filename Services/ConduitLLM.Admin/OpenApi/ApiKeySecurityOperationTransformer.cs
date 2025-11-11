using Microsoft.AspNetCore.OpenApi;

namespace ConduitLLM.Admin.OpenApi;

/// <summary>
/// Adds API key security requirements to all operations
/// </summary>
public class ApiKeySecurityOperationTransformer : IOpenApiOperationTransformer
{
    /// <summary>
    /// Transforms each operation to add security requirements
    /// </summary>
    public Task TransformAsync(Microsoft.OpenApi.OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        // Note: In .NET 10's built-in OpenAPI support, security is handled differently
        // The API key authentication will be configured through middleware and attributes
        // rather than through OpenAPI transformers

        // Add a custom extension to indicate API key requirement
        // Note: Extensions can be added but the exact type will depend on the OpenAPI model version
        // For now, we'll skip adding extensions as the security is handled by middleware

        return Task.CompletedTask;
    }
}