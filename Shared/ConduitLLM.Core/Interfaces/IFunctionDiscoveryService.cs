using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Interfaces;

/// <summary>
/// Service for discovering and converting function configurations into LLM-compatible Tool definitions.
/// Handles validation, access control, and schema transformation.
/// </summary>
public interface IFunctionDiscoveryService
{
    /// <summary>
    /// Loads function configurations and converts them to Tool definitions for LLM use.
    /// Validates that functions are enabled and accessible to the given virtual key.
    /// </summary>
    /// <param name="functionConfigurationIds">List of function configuration IDs to load</param>
    /// <param name="virtualKeyId">Virtual key making the request (for access control)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of Tool definitions ready to be injected into chat requests</returns>
    /// <exception cref="ArgumentException">If any configuration IDs are invalid or inaccessible</exception>
    Task<List<Tool>> GetToolsForFunctionConfigurationsAsync(
        List<int> functionConfigurationIds,
        int virtualKeyId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a mapping from function name to function configuration ID.
    /// Used to resolve function calls from LLM responses back to configuration IDs.
    /// </summary>
    /// <param name="functionConfigurationIds">List of function configuration IDs</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>
    /// Dictionary mapping each LLM-facing function name to a <see cref="FunctionRoute"/> (owning
    /// configuration id and, for dynamic multi-tool providers such as MCP, the provider-native tool
    /// name to invoke).
    /// </returns>
    Task<Dictionary<string, FunctionRoute>> GetFunctionNameToIdMappingAsync(
        List<int> functionConfigurationIds,
        CancellationToken cancellationToken = default);
}
