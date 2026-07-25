using StackExchange.Redis;

using Xunit;

namespace ConduitLLM.Tests.Core.Services;

/// <summary>
/// Shared gate for tests that need a real Redis. Enforces the difference between
/// "no Redis here, skip" and "Redis was promised and is missing, fail" — the rate limiter's
/// Lua path had no executed coverage precisely because every Redis-less run reported green.
/// </summary>
/// <remarks>
/// <para>
/// CI supplies Redis as a service container and sets <c>TEST_REDIS_CONNECTION</c>. When that
/// variable is present, Redis is treated as mandatory and an unreachable server fails the test
/// rather than skipping it. On a developer machine with the variable unset, the tests skip.
/// </para>
/// <para>
/// <see cref="RedisRequirementTests"/> closes the remaining hole: it fails if CI itself ever
/// stops providing Redis, so the suite cannot quietly stop covering the limiter.
/// </para>
/// </remarks>
public static class RedisTestServer
{
    private const string ConnectionVariable = "TEST_REDIS_CONNECTION";
    // 127.0.0.1 rather than "localhost": on Windows that name resolves to ::1 first, and a
    // server listening only on IPv4 is then reported as unreachable.
    private const string DefaultConnection = "127.0.0.1:6379,allowAdmin=true";

    private static readonly Lazy<ConnectionMultiplexer?> Connection = new(Connect, isThreadSafe: true);

    /// <summary>True when the environment declares that a Redis server must be reachable.</summary>
    public static bool IsRequired => !string.IsNullOrWhiteSpace(ConfiguredConnection);

    /// <summary>True when this process is running inside GitHub Actions.</summary>
    public static bool IsCi =>
        string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase);

    public static string? ConfiguredConnection => Environment.GetEnvironmentVariable(ConnectionVariable);

    public static bool IsAvailable => Connection.Value is { IsConnected: true };

    /// <summary>
    /// Returns a connected multiplexer, skipping the calling test when Redis is optional and
    /// absent. Throws when Redis was declared mandatory but is unreachable.
    /// </summary>
    public static IConnectionMultiplexer Require()
    {
        if (IsAvailable)
        {
            return Connection.Value!;
        }

        if (IsRequired)
        {
            throw new InvalidOperationException(
                $"{ConnectionVariable}={ConfiguredConnection} declares Redis as mandatory for this run, " +
                "but no server could be reached. The rate limiter's Lua path is not being covered.");
        }

        Skip.If(true,
            $"Redis is not available. Set {ConnectionVariable} (CI does) or run a local server to execute these tests.");
        throw new InvalidOperationException("unreachable");
    }

    /// <summary>
    /// Prefix for keys created by a single test, so parallel tests and repeat runs cannot
    /// observe each other's windows.
    /// </summary>
    public static string NewKeyPrefix(string name) => $"test:{name}:{Guid.NewGuid():N}:";

    /// <summary>Deletes every key created under a prefix, including the :sum companions.</summary>
    public static async Task CleanupAsync(string keyPrefix)
    {
        if (!IsAvailable)
        {
            return;
        }

        var multiplexer = Connection.Value!;
        foreach (var endpoint in multiplexer.GetEndPoints())
        {
            var server = multiplexer.GetServer(endpoint);
            if (!server.IsConnected || server.IsReplica)
            {
                continue;
            }

            foreach (var key in server.Keys(pattern: $"{keyPrefix}*"))
            {
                await multiplexer.GetDatabase().KeyDeleteAsync(key);
            }
        }
    }

    private static ConnectionMultiplexer? Connect()
    {
        try
        {
            var options = ConfigurationOptions.Parse(ConfiguredConnection ?? DefaultConnection);
            options.ConnectTimeout = 2000;
            options.SyncTimeout = 2000;
            options.AbortOnConnectFail = false;
            options.AllowAdmin = true;

            var multiplexer = ConnectionMultiplexer.Connect(options);
            multiplexer.GetDatabase().Ping();
            return multiplexer;
        }
        catch (Exception)
        {
            return null;
        }
    }
}

/// <summary>
/// Guards the guard: if CI ever stops supplying Redis, this fails instead of letting every
/// Redis-backed limiter test silently skip.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "TestInfrastructure")]
public class RedisRequirementTests
{
    [Fact]
    public void ContinuousIntegration_MustProvideRedisForLimiterCoverage()
    {
        if (!RedisTestServer.IsCi)
        {
            return;
        }

        Assert.True(
            RedisTestServer.IsRequired,
            "CI must set TEST_REDIS_CONNECTION so the Redis-backed rate limiter tests execute " +
            "instead of skipping. See the redis service in .github/workflows/ci.yml.");

        Assert.True(
            RedisTestServer.IsAvailable,
            $"TEST_REDIS_CONNECTION={RedisTestServer.ConfiguredConnection} is set but no server answered.");
    }
}
