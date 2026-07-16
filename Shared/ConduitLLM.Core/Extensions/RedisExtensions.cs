using StackExchange.Redis;

namespace ConduitLLM.Core.Extensions;

/// <summary>
/// Extension methods for StackExchange.Redis types.
/// </summary>
public static class RedisExtensions
{
    /// <summary>
    /// Gets the primary Redis server from a multiplexer connection.
    /// Provides defensive checking against empty endpoint lists.
    /// </summary>
    public static IServer GetPrimaryServer(this IConnectionMultiplexer multiplexer)
    {
        var endpoints = multiplexer.GetEndPoints();
        if (endpoints.Length == 0)
            throw new InvalidOperationException("No Redis endpoints available.");
        return multiplexer.GetServer(endpoints[0]);
    }
}
