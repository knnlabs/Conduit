using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

namespace ConduitLLM.Gateway.Utilities;

/// <summary>
/// Resolves SignalR hub contexts by hub name. Shared by SignalRMessageBatcher and
/// SignalRMessageQueueService to avoid duplicated hub resolution logic.
/// </summary>
internal static class SignalRHubContextResolver
{
    // Cache the resolved IHubContext<>-closed Type per hub name. Type.GetType +
    // MakeGenericType is the expensive part and the result is process-wide invariant.
    // A sentinel marker is used to memoize misses without re-running reflection.
    private static readonly ConcurrentDictionary<string, Type?> _contextTypeCache = new();

    /// <summary>
    /// Resolves an <see cref="IHubContext{THub}"/> by hub class name from the Gateway assembly.
    /// </summary>
    public static IHubContext<Hub>? Resolve(IServiceProvider serviceProvider, string hubName)
    {
        var contextType = _contextTypeCache.GetOrAdd(hubName, static name =>
        {
            var hubType = Type.GetType($"ConduitLLM.Gateway.Hubs.{name}, ConduitLLM.Gateway");
            return hubType == null ? null : typeof(IHubContext<>).MakeGenericType(hubType);
        });

        return contextType == null ? null : serviceProvider.GetService(contextType) as IHubContext<Hub>;
    }
}
