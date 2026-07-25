using ConduitLLM.Core.Models;

using Prometheus;

namespace ConduitLLM.Core.Metrics;

/// <summary>
/// Operational signals for token-count estimation fidelity.
/// </summary>
/// <remarks>
/// The <c>character_heuristic</c> series is the alarm line: it counts estimates produced by the
/// chars/4 fallback, which only happens when tokenizer vocabulary data is unavailable. Any
/// sustained rate there means spend reservations and fallback billing are running degraded —
/// the failure mode #1227 shipped silently before this signal existed.
/// </remarks>
public static class TokenCountingMetrics
{
    public static readonly Counter Estimates = Prometheus.Metrics.CreateCounter(
        "conduit_token_count_estimates_total",
        "Token-count estimates by fidelity tier (exact, approximate_vocabulary, character_heuristic)",
        new CounterConfiguration
        {
            LabelNames = new[] { "fidelity" }
        });

    /// <summary>Records one estimate at the given fidelity.</summary>
    public static void Record(TokenCountFidelity fidelity) =>
        Estimates.WithLabels(LabelFor(fidelity)).Inc();

    private static string LabelFor(TokenCountFidelity fidelity) => fidelity switch
    {
        TokenCountFidelity.Exact => "exact",
        TokenCountFidelity.ApproximateVocabulary => "approximate_vocabulary",
        TokenCountFidelity.CharacterHeuristic => "character_heuristic",
        _ => "unknown"
    };
}
