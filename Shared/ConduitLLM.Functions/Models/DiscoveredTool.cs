using System.Text.Json.Nodes;

namespace ConduitLLM.Functions.Models;

/// <summary>
/// A single tool discovered at runtime from a dynamic tool provider (e.g. an MCP server).
/// </summary>
/// <remarks>
/// This is a transport-neutral description consumed by the discovery pipeline in
/// ConduitLLM.Core, which maps it to the LLM-facing <c>Tool</c> type. It intentionally avoids
/// referencing Core types so that ConduitLLM.Functions has no dependency on ConduitLLM.Core.
/// </remarks>
public sealed class DiscoveredTool
{
    /// <summary>
    /// The provider-native tool name (e.g. the MCP tool name as advertised by <c>tools/list</c>).
    /// This is the name passed back to the provider on execution.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Human-readable description of what the tool does, surfaced to the model.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// JSON Schema object describing the tool's input parameters, or null if the provider
    /// advertises no schema. Corresponds to the MCP tool <c>inputSchema</c>.
    /// </summary>
    public JsonObject? ParametersSchema { get; init; }
}
