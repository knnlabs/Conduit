using ConduitLLM.Core.Models;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ConduitLLM.Gateway.OpenApi;

/// <summary>Normalizes Gateway media types, errors, binary bodies, and request IDs.</summary>
public sealed class ResponseContractOperationTransformer : IOpenApiOperationTransformer
{
    private static readonly IReadOnlyDictionary<string, string[]> BinaryContentTypes =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["Audio_CreateSpeech"] = ["audio/mpeg"],
            ["Downloads_Get"] = ["application/octet-stream"],
            ["Media_Get"] = ["application/octet-stream"]
        };

    public async Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        Normalize(operation, context.Description.HttpMethod);

        if (operation.OperationId is "Audio_CreateTranscription" or "Media_Upload" &&
            operation.RequestBody?.Content?["multipart/form-data"].Schema is OpenApiSchema multipartSchema)
        {
            multipartSchema.Required?.Clear();
            if (operation.OperationId == "Media_Upload")
            {
                operation.RequestBody.Description = "Optional media type (image/video/audio).";
            }
        }

        if (operation.Responses is null)
        {
            return;
        }

        var schema = await context.GetOrCreateSchemaAsync(
            typeof(OpenAIErrorResponse),
            parameterDescription: null,
            cancellationToken);
        var document = context.Document ?? throw new InvalidOperationException("An OpenAPI document is required.");
        var components = document.Components ??= new OpenApiComponents();
        components.Schemas ??= new Dictionary<string, IOpenApiSchema>();
        components.Schemas["OpenAIErrorResponse"] = schema;
        var errorReference = new OpenApiSchemaReference("OpenAIErrorResponse", document);

        foreach (var responseEntry in operation.Responses)
        {
            if (responseEntry.Value is not OpenApiResponse response)
            {
                continue;
            }

            response.Headers ??= new Dictionary<string, IOpenApiHeader>();
            response.Headers["x-request-id"] = new OpenApiHeader
            {
                Description = "Request identifier for support and distributed tracing.",
                Schema = new OpenApiSchema { Type = JsonSchemaType.String }
            };

            if (int.TryParse(responseEntry.Key, out var status) && status >= 400)
            {
                response.Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new() { Schema = errorReference }
                };
            }
        }
    }

    internal static void Normalize(OpenApiOperation operation, string? httpMethod)
    {
        RemoveLegacyAliases(operation.RequestBody?.Content);
        if (operation.Responses is null)
        {
            return;
        }

        foreach (var responseEntry in operation.Responses)
        {
            if (responseEntry.Value is not OpenApiResponse response)
            {
                continue;
            }

            RemoveLegacyAliases(response.Content);
            if (!int.TryParse(responseEntry.Key, out var status) || status is < 200 or >= 300)
            {
                continue;
            }

            if (status is 204 or 205 || string.Equals(httpMethod, "HEAD", StringComparison.OrdinalIgnoreCase))
            {
                response.Content = new Dictionary<string, OpenApiMediaType>();
                continue;
            }

            if (operation.OperationId is not null &&
                BinaryContentTypes.TryGetValue(operation.OperationId, out var contentTypes))
            {
                response.Content = contentTypes.ToDictionary(
                    contentType => contentType,
                    _ => new OpenApiMediaType
                    {
                        Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" }
                    });
                continue;
            }

            if (responseEntry.Key == "200" && operation.OperationId == "Chat_CreateCompletion")
            {
                response.Content!["text/event-stream"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema { Type = JsonSchemaType.String }
                };
            }
        }
    }

    private static void RemoveLegacyAliases(IDictionary<string, OpenApiMediaType>? content)
    {
        content?.Remove("text/json");
        content?.Remove("text/plain");
        content?.Remove("application/*+json");
    }
}
