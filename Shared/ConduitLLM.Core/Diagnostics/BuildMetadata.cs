using System.Reflection;

namespace ConduitLLM.Core.Diagnostics;

/// <summary>
/// Immutable identity embedded in each service assembly at build time.
/// </summary>
public sealed record BuildMetadata(string Version, string CommitSha, string BuildTimestamp)
{
    /// <summary>Reads build identity from an assembly, using safe local-build fallbacks.</summary>
    public static BuildMetadata FromAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        var version = informationalVersion?.Split('+', 2)[0]
            ?? assembly.GetName().Version?.ToString()
            ?? "dev";
        var metadata = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);

        return new BuildMetadata(
            string.IsNullOrWhiteSpace(version) ? "dev" : version,
            GetValue(metadata, "ConduitCommitSha", "dev"),
            GetValue(metadata, "ConduitBuildTimestamp", "unknown"));
    }

    private static string GetValue(
        IReadOnlyDictionary<string, string?> metadata,
        string key,
        string fallback) =>
        metadata.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : fallback;
}
