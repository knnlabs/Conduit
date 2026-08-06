using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.Gateway.Options;

public sealed class UsageTrackingOptions
{
    [Range(1024, 16 * 1024 * 1024)]
    public int MaximumStreamingCompletionCharacters { get; set; } = 2 * 1024 * 1024;

    [Range(1024, 16 * 1024 * 1024)]
    public int MaximumStreamingToolCallCharacters { get; set; } = 1024 * 1024;

    [Range(1, 1024)]
    public int MaximumStreamingToolCalls { get; set; } = 128;

    [Range(1, 120)]
    public int AccountingFinalizationTimeoutSeconds { get; set; } = 10;

    [Range(5, 600)]
    public int GracefulShutdownSeconds { get; set; } = 45;
}
