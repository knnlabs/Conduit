using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ConduitLLM.Gateway.OpenApi;

/// <summary>Normalizes bodyless, binary, SSE, and otherwise untyped successful responses.</summary>
public sealed class ResponseContractOperationTransformer : IOpenApiOperationTransformer
{
    private static readonly IReadOnlyDictionary<string, string[]> BinaryContentTypes =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["Audio_CreateSpeech"] = ["audio/mpeg"],
            ["Downloads_DownloadFile"] = ["application/octet-stream"],
            ["Media_GetMedia"] = ["application/octet-stream"]
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
                BinaryContentTypes.TryGetValue(operation.OperationId, out var contentTypes))
            {
                response.Content = contentTypes.ToDictionary(
                    contentType => contentType,
                    _ => new OpenApiMediaType { Schema = BinarySchema() });
                continue;
            }

            EnsureJsonBody(response);

            if (responseEntry.Key == "200" && operation.OperationId == "Chat_CreateChatCompletion")
            {
                response.Content!["text/event-stream"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema { Type = JsonSchemaType.String }
                };
            }
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
