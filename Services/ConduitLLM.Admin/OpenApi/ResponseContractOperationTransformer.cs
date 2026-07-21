using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ConduitLLM.Admin.OpenApi;

/// <summary>Normalizes bodyless, file, and otherwise untyped successful responses.</summary>
public sealed class ResponseContractOperationTransformer : IOpenApiOperationTransformer
{
    private static readonly IReadOnlyDictionary<string, string[]> FileContentTypes =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["Analytics_ExportAnalytics"] = ["text/csv", "application/json"],
            ["BillingAudit_ExportAuditEvents"] = ["text/csv", "application/json"],
            ["ModelCosts_ExportCsv"] = ["text/csv"],
            ["ModelCosts_ExportJson"] = ["application/json"]
        };

    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        Normalize(operation, context.Description.HttpMethod);
        return Task.CompletedTask;
    }

    internal static void Normalize(OpenApiOperation operation, string? httpMethod)
    {
        if (operation.Responses is null) return;

        foreach (var responseEntry in operation.Responses)
        {
            if (!int.TryParse(responseEntry.Key, out var status) || status is < 200 or >= 300) continue;
            if (responseEntry.Value is not OpenApiResponse response) continue;

            if (status is 204 or 205 || string.Equals(httpMethod, "HEAD", StringComparison.OrdinalIgnoreCase))
            {
                response.Content = new Dictionary<string, OpenApiMediaType>();
                continue;
            }

            if (responseEntry.Key == "200" && operation.OperationId is not null &&
                FileContentTypes.TryGetValue(operation.OperationId, out var contentTypes))
            {
                response.Content = contentTypes.ToDictionary(
                    contentType => contentType,
                    _ => new OpenApiMediaType { Schema = BinarySchema() });
                continue;
            }

            EnsureJsonBody(response);
        }
    }

    private static void EnsureJsonBody(OpenApiResponse response)
    {
        if (response.Content?.Values.Any(media => media.Schema is not null) == true) return;
        response.Content = new Dictionary<string, OpenApiMediaType>
        {
            ["application/json"] = new() { Schema = new OpenApiSchema { Type = JsonSchemaType.Object } }
        };
    }

    private static OpenApiSchema BinarySchema() => new() { Type = JsonSchemaType.String, Format = "binary" };
}
