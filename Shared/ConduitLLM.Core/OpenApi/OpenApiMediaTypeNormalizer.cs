using Microsoft.OpenApi;

namespace ConduitLLM.Core.OpenApi;

public static class OpenApiMediaTypeNormalizer
{
    public static void RemoveLegacyAliases(IDictionary<string, OpenApiMediaType>? content)
    {
        content?.Remove("text/json");
        content?.Remove("text/plain");
        content?.Remove("application/*+json");
    }
}
