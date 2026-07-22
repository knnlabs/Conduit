namespace ConduitLLM.Functions.Models;

/// <summary>
/// Reserved keys the function-execution layer injects into the parameter dictionary to carry
/// routing information that does not fit the fixed <c>IFunctionClient.ExecuteAsync</c> signature.
/// </summary>
/// <remarks>
/// Because one MCP <c>FunctionConfiguration</c> exposes many tools, the specific tool to invoke is
/// a per-call value rather than a per-configuration one. The execution service places it under
/// <see cref="ToolName"/>; <c>McpFunctionClient</c> reads and strips it before calling the server.
/// The double-underscore prefix keeps it from colliding with model-supplied arguments.
/// </remarks>
public static class McpReservedParameterKeys
{
    /// <summary>Parameter key carrying the MCP tool name to invoke.</summary>
    public const string ToolName = "__conduit_mcp_tool";
}
