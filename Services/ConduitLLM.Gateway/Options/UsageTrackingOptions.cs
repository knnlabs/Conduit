using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.Gateway.Options;

public sealed class UsageTrackingOptions
{
    [Range(1024, 64L * 1024 * 1024)]
    public long MaximumLegacyResponseCaptureBytes { get; set; } = 4L * 1024 * 1024;
}
