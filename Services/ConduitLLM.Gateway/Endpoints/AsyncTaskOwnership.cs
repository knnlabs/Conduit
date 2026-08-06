using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Gateway.Endpoints;

/// <summary>Central ownership check for every virtual-key task route.</summary>
internal static class AsyncTaskOwnership
{
    public static bool IsOwnedBy(AsyncTaskStatus? taskStatus, int virtualKeyId) =>
        taskStatus?.Metadata?.VirtualKeyId == virtualKeyId;
}
