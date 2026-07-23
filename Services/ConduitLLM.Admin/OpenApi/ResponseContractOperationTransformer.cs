using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ConduitLLM.Admin.OpenApi;

/// <summary>Normalizes bodyless, file, and otherwise untyped successful responses.</summary>
public sealed class ResponseContractOperationTransformer : IOpenApiOperationTransformer
{
    private static readonly HashSet<string> LegacyResponseFormatterPrefixes =
    [
        "Analytics", "BillingAudit", "FunctionConfigurations", "HealthMonitoring",
        "IpFilter", "Media", "MediaCleanup", "MediaRetention", "Model", "ModelCosts",
        "ModelProviderMapping", "Pricing", "ProviderCredentials", "ProviderErrors",
        "ProviderTools", "SecurityMonitoring", "VirtualKeyGroups", "VirtualKeys"
    ];

    private static readonly HashSet<string> LegacyRequestFormatterPrefixes =
    [
        "BillingAudit", "FunctionConfigurations", "IpFilter", "Media", "MediaCleanup",
        "MediaRetention", "Model", "ModelCosts", "ModelProviderMapping", "Pricing",
        "ProviderCredentials", "ProviderErrors", "ProviderTools", "VirtualKeyGroups",
        "VirtualKeys"
    ];

    private static readonly HashSet<string> JsonOnlyResponses =
    [
        "Analytics_ExportAnalytics:200",
        "Analytics_InvalidateCache:200",
        "BillingAudit_ExportAuditEvents:200",
        "MediaRetention_AssignPolicyToGroup:200",
        "MediaRetention_SetDefaultPolicy:200",
        "ModelCosts_ExportJson:200"
    ];

    private static readonly HashSet<string> BodylessClientErrors =
    [
        "ModelAuthors_Create:400",
        "ModelAuthors_Update:400",
        "ModelAuthors_Update:404",
        "ModelAuthors_Delete:400",
        "ModelAuthors_Delete:404",
        "Auth_GenerateEphemeralMasterKey:401",
        "Configuration_GetAliasRouting:404",
        "Configuration_UpdateAliasRouting:404"
    ];

    private static readonly HashSet<string> LegacyProblemDetailsOverrides =
    [
        "Media_Search:400",
        "Media_Prune:400"
    ];

    private static readonly IReadOnlyDictionary<string, HashSet<string>> LegacyOptionalQueryParameters =
        new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
        {
            ["Media_Search"] = ["pattern"],
            ["Model_Search"] = ["query"],
            ["BillingAudit_GetSummary"] = ["from", "to"],
            ["BillingAudit_DetectAnomalies"] = ["from", "to"],
            ["BillingAudit_GetRevenueLoss"] = ["from", "to"],
            ["Pricing_GetAuditSummary"] = ["from", "to"],
            ["ModelCosts_GetOverview"] = ["startDate", "endDate"]
        };

    private static readonly IReadOnlyDictionary<string, string[]> FileContentTypes =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["Analytics_ExportAnalytics"] = ["text/csv", "application/json"],
            ["BillingAudit_ExportAuditEvents"] = ["text/csv", "application/json"],
            ["ModelCosts_ExportCsv"] = ["text/csv"],
            ["ModelCosts_ExportJson"] = ["application/json"]
        };

    public async Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        Normalize(operation, context.Description.HttpMethod);
        await RestoreLegacyFormatterMetadataAsync(operation, context, cancellationToken);
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

        }
    }

    private static OpenApiSchema BinarySchema() => new() { Type = JsonSchemaType.String, Format = "binary" };

    private static async Task RestoreLegacyFormatterMetadataAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (operation.OperationId is null) return;

        if (LegacyOptionalQueryParameters.TryGetValue(operation.OperationId, out var optionalParameters) &&
            operation.Parameters is not null)
        {
            foreach (var parameter in operation.Parameters.OfType<OpenApiParameter>())
            {
                if (parameter.Name is null || !optionalParameters.Contains(parameter.Name)) continue;
                parameter.Required = false;
                if (parameter.Schema is OpenApiSchema schema) schema.Type = JsonSchemaType.String;
            }
        }

        if (operation.OperationId is "ModelCosts_ImportCsv" or "ModelCosts_ImportJson" &&
            operation.RequestBody?.Content?["multipart/form-data"].Schema is OpenApiSchema importSchema)
        {
            importSchema.Required?.Clear();
            operation.RequestBody.Description = operation.OperationId == "ModelCosts_ImportCsv"
                ? "CSV file containing model costs"
                : "JSON file containing model costs";
        }

        var separator = operation.OperationId.IndexOf('_');
        var prefix = separator > 0 ? operation.OperationId[..separator] : operation.OperationId;

        if (operation.RequestBody?.Content is { } requestContent &&
            LegacyRequestFormatterPrefixes.Contains(prefix) &&
            requestContent.TryGetValue("application/json", out var requestJson))
        {
            requestContent.TryAdd("text/json", requestJson);
            requestContent.TryAdd("application/*+json", requestJson);
        }

        if (operation.Responses is null) return;

        OpenApiSchema? problemDetailsSchema = null;
        foreach (var responseEntry in operation.Responses)
        {
            if (responseEntry.Value is not OpenApiResponse response) continue;
            var responseKey = $"{operation.OperationId}:{responseEntry.Key}";

            var needsProblemDetails =
                (response.Content is { Count: 0 } &&
                 responseEntry.Key is "400" or "401" or "403" or "404" or "409" &&
                 !BodylessClientErrors.Contains(responseKey)) ||
                LegacyProblemDetailsOverrides.Contains(responseKey);

            if (needsProblemDetails)
            {
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
                if (response.Content is { Count: > 0 })
                {
                    foreach (var media in response.Content.Values) media.Schema = problemDetailsReference;
                }
                else
                {
                    var media = new OpenApiMediaType { Schema = problemDetailsReference };
                    response.Content = new Dictionary<string, OpenApiMediaType>
                    {
                        ["application/json"] = media,
                        ["text/json"] = media,
                        ["text/plain"] = media
                    };
                }
                continue;
            }

            if (responseEntry.Key != "500" &&
                LegacyResponseFormatterPrefixes.Contains(prefix) &&
                !JsonOnlyResponses.Contains(responseKey) &&
                response.Content?.TryGetValue("application/json", out var responseJson) == true)
            {
                response.Content.TryAdd("text/json", responseJson);
                response.Content.TryAdd("text/plain", responseJson);
            }
        }
    }
}
