using ConduitLLM.Configuration.DTOs.IpFilter;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ConduitLLM.Admin.OpenApi;

/// <summary>Marks fields that IP-filter response objects always emit as required.</summary>
public sealed class IpFilterResponseSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        var type = context.JsonTypeInfo.Type;
        if (type == typeof(IpFilterDto))
            Require(schema, "id", "filterType", "ipAddressOrCidr", "name", "isEnabled", "createdAt", "updatedAt");
        else if (type == typeof(IpFilterSettingsDto))
            Require(schema, "isEnabled", "defaultAllow", "bypassForAdminUi", "excludedEndpoints", "filterMode", "whitelistFilters", "blacklistFilters");
        else if (type == typeof(IpCheckResult))
            Require(schema, "isAllowed");

        return Task.CompletedTask;
    }

    private static void Require(OpenApiSchema schema, params string[] propertyNames)
    {
        schema.Required ??= new HashSet<string>();
        foreach (var propertyName in propertyNames)
            schema.Required.Add(propertyName);
    }
}
