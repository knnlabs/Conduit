namespace ConduitLLM.Gateway.Constants;

/// <summary>
/// Constants for HttpContext.Items keys used to pass data between
/// controllers and middleware.
/// </summary>
public static class HttpContextKeys
{
    /// <summary>Authoritative provider usage for non-streaming chat responses.</summary>
    public const string NonStreamingUsage = "NonStreamingUsage";

    public const string PromptCachingEligible = "PromptCachingEligible";
    public const string ModelProviderMappingId = "ModelProviderMappingId";
    public const string PromptCachingPolicyApplied = "PromptCachingPolicyApplied";
    public const string CachedReadSavings = "CachedReadSavings";
    public const string CacheWritePremium = "CacheWritePremium";
    public const string RoutingAffinityUsed = "RoutingAffinityUsed";
    public const string RoutingDecisionReason = "RoutingDecisionReason";
    public const string RoutingFailoverCount = "RoutingFailoverCount";

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
    /// Key for server-only per-provider-call usage from an agentic chat request.
    /// Value type: List&lt;ProviderCallUsage&gt;
    /// </summary>
    public const string ChatProviderCalls = "ChatProviderCalls";

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

    /// <summary>
    /// Key for storing the provider billing policy (whether provider-reported cost is authoritative
    /// and the markup to apply). Stamped by the controller from the resolved provider before the
    /// request runs, and applied onto the Usage by UsageTrackingMiddleware before cost calculation.
    /// Value type: ConduitLLM.Core.Models.ProviderCostBillingPolicy
    /// </summary>
    public const string ProviderBillingPolicy = "ProviderBillingPolicy";

    /// <summary>
    /// Key for storing the provider-reported cost (USD) for the request. Stashed by the controller
    /// from the response Usage on non-streaming/media paths (the value is never serialized into the
    /// client-facing body). On streaming, the cost travels on the StreamingUsage object instead.
    /// Value type: decimal
    /// </summary>
    public const string ProviderReportedCost = "ProviderReportedCost";

    // NOTE: Image and video request-shape data (model, size, quality, duration, fps, style,
    // N, pricing parameters) is no longer carried via string keys. It now flows through the
    // typed ConduitLLM.Gateway.Usage.IUsageContext set by ImagesController/VideosController
    // and consumed by UsageTrackingMiddleware.
}
