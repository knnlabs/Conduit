using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;

using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using Xunit.Abstractions;

namespace ConduitLLM.BillingInvariantTests;

/// <summary>
/// Pins the exact token count each tiktoken encoding produces for a fixed corpus.
/// </summary>
/// <remarks>
/// <para>
/// These are <b>vocabulary</b> pins: they assert that a given encoding's BPE ranks and UTF-8
/// handling produce a specific number. They deliberately do not exercise
/// <see cref="TiktokenCounter"/>'s per-message arithmetic - that is pinned separately in
/// <c>TiktokenCounterGoldenTests</c> - so that a red test here means the vocabulary moved and
/// nothing else.
/// </para>
/// <para>
/// They live in the billing-invariant suite because the counts they pin become money:
/// <c>ChatSpendEstimator</c> turns a prompt count into a spend reservation with no safety buffer,
/// and <c>UsageEstimationService</c> turns one into the billed usage whenever a streaming provider
/// omits its own.
/// </para>
/// <para>
/// The corpus is addressed by <i>encoding identifier</i> ("cl100k_base"), which
/// <c>TokenizerEncodingMap.Resolve</c> accepts verbatim. That keeps this file independent of the
/// <c>TokenizerType</c> mapping table and of whichever library is underneath, so a tokenizer
/// migration should not need to touch it.
/// </para>
/// <para>
/// <b>Provenance.</b> Captured from TiktokenSharp 1.2.1 on 2026-07-25 via the skipped
/// <see cref="EmitBaseline"/> emitter below, then independently confirmed against Python
/// <c>tiktoken</c>:
/// </para>
/// <code>
/// enc = tiktoken.get_encoding("cl100k_base")          # and p50k_base, o200k_base
/// len(enc.encode(text, disallowed_special=()))
/// </code>
/// <para>
/// All 54 comparable entries matched exactly (18 corpus entries x 3 encodings). The four not
/// cross-checked are <c>empty</c> (short-circuited before tokenizing), <c>prose-paragraph</c> and
/// <c>long-mixed-100k</c> (too long to transcribe reliably into the oracle script), and
/// <c>lone-surrogate</c> (Python cannot UTF-8 encode an unpaired surrogate at all, which is itself
/// the interesting difference).
/// </para>
/// <para>
/// <c>disallowed_special=()</c> is required to reproduce these numbers: it makes Python treat
/// <c>&lt;|endoftext|&gt;</c> as ordinary text, which is what TiktokenSharp does by default. A
/// tokenizer that recognises the marker instead counts the <c>special-token-text</c> entry as 4
/// rather than 8 - a real and expected difference, not a broken pin.
/// </para>
/// <para>
/// So these numbers are anchored to the tiktoken specification, not merely to whatever the current
/// library happens to do. A failure here means the vocabulary changed - it does not mean the pin is
/// stale. Do not re-run the emitter to make a red test green; find out what moved and why first.
/// </para>
/// </remarks>
public sealed class TokenVocabularyGoldenTests
{
    private readonly ITestOutputHelper _output;

    public TokenVocabularyGoldenTests(ITestOutputHelper output) => _output = output;

    /// <summary>Encodings addressed verbatim, bypassing the TokenizerType table.</summary>
    private static readonly string[] Encodings = { "cl100k_base", "p50k_base", "o200k_base" };

    /// <summary>
    /// Every non-ASCII character is written as a backslash-u escape and every newline as \n, never
    /// as a literal and never in a verbatim string. .gitattributes normalizes only *.sh, so a
    /// literal would pick up CRLF on a Windows checkout, and a BOM or editor round-trip could
    /// silently change the bytes - either of which moves the counts this file exists to pin.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> Corpus =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // Degenerate and guard cases.
            ["empty"] = "",
            ["single-space"] = " ",
            ["whitespace-run"] = "    \n\n\t\t   ",
            ["control-chars"] = "\u0000\u0001\u001F",

            // Ordinary English: the common case, and the anchor for "nothing changed".
            ["prose-short"] = "The quick brown fox jumps over the lazy dog.",
            ["prose-paragraph"] =
                "Conduit is a unified gateway for large language model providers. It accepts an "
                + "OpenAI-compatible request, resolves the requested model alias to a provider "
                + "mapping, forwards the call, and records the resulting usage against a virtual "
                + "key. Because providers differ in what they report, the gateway sometimes has to "
                + "estimate token usage rather than read it from the response. Those estimates feed "
                + "spend reservations and rate limits, so the arithmetic behind them is worth "
                + "pinning down precisely rather than trusting to luck or to a library upgrade.",
            ["contractions-lower"] = "don't won't it's they're I'll we've",
            ["contractions-upper"] = "DON'T WON'T IT'S THEY'RE I'LL WE'VE",
            ["punctuation-run"] = "...!!!???---___===+++",

            // Source code: indentation runs tokenize specially in cl100k.
            ["code-csharp"] = "public sealed class Foo\n{\n    private readonly int _x;\n}\n",
            ["code-json"] = "{\"model\":\"gpt-4o\",\"messages\":[{\"role\":\"user\"}]}",

            // Non-Latin scripts. Chinese for "Hello world. This is a test."
            ["cjk-chinese"] =
                "\u4F60\u597D\u4E16\u754C\u3002\u8FD9\u662F\u4E00\u4E2A\u6D4B\u8BD5\u3002",
            // Japanese hiragana wrapped around an ASCII word.
            ["cjk-japanese-mixed"] = "\u3053\u3093\u306B\u3061\u306FConduit\u3067\u3059",
            ["cyrillic"] = "\u041F\u0440\u0438\u0432\u0435\u0442",
            // Decomposed (e + combining acute) against precomposed. Must NOT be normalized away.
            ["combining-marks"] = "e\u0301 vs \u00E9",

            // Surrogate handling: the highest-risk divergence between UTF-8 encoders.
            ["emoji-bmp"] = "\u2764",
            ["emoji-astral"] = "\uD83D\uDE00",
            ["emoji-zwj"] = "\uD83D\uDC69\u200D\uD83D\uDCBB",
            // Unpaired high surrogate. Reachable from real traffic; encoders differ on whether
            // they substitute U+FFFD or throw, and throwing lands in the counter's char/4 fallback.
            ["lone-surrogate"] = "a\uD800b",

            // Adversarial: a special-token marker appearing as ordinary user text. Whether this
            // encodes as one token or as its literal characters is a library policy decision, and
            // it is directly reachable from a caller's prompt.
            ["special-token-text"] = "hello <|endoftext|> world",

            // Scale.
            ["repeat-10k"] = new string('a', 10_000),
            ["long-mixed-100k"] = BuildLongMixed(),
        };

    /// <summary>Deterministic ~100 KB of mixed content. No randomness: the pins must be stable.</summary>
    private static string BuildLongMixed()
    {
        const string Unit = "The gateway forwarded 1,024 tokens to provider #7. "
            + "\u4F60\u597D\u4E16\u754C\u3002 { \"ok\": true }\n";
        var builder = new System.Text.StringBuilder(capacity: 102_400);
        while (builder.Length < 100_000)
        {
            builder.Append(Unit);
        }
        return builder.ToString();
    }

    /// <summary>
    /// Counts pinned from the current implementation, keyed "{encoding}|{corpusId}".
    /// </summary>
    private static readonly IReadOnlyDictionary<string, int> Expected =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["cl100k_base|cjk-chinese"] = 11,
            ["cl100k_base|cjk-japanese-mixed"] = 4,
            ["cl100k_base|code-csharp"] = 14,
            ["cl100k_base|code-json"] = 16,
            ["cl100k_base|combining-marks"] = 4,
            ["cl100k_base|contractions-lower"] = 12,
            ["cl100k_base|contractions-upper"] = 14,
            ["cl100k_base|control-chars"] = 3,
            ["cl100k_base|cyrillic"] = 3,
            ["cl100k_base|emoji-astral"] = 2,
            ["cl100k_base|emoji-bmp"] = 2,
            ["cl100k_base|emoji-zwj"] = 7,
            ["cl100k_base|empty"] = 0,
            ["cl100k_base|lone-surrogate"] = 3,
            ["cl100k_base|long-mixed-100k"] = 36114,
            ["cl100k_base|prose-paragraph"] = 99,
            ["cl100k_base|prose-short"] = 10,
            ["cl100k_base|punctuation-run"] = 7,
            ["cl100k_base|repeat-10k"] = 1250,
            ["cl100k_base|single-space"] = 1,
            ["cl100k_base|special-token-text"] = 8,
            ["cl100k_base|whitespace-run"] = 2,
            ["p50k_base|cjk-chinese"] = 20,
            ["p50k_base|cjk-japanese-mixed"] = 10,
            ["p50k_base|code-csharp"] = 18,
            ["p50k_base|code-json"] = 17,
            ["p50k_base|combining-marks"] = 5,
            ["p50k_base|contractions-lower"] = 12,
            ["p50k_base|contractions-upper"] = 19,
            ["p50k_base|control-chars"] = 3,
            ["p50k_base|cyrillic"] = 7,
            ["p50k_base|emoji-astral"] = 2,
            ["p50k_base|emoji-bmp"] = 2,
            ["p50k_base|emoji-zwj"] = 7,
            ["p50k_base|empty"] = 0,
            ["p50k_base|lone-surrogate"] = 3,
            ["p50k_base|long-mixed-100k"] = 40281,
            ["p50k_base|prose-paragraph"] = 100,
            ["p50k_base|prose-short"] = 10,
            ["p50k_base|punctuation-run"] = 7,
            ["p50k_base|repeat-10k"] = 2500,
            ["p50k_base|single-space"] = 1,
            ["p50k_base|special-token-text"] = 9,
            ["p50k_base|whitespace-run"] = 5,
            ["o200k_base|cjk-chinese"] = 7,
            ["o200k_base|cjk-japanese-mixed"] = 4,
            ["o200k_base|code-csharp"] = 14,
            ["o200k_base|code-json"] = 17,
            ["o200k_base|combining-marks"] = 4,
            ["o200k_base|contractions-lower"] = 6,
            ["o200k_base|contractions-upper"] = 14,
            ["o200k_base|control-chars"] = 3,
            ["o200k_base|cyrillic"] = 2,
            ["o200k_base|emoji-astral"] = 1,
            ["o200k_base|emoji-bmp"] = 1,
            ["o200k_base|emoji-zwj"] = 5,
            ["o200k_base|empty"] = 0,
            ["o200k_base|lone-surrogate"] = 3,
            ["o200k_base|long-mixed-100k"] = 31947,
            ["o200k_base|prose-paragraph"] = 99,
            ["o200k_base|prose-short"] = 10,
            ["o200k_base|punctuation-run"] = 7,
            ["o200k_base|repeat-10k"] = 1250,
            ["o200k_base|single-space"] = 1,
            ["o200k_base|special-token-text"] = 9,
            ["o200k_base|whitespace-run"] = 2,
        };

    public static TheoryData<string, string> Cases()
    {
        var data = new TheoryData<string, string>();
        foreach (var encoding in Encodings)
        {
            foreach (var id in Corpus.Keys.OrderBy(k => k, StringComparer.Ordinal))
            {
                data.Add(encoding, id);
            }
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Encoding_ProducesPinnedTokenCount(string encoding, string corpusId)
    {
        var counter = CounterFor(encoding);

        var actual = await counter.EstimateTokenCountAsync($"probe-{encoding}", Corpus[corpusId]);

        Assert.Equal(Expected[$"{encoding}|{corpusId}"], actual);
    }

    /// <summary>
    /// Fails if a corpus entry was added without a pinned count, so the corpus cannot silently
    /// grow past its coverage.
    /// </summary>
    [Fact]
    public void EveryCorpusEntry_HasAPinnedCountForEveryEncoding()
    {
        var missing = (from encoding in Encodings
                       from id in Corpus.Keys
                       let key = $"{encoding}|{id}"
                       where !Expected.ContainsKey(key)
                       select key)
                      .OrderBy(k => k, StringComparer.Ordinal)
                      .ToList();

        Assert.True(missing.Count == 0, "Unpinned corpus entries: " + string.Join(", ", missing));
    }

    /// <summary>
    /// Fails if a pin exists for a corpus entry or encoding that no longer exists, so deletions do
    /// not leave dead expectations behind.
    /// </summary>
    [Fact]
    public void EveryPin_CorrespondsToALiveCorpusEntry()
    {
        var live = (from encoding in Encodings from id in Corpus.Keys select $"{encoding}|{id}")
            .ToHashSet(StringComparer.Ordinal);

        var orphaned = Expected.Keys.Where(k => !live.Contains(k))
            .OrderBy(k => k, StringComparer.Ordinal).ToList();

        Assert.True(orphaned.Count == 0, "Pins with no corpus entry: " + string.Join(", ", orphaned));
    }

    [Fact(Skip = "Baseline emitter. Un-skip locally, run, paste the output into Expected, re-skip. "
               + "Never leave un-skipped: it asserts nothing.")]
    public async Task EmitBaseline()
    {
        foreach (var encoding in Encodings)
        {
            var counter = CounterFor(encoding);
            foreach (var id in Corpus.Keys.OrderBy(k => k, StringComparer.Ordinal))
            {
                var count = await counter.EstimateTokenCountAsync($"probe-{encoding}", Corpus[id]);
                _output.WriteLine($"            [\"{encoding}|{id}\"] = {count},");
            }
        }
    }

    /// <summary>
    /// Builds a counter pinned to one encoding. The capability service is the only input that
    /// selects a tokenizer, and it accepts an encoding identifier verbatim.
    /// </summary>
    private static ITokenCounter CounterFor(string encodingName)
    {
        var capabilities = new Mock<IModelCapabilityService>();
        capabilities
            .Setup(c => c.GetTokenizerTypeAsync(It.IsAny<string>()))
            .ReturnsAsync(encodingName);

        return new TiktokenCounter(NullLogger<TiktokenCounter>.Instance, capabilities.Object);
    }
}
