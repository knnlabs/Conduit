using ConduitLLM.Functions.Entities;

namespace ConduitLLM.Functions.Services;

/// <summary>
/// Selects which credential to use for a given function configuration.
/// </summary>
/// <remarks>
/// Config-scoped credentials (<see cref="FunctionCredential.FunctionConfigurationId"/> set — used by
/// MCP, where each server has its own token) take precedence over provider-global credentials
/// (null — the Exa/Tavily model). Within each tier the primary credential is preferred, then any
/// enabled one.
/// </remarks>
public static class CredentialSelector
{
    /// <summary>
    /// Returns the best enabled credential for the configuration, or null when none is enabled.
    /// </summary>
    public static FunctionCredential? SelectForConfiguration(
        IEnumerable<FunctionCredential> credentials,
        int functionConfigurationId)
    {
        var enabled = credentials.Where(c => c.IsEnabled).ToList();

        var scoped = enabled.Where(c => c.FunctionConfigurationId == functionConfigurationId).ToList();
        if (scoped.Count > 0)
        {
            return scoped.FirstOrDefault(c => c.IsPrimary) ?? scoped[0];
        }

        var global = enabled.Where(c => c.FunctionConfigurationId == null).ToList();
        if (global.Count > 0)
        {
            return global.FirstOrDefault(c => c.IsPrimary) ?? global[0];
        }

        return null;
    }
}
