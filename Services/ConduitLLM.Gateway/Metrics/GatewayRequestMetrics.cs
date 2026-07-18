using System.Diagnostics;

namespace ConduitLLM.Gateway.Metrics;

/// <summary>
/// Distributed tracing activity source for Gateway API request processing.
/// Provides spans for chat completions, image/video generation, embeddings,
/// and authentication flows to enable end-to-end trace visualization.
/// </summary>
public static class GatewayRequestMetrics
{
    /// <summary>
    /// Activity source for Gateway request processing spans.
    /// </summary>
    public static readonly ActivitySource ActivitySource = new("ConduitLLM.Gateway.Requests", "1.0.0");

    /// <summary>
    /// Starts a span for a chat completion request.
    /// </summary>
    public static Activity? StartChatCompletionActivity(string model, bool isStreaming)
    {
        return ActivitySource.StartActivity("gateway.chat.completion", ActivityKind.Server,
            Activity.Current?.Context ?? default, new TagList
            {
                { "gateway.model", model },
                { "gateway.streaming", isStreaming },
                { "gateway.operation", "chat_completion" }
            });
    }

    /// <summary>
    /// Starts a span for an image generation request.
    /// </summary>
    public static Activity? StartImageGenerationActivity(string model, bool isAsync)
    {
        return ActivitySource.StartActivity("gateway.image.generation", ActivityKind.Server,
            Activity.Current?.Context ?? default, new TagList
            {
                { "gateway.model", model },
                { "gateway.async", isAsync },
                { "gateway.operation", "image_generation" }
            });
    }

    /// <summary>
    /// Starts a span for a video generation request.
    /// </summary>
    public static Activity? StartVideoGenerationActivity(string model)
    {
        return ActivitySource.StartActivity("gateway.video.generation", ActivityKind.Server,
            Activity.Current?.Context ?? default, new TagList
            {
                { "gateway.model", model },
                { "gateway.operation", "video_generation" }
            });
    }

    /// <summary>
    /// Starts a span for an embeddings request.
    /// </summary>
    public static Activity? StartEmbeddingsActivity(string model)
    {
        return ActivitySource.StartActivity("gateway.embeddings", ActivityKind.Server,
            Activity.Current?.Context ?? default, new TagList
            {
                { "gateway.model", model },
                { "gateway.operation", "embeddings" }
            });
    }

    /// <summary>
    /// Starts a span for virtual key authentication.
    /// </summary>
    public static Activity? StartAuthenticationActivity(string authType)
    {
        return ActivitySource.StartActivity("gateway.auth.validate", ActivityKind.Internal,
            Activity.Current?.Context ?? default, new TagList
            {
                { "gateway.auth_type", authType },
                { "gateway.operation", "authentication" }
            });
    }

    /// <summary>
    /// Starts a span for usage tracking / billing.
    /// </summary>
    public static Activity? StartUsageTrackingActivity(string endpointType)
    {
        return ActivitySource.StartActivity("gateway.usage.tracking", ActivityKind.Internal,
            Activity.Current?.Context ?? default, new TagList
            {
                { "gateway.endpoint_type", endpointType },
                { "gateway.operation", "usage_tracking" }
            });
    }
}
