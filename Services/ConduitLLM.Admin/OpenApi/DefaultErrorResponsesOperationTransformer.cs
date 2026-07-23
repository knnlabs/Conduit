using ConduitLLM.Admin.DTOs;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ConduitLLM.Admin.OpenApi;

/// <summary>Documents the canonical RFC 9457 error body and request ID on every response.</summary>
public sealed class DefaultErrorResponsesOperationTransformer : IOpenApiOperationTransformer
{
    public async Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        operation.Responses ??= new OpenApiResponses();
        operation.Responses.TryAdd("500", new OpenApiResponse { Description = "Internal Server Error" });

        var schema = await context.GetOrCreateSchemaAsync(
            typeof(AdminProblemDetails),
            parameterDescription: null,
            cancellationToken);
        var document = context.Document ?? throw new InvalidOperationException("An OpenAPI document is required.");
        var components = document.Components ??= new OpenApiComponents();
        components.Schemas ??= new Dictionary<string, IOpenApiSchema>();
        components.Schemas["AdminProblemDetails"] = schema;
        var problemReference = new OpenApiSchemaReference("AdminProblemDetails", document);

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
                    ["application/problem+json"] = new() { Schema = problemReference }
                };
            }
        }
    }
}
