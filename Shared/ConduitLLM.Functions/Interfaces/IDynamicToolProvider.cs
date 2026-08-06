using ConduitLLM.Functions.Models;

namespace ConduitLLM.Functions.Interfaces;

/// <summary>
/// Optional capability implemented by function clients whose tool set is discovered
/// dynamically at runtime rather than defined by a single static parameter schema.
/// </summary>
/// <remarks>
/// The fixed-schema providers (Exa, Tavily) expose exactly one tool whose JSON schema is
/// stored on the <see cref="Entities.FunctionConfiguration.ParameterSchema"/> column. An MCP
/// server instead advertises many tools via <c>tools/list</c>. A client that implements this
/// interface is asked, during function discovery, to enumerate its tools so that a single
/// <see cref="Entities.FunctionConfiguration"/> can expand into multiple LLM-visible tools.
/// Clients that do not implement this interface keep the legacy one-tool-per-configuration
/// behavior unchanged.
/// </remarks>
public interface IDynamicToolProvider
{
    /// <summary>
    /// Enumerates the tools this provider currently exposes.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The discovered tools, already filtered by any configured allowlist.</returns>
    Task<IReadOnlyList<DiscoveredTool>> ListToolsAsync(CancellationToken cancellationToken = default);
}
