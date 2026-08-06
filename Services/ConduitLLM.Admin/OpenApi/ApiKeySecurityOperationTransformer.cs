using ConduitLLM.Core.OpenApi;

namespace ConduitLLM.Admin.OpenApi;

/// <summary>
/// Publishes the Admin API's security requirement from endpoint authorization metadata.
/// </summary>
public sealed class ApiKeySecurityOperationTransformer : AuthorizedOperationSecurityTransformer
{
    public const string SecuritySchemeName = "MasterKey";

    public ApiKeySecurityOperationTransformer()
        : base(SecuritySchemeName)
    {
    }
}
