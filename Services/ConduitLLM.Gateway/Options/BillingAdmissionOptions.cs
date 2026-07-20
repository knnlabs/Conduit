using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.Gateway.Options;

public enum BillingAdmissionMode
{
    Off,
    Shadow,
    Enforce
}

public sealed class BillingAdmissionOptions
{
    public const string SectionName = "BillingAdmission";

    public BillingAdmissionMode Mode { get; set; } = BillingAdmissionMode.Shadow;

    [Range(1, 1_000_000)]
    public int DefaultMaximumOutputTokens { get; set; } = 4096;

    [Range(1, 1_000_000)]
    public int MaximumOutputTokensCap { get; set; } = 32_768;
}
