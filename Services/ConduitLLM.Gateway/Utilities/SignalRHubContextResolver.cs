using Microsoft.AspNetCore.SignalR;

namespace ConduitLLM.Gateway.Utilities;

/// <summary>
/// Resolves SignalR hub contexts by hub name using reflection.
/// Shared by SignalRMessageBatcher and SignalRMessageQueueService
/// to avoid duplicated hub resolution logic.
/// </summary>
internal static class SignalRHubContextResolver
{
    /// <summary>
    /// Resolves an <see cref="IHubContext{THub}"/> by hub class name from the Gateway assembly.
    /// </summary>
    /// <param name="serviceProvider">The service provider to resolve from.</param>
    /// <param name="hubName">The hub class name (e.g., "MetricsHub").</param>
    /// <returns>The hub context, or null if the hub type cannot be found.</returns>
    public static IHubContext<Hub>? Resolve(IServiceProvider serviceProvider, string hubName)
    {
        var hubType = Type.GetType($"ConduitLLM.Gateway.Hubs.{hubName}, ConduitLLM.Gateway");
        if (hubType == null)
            return null;

        var contextType = typeof(IHubContext<>).MakeGenericType(hubType);
        return serviceProvider.GetService(contextType) as IHubContext<Hub>;
    }
}
