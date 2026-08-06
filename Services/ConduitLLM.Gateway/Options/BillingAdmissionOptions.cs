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

    /// <summary>
    /// Buffer applied to the prompt-token estimate when it was produced with a stand-in
    /// vocabulary (#1233). Exact counts are reserved as-is. Reservations are reconciled against
    /// real usage, so over-reserving briefly holds budget rather than over-billing.
    /// </summary>
    [Range(0.0, 2.0)]
    public double ApproximateVocabularyPromptBuffer { get; set; } = 0.15;

    /// <summary>
    /// Buffer applied to the prompt-token estimate when it came from the chars/4 heuristic,
    /// which under-counts English prose by 20-40% and CJK text by far more (#1233).
    /// </summary>
    [Range(0.0, 2.0)]
    public double CharacterHeuristicPromptBuffer { get; set; } = 0.50;
}
