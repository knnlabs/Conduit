using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;

namespace ConduitLLM.Gateway.OpenApi;

/// <summary>
/// Publishes the Gateway's virtual-key requirement from endpoint authorization metadata.
/// </summary>
public class VirtualKeySecurityOperationTransformer : IOpenApiOperationTransformer
{
    public const string SecuritySchemeName = "VirtualKey";

    /// <summary>
    /// Transforms each operation to add security requirements
    /// </summary>
    public Task TransformAsync(Microsoft.OpenApi.OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;
        operation.Security = new List<OpenApiSecurityRequirement>();

        if (metadata.OfType<IAllowAnonymous>().Any() || !metadata.OfType<IAuthorizeData>().Any())
        {
            return Task.CompletedTask;
        }

        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(SecuritySchemeName, context.Document, null)] = []
        });

        return Task.CompletedTask;
    }
}
