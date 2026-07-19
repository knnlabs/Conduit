namespace ConduitLLM.Gateway.UsageTracking;

/// <summary>
/// Marker interface for typed usage-tracking context flowing from controllers to
/// <c>UsageTrackingMiddleware</c>. Replaces string-keyed <c>HttpContext.Items</c>
/// entries for request-shape data (model, size, quality, duration, etc.) with a
/// compile-checked carrier. Set via <see cref="UsageContextExtensions.SetUsageContext"/>;
/// read via <see cref="UsageContextExtensions.GetUsageContext"/>.
/// </summary>
public interface IUsageContext
{
    /// <summary>Model alias as submitted by the caller (before provider resolution).</summary>
    string Model { get; }
}

/// <summary>
/// Image generation request context captured by <c>ImagesController</c>.
/// </summary>
public sealed class ImageUsageContext : IUsageContext
{
    public required string Model { get; init; }
    public string? Quality { get; init; }
    public string? Size { get; init; }
    public int? N { get; init; }
    public string? Style { get; init; }
}

/// <summary>
/// Video generation request context captured by <c>VideosController</c>.
/// </summary>
public sealed class VideoUsageContext : IUsageContext
{
    public required string Model { get; init; }
    public string? Size { get; init; }
    public int? Duration { get; init; }
    public int? Fps { get; init; }
    public string? Style { get; init; }
    public int? N { get; init; }
    /// <summary>
    /// Rules-based pricing parameters built from request fields plus <c>ExtensionData</c>
    /// (e.g., resolution, duration, fps, with_audio, aspect_ratio).
    /// </summary>
    public Dictionary<string, object>? PricingParameters { get; init; }
}

/// <summary>
/// Audio (speech-to-text / text-to-speech) request context captured by <c>AudioController</c>.
/// Carries the billable units (audio duration for STT, character count for TTS) so the middleware
/// can bill without parsing the response — which for TTS is raw binary audio.
/// </summary>
public sealed class AudioUsageContext : IUsageContext
{
    public required string Model { get; init; }

    /// <summary>Transcribed audio duration in seconds (speech-to-text).</summary>
    public double? AudioDurationSeconds { get; init; }

    /// <summary>Number of input characters synthesized (text-to-speech).</summary>
    public int? TtsCharacters { get; init; }
}

public static class UsageContextExtensions
{
    private static readonly object Key = new();

    public static void SetUsageContext(this HttpContext context, IUsageContext value)
    {
        context.Items[Key] = value;
    }

    public static IUsageContext? GetUsageContext(this HttpContext context)
    {
        return context.Items.TryGetValue(Key, out var value) ? value as IUsageContext : null;
    }
}
