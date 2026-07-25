namespace ConduitLLM.Core.Models
{
    /// <summary>
    /// How trustworthy a token count is, ordered from most to least trustworthy.
    /// </summary>
    /// <remarks>
    /// The numeric ordering is meaningful and load-bearing: a higher value is strictly less
    /// trustworthy, so a count assembled from parts carries the maximum of its parts' values.
    /// Consumers that reserve or bill on these counts size their safety buffers by tier —
    /// see <c>ChatSpendEstimator</c> and <c>UsageEstimationService</c>.
    /// </remarks>
    public enum TokenCountFidelity
    {
        /// <summary>Counted with the model's own vocabulary.</summary>
        Exact = 0,

        /// <summary>
        /// Counted with a documented stand-in vocabulary (for example a Claude or Llama model
        /// counted with <c>cl100k_base</c>). Typically within 10-30% of the true count; worse on
        /// CJK text and source code.
        /// </summary>
        ApproximateVocabulary = 1,

        /// <summary>
        /// The characters-divided-by-four heuristic; no vocabulary was available. Under-counts
        /// English prose by roughly 20-40% and CJK text by far more. This tier appearing in
        /// production means tokenizer data is broken and billing accuracy is degraded (#1227).
        /// </summary>
        CharacterHeuristic = 2,
    }

    /// <summary>
    /// A token count together with how it was produced.
    /// </summary>
    /// <remarks>
    /// There is deliberately no implicit conversion to or from <see cref="int"/>: dropping
    /// <see cref="Fidelity"/> is a decision each consumer must make visibly, because the count's
    /// error margin — and therefore any buffer applied to it — depends on the tier.
    /// </remarks>
    /// <param name="Tokens">The estimated token count.</param>
    /// <param name="Fidelity">How the estimate was produced.</param>
    public readonly record struct TokenCount(int Tokens, TokenCountFidelity Fidelity)
    {
        /// <summary>The less trustworthy of two fidelities, per the enum's severity ordering.</summary>
        public static TokenCountFidelity Worst(TokenCountFidelity left, TokenCountFidelity right) =>
            left >= right ? left : right;
    }
}
