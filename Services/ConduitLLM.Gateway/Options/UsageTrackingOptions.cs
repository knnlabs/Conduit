using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.Gateway.Options;

public sealed class UsageTrackingOptions
{
    [Range(1024, 64L * 1024 * 1024)]
    public long MaximumLegacyResponseCaptureBytes { get; set; } = 4L * 1024 * 1024;

    [Range(1024, 16 * 1024 * 1024)]
    public int MaximumStreamingCompletionCharacters { get; set; } = 2 * 1024 * 1024;

    [Range(1024, 16 * 1024 * 1024)]
    public int MaximumStreamingToolCallCharacters { get; set; } = 1024 * 1024;

    [Range(1, 1024)]
    public int MaximumStreamingToolCalls { get; set; } = 128;
}
