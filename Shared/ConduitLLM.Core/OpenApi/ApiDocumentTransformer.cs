using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ConduitLLM.Core.OpenApi;

/// <summary>Sets service metadata and registers its authentication scheme.</summary>
public abstract class ApiDocumentTransformer : IOpenApiDocumentTransformer
{
    private readonly string _title;
    private readonly string _description;
    private readonly string _securitySchemeName;
    private readonly OpenApiSecurityScheme _securityScheme;

    protected ApiDocumentTransformer(
        string title,
        string description,
        string securitySchemeName,
        OpenApiSecurityScheme securityScheme)
    {
        _title = title;
        _description = description;
        _securitySchemeName = securitySchemeName;
        _securityScheme = securityScheme;
    }

    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        document.Info.Title = _title;
        document.Info.Version = "v1";
        document.Info.Description = _description;

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??=
            new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[_securitySchemeName] = _securityScheme;

        return Task.CompletedTask;
    }
}
