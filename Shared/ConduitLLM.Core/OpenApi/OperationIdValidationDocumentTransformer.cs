using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ConduitLLM.Core.OpenApi;

/// <summary>Rejects incomplete or ambiguous operation IDs during document generation.</summary>
public sealed class OperationIdValidationDocumentTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        Validate(document);
        return Task.CompletedTask;
    }

    public static void Validate(OpenApiDocument document)
    {
        var seen = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var path in document.Paths ?? new OpenApiPaths())
        {
            if (path.Value is null)
            {
                continue;
            }

            foreach (var operationEntry in path.Value.Operations ??
                new Dictionary<System.Net.Http.HttpMethod, OpenApiOperation>())
            {
                var location = $"{operationEntry.Key.Method} {path.Key}";
                if (operationEntry.Value is null)
                {
                    throw new InvalidOperationException($"OpenAPI operation {location} is null.");
                }

                var operationId = operationEntry.Value.OperationId;
                if (string.IsNullOrWhiteSpace(operationId))
                {
                    throw new InvalidOperationException(
                        $"OpenAPI operation {location} has no operationId.");
                }

                if (!seen.TryAdd(operationId, location))
                {
                    throw new InvalidOperationException(
                        $"Duplicate OpenAPI operationId '{operationId}' on {seen[operationId]} and {location}.");
                }
            }
        }
    }
}
