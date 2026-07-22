using ConduitLLM.Functions.Interfaces;
using ConduitLLM.Functions.Models;

namespace ConduitLLM.Functions.Providers.Mcp;

/// <summary>
/// Usage calculation for <see cref="McpFunctionClient"/>.
/// </summary>
public sealed partial class McpFunctionClient
{
    /// <inheritdoc />
    /// <remarks>
    /// MCP tool calls carry no standard cost signal, so the only reliable billing dimensions are
    /// "one invocation" and elapsed time. Priced via a flat-rate / time-based <c>FunctionCost</c>
    /// (default 0). The response size is surfaced as metadata for observability.
    /// </remarks>
    public FunctionExecutionUsage CalculateUsageFromResponse(
        Dictionary<string, object> parameters,
        FunctionExecutionResult result)
    {
        return new FunctionExecutionUsage
        {
            ResultCount = result.IsSuccess ? 1 : 0,
            ExecutionDuration = result.Duration,
            Metadata = new Dictionary<string, object>
            {
                ["responseChars"] = result.ResponseJson?.Length ?? 0
            }
        };
    }
}
