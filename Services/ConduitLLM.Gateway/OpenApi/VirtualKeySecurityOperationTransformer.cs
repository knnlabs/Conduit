using ConduitLLM.Core.OpenApi;

namespace ConduitLLM.Gateway.OpenApi;

/// <summary>
/// Publishes the Gateway's virtual-key requirement from endpoint authorization metadata.
/// </summary>
public sealed class VirtualKeySecurityOperationTransformer : AuthorizedOperationSecurityTransformer
{
    public const string SecuritySchemeName = "VirtualKey";

    public VirtualKeySecurityOperationTransformer()
        : base(SecuritySchemeName)
    {
    }
}
