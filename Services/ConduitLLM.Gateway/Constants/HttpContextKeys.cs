namespace ConduitLLM.Gateway.Constants;

/// <summary>
/// Constants for HttpContext.Items keys used to pass data between
/// controllers and middleware.
/// </summary>
public static class HttpContextKeys
{
    /// <summary>
    /// Key for storing the virtual key ID extracted from authentication.
    /// </summary>
    public const string VirtualKeyId = "VirtualKeyId";

    /// <summary>
    /// Key for storing accumulated function call summaries from agentic chat completions.
    /// Value type: List&lt;FunctionCallSummary&gt;
    /// </summary>
    public const string ChatFunctionCalls = "ChatFunctionCalls";

    /// <summary>
    /// Key for storing the total function cost from agentic chat completions.
    /// Value type: decimal
    /// </summary>
    public const string ChatFunctionCost = "ChatFunctionCost";

    /// <summary>
    /// Key for storing the total iteration count from agentic chat completions.
    /// Value type: int
    /// </summary>
    public const string ChatAgenticIterations = "ChatAgenticIterations";

    /// <summary>
    /// Key for storing function configuration ID (used by FunctionsController).
    /// </summary>
    public const string FunctionConfigurationId = "FunctionConfigurationId";

    /// <summary>
    /// Key for storing function configuration name (used by FunctionsController).
    /// </summary>
    public const string FunctionConfigurationName = "FunctionConfigurationName";

    /// <summary>
    /// Key for storing function execution ID (used by FunctionsController).
    /// </summary>
    public const string FunctionExecutionId = "FunctionExecutionId";

    /// <summary>
    /// Key for storing the request start time for latency calculation.
    /// </summary>
    public const string RequestStartTime = "RequestStartTime";

    /// <summary>
    /// Key for storing the ModelCost ID for cost calculation.
    /// This is the preferred lookup key as it uses direct ID matching instead of string matching.
    /// Value type: int?
    /// </summary>
    public const string ModelCostId = "ModelCostId";

    // NOTE: Image and video request-shape data (model, size, quality, duration, fps, style,
    // N, pricing parameters) is no longer carried via string keys. It now flows through the
    // typed ConduitLLM.Gateway.Usage.IUsageContext set by ImagesController/VideosController
    // and consumed by UsageTrackingMiddleware.
}
