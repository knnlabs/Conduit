using Microsoft.AspNetCore.Mvc;
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
            ["Downloads_Get"] = ["application/octet-stream"],
            ["Media_Get"] = ["application/octet-stream"]
        };

    private static readonly IReadOnlyDictionary<string, HashSet<string>> MvcJsonAliases =
        new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
        {
            ["Auth_GenerateEphemeralKey"] = ["200", "401", "500"],
            ["BatchOperations_StartSpendUpdates"] = ["202", "400", "401"],
            ["BatchOperations_StartVirtualKeyUpdates"] = ["202", "400", "401"],
            ["BatchOperations_StartWebhookSends"] = ["202", "400", "401"],
            ["BatchOperations_GetStatus"] = ["200", "404"],
            ["BatchOperations_Cancel"] = ["404", "409"],
            ["Chat_CreateCompletion"] = ["400", "500"],
            ["Embeddings_Create"] = ["200", "400", "500"],
            ["Functions_Execute"] = ["400", "404"],
            ["Functions_GetExecution"] = ["404"],
            ["ProviderModels_List"] = ["200", "404"],
            ["SignalRBatching_GetStatistics"] = ["200"],
            ["SignalRHealth_GetConnections"] = ["200"],
            ["SignalRHealth_GetQueue"] = ["200"],
            ["Videos_GenerateAsync"] = ["202", "400", "401", "403", "429", "500"],
            ["Videos_GetTaskStatus"] = ["200", "401", "404", "500"],
            ["Videos_RetryTask"] = ["200", "400", "401", "404", "500"],
            ["Videos_CancelTask"] = ["401", "404", "409", "500"]
        };

    private static readonly HashSet<string> MvcJsonRequestAliases =
    [
        "Audio_CreateSpeech", "Auth_GenerateEphemeralKey",
        "BatchOperations_StartSpendUpdates", "BatchOperations_StartVirtualKeyUpdates",
        "BatchOperations_StartWebhookSends", "Chat_CreateCompletion", "Downloads_GenerateUrl",
        "Embeddings_Create", "Functions_Execute", "Images_CreateImageAsync", "Images_Create",
        "Rerank_Create", "Videos_GenerateAsync"
    ];

    private static readonly HashSet<string> LegacyProblemDetailsResponses =
    [
        "BatchOperations_Cancel:404", "BatchOperations_Cancel:409",
        "BatchOperations_GetStatus:404", "BatchOperations_StartSpendUpdates:400",
        "BatchOperations_StartSpendUpdates:401", "BatchOperations_StartVirtualKeyUpdates:400",
        "BatchOperations_StartVirtualKeyUpdates:401", "BatchOperations_StartWebhookSends:400",
        "BatchOperations_StartWebhookSends:401", "Functions_Execute:400",
        "Functions_Execute:404", "Functions_GetExecution:404"
    ];

    public async Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        Normalize(operation, context.Description.HttpMethod);

        if (operation.OperationId is null || operation.Responses is null) return;
        if (operation.OperationId is "Audio_CreateTranscription" or "Media_Upload" &&
            operation.RequestBody?.Content?["multipart/form-data"].Schema is OpenApiSchema multipartSchema)
        {
            multipartSchema.Required?.Clear();
            if (operation.OperationId == "Media_Upload")
            {
                operation.RequestBody.Description = "Optional media type (image/video/audio).";
            }
        }
        OpenApiSchema? problemDetailsSchema = null;
        foreach (var responseEntry in operation.Responses)
        {
            if (!LegacyProblemDetailsResponses.Contains($"{operation.OperationId}:{responseEntry.Key}") ||
                responseEntry.Value is not OpenApiResponse response ||
                response.Content is null)
            {
                continue;
            }

            if (problemDetailsSchema is null)
            {
                problemDetailsSchema = await context.GetOrCreateSchemaAsync(
                    typeof(ProblemDetails),
                    parameterDescription: null,
                    cancellationToken);
                var document = context.Document ?? throw new InvalidOperationException("An OpenAPI document is required.");
                var components = document.Components ??= new OpenApiComponents();
                components.Schemas ??= new Dictionary<string, IOpenApiSchema>();
                components.Schemas["ProblemDetails"] = problemDetailsSchema;
            }
            var problemDetailsReference = new OpenApiSchemaReference(
                "ProblemDetails",
                context.Document ?? throw new InvalidOperationException("An OpenAPI document is required."));
            foreach (var media in response.Content.Values)
            {
                media.Schema = problemDetailsReference;
            }
        }
    }

    internal static void Normalize(OpenApiOperation operation, string? httpMethod)
    {
        if (operation.OperationId is not null &&
            MvcJsonRequestAliases.Contains(operation.OperationId) &&
            operation.RequestBody?.Content is not null &&
            operation.RequestBody.Content.TryGetValue("application/json", out var requestJson))
        {
            operation.RequestBody.Content["text/json"] = new OpenApiMediaType { Schema = requestJson.Schema };
            operation.RequestBody.Content["application/*+json"] = new OpenApiMediaType { Schema = requestJson.Schema };
        }

        if (operation.Responses is null) return;

        foreach (var responseEntry in operation.Responses)
        {
            if (responseEntry.Value is not OpenApiResponse response) continue;

            if (operation.OperationId is not null &&
                MvcJsonAliases.TryGetValue(operation.OperationId, out var legacyStatuses) &&
                legacyStatuses.Contains(responseEntry.Key) &&
                response.Content is not null &&
                response.Content.TryGetValue("application/json", out var legacyJsonMedia))
            {
                response.Content["text/json"] = new OpenApiMediaType { Schema = legacyJsonMedia.Schema };
                response.Content["text/plain"] = new OpenApiMediaType { Schema = legacyJsonMedia.Schema };
            }

            if (!int.TryParse(responseEntry.Key, out var status) || status is < 200 or >= 300) continue;

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
                    _ => new OpenApiMediaType { Schema = BinarySchema() });
                continue;
            }

            EnsureJsonBody(response, operation.OperationId);

            if (responseEntry.Key == "200" && operation.OperationId == "Chat_CreateCompletion")
            {
                response.Content!["text/event-stream"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema { Type = JsonSchemaType.String }
                };
            }
        }
    }

    private static void EnsureJsonBody(OpenApiResponse response, string? operationId)
    {
        if (response.Content?.Values.Any(media => media.Schema is not null) == true)
        {
            foreach (var media in response.Content.Values)
            {
                if (!string.Equals(operationId, "Models_ListModels", StringComparison.Ordinal) &&
                    !string.Equals(operationId, "Models_GetModelMetadata", StringComparison.Ordinal) &&
                    media.Schema is OpenApiSchema { Type: null } schema)
                {
                    schema.Type = JsonSchemaType.Object;
                }
            }
            return;
        }
        response.Content = new Dictionary<string, OpenApiMediaType>
        {
            ["application/json"] = new() { Schema = new OpenApiSchema { Type = JsonSchemaType.Object } }
        };
    }

    private static OpenApiSchema BinarySchema() => new() { Type = JsonSchemaType.String, Format = "binary" };
}
