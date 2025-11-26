namespace ConduitLLM.Http.Constants;

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

    /// <summary>
    /// Key for storing the model name from image generation request (before provider mapping).
    /// Value type: string
    /// </summary>
    public const string ImageRequestModel = "ImageRequestModel";

    /// <summary>
    /// Key for storing the quality setting from image generation request.
    /// Value type: string (e.g., "standard", "hd")
    /// </summary>
    public const string ImageRequestQuality = "ImageRequestQuality";

    /// <summary>
    /// Key for storing the size/resolution from image generation request.
    /// Value type: string (e.g., "1024x1024", "1792x1024")
    /// </summary>
    public const string ImageRequestSize = "ImageRequestSize";

    /// <summary>
    /// Key for storing the number of images requested.
    /// Value type: int
    /// </summary>
    public const string ImageRequestN = "ImageRequestN";

    #region Video Request Keys

    /// <summary>
    /// Key for storing the model name from video generation request (before provider mapping).
    /// Value type: string
    /// </summary>
    public const string VideoRequestModel = "VideoRequestModel";

    /// <summary>
    /// Key for storing the size/resolution from video generation request.
    /// Value type: string (e.g., "1920x1080", "1280x720")
    /// </summary>
    public const string VideoRequestSize = "VideoRequestSize";

    /// <summary>
    /// Key for storing the duration from video generation request.
    /// Value type: int (seconds)
    /// </summary>
    public const string VideoRequestDuration = "VideoRequestDuration";

    /// <summary>
    /// Key for storing the FPS from video generation request.
    /// Value type: int
    /// </summary>
    public const string VideoRequestFps = "VideoRequestFps";

    /// <summary>
    /// Key for storing the style from video generation request.
    /// Value type: string
    /// </summary>
    public const string VideoRequestStyle = "VideoRequestStyle";

    /// <summary>
    /// Key for storing the number of videos requested.
    /// Value type: int
    /// </summary>
    public const string VideoRequestN = "VideoRequestN";

    /// <summary>
    /// Key for storing additional pricing parameters extracted from video generation request.
    /// Value type: Dictionary&lt;string, object&gt;
    /// </summary>
    public const string VideoRequestPricingParameters = "VideoRequestPricingParameters";

    #endregion
}
