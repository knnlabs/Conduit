// TokenizerType lives in the global namespace (Shared/ConduitLLM.Configuration/Entities/TokenizerType.cs).

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// The outcome of resolving a <see cref="TokenizerType"/> to a Tiktoken encoding name.
    /// </summary>
    /// <param name="EncodingName">The Tiktoken encoding identifier to hand to TiktokenSharp.</param>
    /// <param name="IsApproximation">
    /// True when the requested tokenizer has no exact TiktokenSharp equivalent and
    /// <paramref name="EncodingName"/> is a documented stand-in. Token counts are estimates in
    /// that case, which matters for context management and fallback billing estimates.
    /// </param>
    /// <param name="IsRecognized">
    /// False when the requested value did not correspond to any known <see cref="TokenizerType"/>.
    /// </param>
    public readonly record struct TokenizerEncoding(string EncodingName, bool IsApproximation, bool IsRecognized);

    /// <summary>
    /// Maps <see cref="TokenizerType"/> values onto the encoding identifiers TiktokenSharp accepts.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Model metadata stores a <see cref="TokenizerType"/> and surfaces it as its enum name
    /// (<c>Cl100KBase</c>, <c>LLaMA3</c>, ...), while TiktokenSharp expects lowercase encoding
    /// identifiers (<c>cl100k_base</c>). Passing the enum name straight through made every
    /// resolution — including the default — take an exception-driven fallback path.
    /// </para>
    /// <para>
    /// TiktokenSharp 1.2.1 implements exactly three encodings: <c>cl100k_base</c>,
    /// <c>p50k_base</c> and <c>o200k_base</c>. <c>p50k_edit</c> and <c>r50k_base</c> throw
    /// <see cref="NotImplementedException"/>, so they are mapped to the nearest implemented
    /// vocabulary rather than requested directly. Every non-OpenAI tokenizer is an approximation:
    /// Conduit uses these counts for context-window checks and for estimating usage when a
    /// provider omits it, not as an authoritative count.
    /// </para>
    /// </remarks>
    public static class TokenizerEncodingMap
    {
        /// <summary>The encoding used whenever no closer equivalent is available.</summary>
        public const string DefaultEncoding = "cl100k_base";

        private const string P50KBase = "p50k_base";
        private const string O200KBase = "o200k_base";

        // Exact: the tokenizer is an OpenAI encoding TiktokenSharp implements.
        // Approximate: the tokenizer is a different vocabulary (or an OpenAI encoding
        // TiktokenSharp does not implement) and the value is the closest available stand-in.
        private static readonly IReadOnlyDictionary<TokenizerType, TokenizerEncoding> Map =
            new Dictionary<TokenizerType, TokenizerEncoding>
            {
                // OpenAI — exact
                [TokenizerType.Cl100KBase] = Exact(DefaultEncoding),
                [TokenizerType.P50KBase] = Exact(P50KBase),
                [TokenizerType.O200KBase] = Exact(O200KBase),

                // OpenAI — not implemented by TiktokenSharp; nearest implemented vocabulary.
                // p50k_edit is p50k_base plus edit-specific special tokens; r50k_base is the
                // GPT-2/Codex vocabulary that p50k_base extends.
                [TokenizerType.P50KEdit] = Approximate(P50KBase),
                [TokenizerType.R50KBase] = Approximate(P50KBase),

                // GPT-OSS harmony is the o200k vocabulary plus harmony-format special tokens.
                [TokenizerType.O200KHarmony] = Approximate(O200KBase),

                // Generic "tiktoken" carries no encoding of its own.
                [TokenizerType.Tiktoken] = Approximate(DefaultEncoding),

                // Non-text models still get asked for a count on occasion (for example when a
                // prompt is logged), so answer with the default rather than failing.
                [TokenizerType.None] = Approximate(DefaultEncoding),

                // Non-OpenAI vocabularies — no Tiktoken equivalent exists, so all approximate.
                [TokenizerType.Claude] = Approximate(DefaultEncoding),
                [TokenizerType.Claude3] = Approximate(DefaultEncoding),
                [TokenizerType.Gemini] = Approximate(DefaultEncoding),
                [TokenizerType.PaLM] = Approximate(DefaultEncoding),
                [TokenizerType.LLaMA] = Approximate(DefaultEncoding),
                [TokenizerType.LLaMA2] = Approximate(DefaultEncoding),
                [TokenizerType.LLaMA3] = Approximate(DefaultEncoding),
                [TokenizerType.Mistral] = Approximate(DefaultEncoding),
                [TokenizerType.Cohere] = Approximate(DefaultEncoding),
                [TokenizerType.Kimi] = Approximate(DefaultEncoding),
                [TokenizerType.Groq] = Approximate(DefaultEncoding),
                [TokenizerType.Cerebras] = Approximate(DefaultEncoding),
                [TokenizerType.MiniMax] = Approximate(DefaultEncoding),
                [TokenizerType.SentencePiece] = Approximate(DefaultEncoding),
                [TokenizerType.BPE] = Approximate(DefaultEncoding),
                [TokenizerType.WordPiece] = Approximate(DefaultEncoding),
            };

        /// <summary>
        /// Resolves a tokenizer identifier to the encoding TiktokenSharp should be asked for.
        /// </summary>
        /// <param name="tokenizerType">
        /// A <see cref="TokenizerType"/> name (<c>Cl100KBase</c>, <c>LLaMA3</c>, ...) or an
        /// encoding identifier already in Tiktoken form (<c>cl100k_base</c>). Matching is
        /// case-insensitive; null, empty and unrecognized values fall back to
        /// <see cref="DefaultEncoding"/>.
        /// </param>
        public static TokenizerEncoding Resolve(string? tokenizerType)
        {
            if (string.IsNullOrWhiteSpace(tokenizerType))
            {
                return Approximate(DefaultEncoding);
            }

            var trimmed = tokenizerType.Trim();

            if (Enum.TryParse<TokenizerType>(trimmed, ignoreCase: true, out var parsed) &&
                Map.TryGetValue(parsed, out var mapped))
            {
                return mapped;
            }

            // Already an encoding identifier (configuration written by hand, or a value that
            // has been through this map once already).
            return trimmed.ToLowerInvariant() switch
            {
                DefaultEncoding => Exact(DefaultEncoding),
                P50KBase => Exact(P50KBase),
                O200KBase => Exact(O200KBase),
                "p50k_edit" => Approximate(P50KBase),
                "r50k_base" => Approximate(P50KBase),
                "o200k_harmony" => Approximate(O200KBase),
                _ => new TokenizerEncoding(DefaultEncoding, IsApproximation: true, IsRecognized: false)
            };
        }

        private static TokenizerEncoding Exact(string encoding) =>
            new(encoding, IsApproximation: false, IsRecognized: true);

        private static TokenizerEncoding Approximate(string encoding) =>
            new(encoding, IsApproximation: true, IsRecognized: true);
    }
}
