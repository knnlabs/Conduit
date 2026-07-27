using ConduitLLM.Core.Exceptions;

namespace ConduitLLM.Core.Utilities;

/// <summary>Guards create-then-read workflows against impossible persistence results.</summary>
public static class ReadBackGuard
{
    public static T RequireCreated<T>(T? resource, string resourceType, object resourceId)
        where T : class =>
        resource ?? throw new ResourceConsistencyException(resourceType, resourceId);
}
