using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ConduitLLM.Admin.OpenApi;

/// <summary>Documents ETag and If-Match semantics for versioned Admin resources.</summary>
public sealed class ConditionalRequestOperationTransformer : IOpenApiOperationTransformer
{
    private static readonly string[] VersionedRoots =
    [
        "v1/admin/ip-filters/",
        "v1/admin/virtual-keys/",
        "v1/admin/virtual-key-groups/"
    ];

    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        var path = context.Description.RelativePath ?? string.Empty;
        if (!VersionedRoots.Any(path.StartsWith) || !path.Contains("{id", StringComparison.Ordinal))
            return Task.CompletedTask;

        operation.Responses ??= new OpenApiResponses();
        var method = context.Description.HttpMethod;

        if (method is "GET" or "HEAD")
        {
            if (operation.Responses.TryGetValue("200", out var success) && success is OpenApiResponse response)
            {
                response.Headers ??= new Dictionary<string, IOpenApiHeader>();
                response.Headers["ETag"] = new OpenApiHeader
                {
                    Description = "Strong validator for conditional resource mutations.",
                    Required = true,
                    Schema = new OpenApiSchema { Type = JsonSchemaType.String }
                };
            }
            return Task.CompletedTask;
        }

        operation.Parameters ??= [];
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "If-Match",
            In = ParameterLocation.Header,
            Required = true,
            Description = "ETag returned by the latest resource read. Use * only when any current version is acceptable.",
            Schema = new OpenApiSchema { Type = JsonSchemaType.String }
        });
        operation.Responses.TryAdd("412", new OpenApiResponse { Description = "Precondition Failed" });
        operation.Responses.TryAdd("428", new OpenApiResponse { Description = "Precondition Required" });
        return Task.CompletedTask;
    }
}
