namespace ConduitLLM.Core.Models;

/// <summary>
/// Resolves an LLM-visible function name back to the function configuration that owns it and,
/// for dynamic (multi-tool) providers such as MCP, the provider-native tool name to invoke.
/// </summary>
/// <param name="ConfigurationId">The owning <c>FunctionConfiguration</c> id.</param>
/// <param name="ProviderToolName">
/// The provider-native tool name (e.g. the MCP <c>tools/list</c> name). Null for fixed-schema
/// providers (Exa, Tavily) where the configuration maps to exactly one implicit tool.
/// </param>
public readonly record struct FunctionRoute(int ConfigurationId, string? ProviderToolName = null);
