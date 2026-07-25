using System.Text.Json;

using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;

using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using Xunit;
using Xunit.Abstractions;

namespace ConduitLLM.Tests.Core.Services
{
    /// <summary>
    /// Pins what <see cref="TiktokenCounter"/> adds on top of the raw vocabulary: which tokenizer a
    /// <c>TokenizerType</c> resolves to, the per-message overhead arithmetic, and the multimodal
    /// content paths.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the companion to <c>TokenVocabularyGoldenTests</c> in the billing-invariant suite.
    /// The split is deliberate: those pins are keyed on an <i>encoding name</i> and must never move,
    /// while these are keyed on a <c>TokenizerType</c> and are expected to move whenever the
    /// resolution table changes. A red test in one file therefore tells you which layer changed.
    /// </para>
    /// <para>
    /// Every probe string here is plain ASCII. Vocabulary behaviour is the other file's job; keeping
    /// this one ASCII means these numbers move only when the counter's arithmetic or the resolution
    /// table moves, and it sidesteps the encoding hazards that file has to guard against.
    /// </para>
    /// <para>
    /// <b>Expected to move.</b> Anything that changes which tokenizer a <c>TokenizerType</c> maps to
    /// will change the <c>Resolution</c> pins for that type, and that is the point of pinning them:
    /// the diff is the review artefact. Update those values deliberately, one line at a time, with
    /// the before/after in the commit message.
    /// </para>
    /// <para>
    /// Captured from Microsoft.ML.Tokenizers 2.0.0 on 2026-07-25 via the skipped emitters below.
    /// (Originally captured from TiktokenSharp 1.2.1; the resolution pins moved when the
    /// TiktokenSharp replacement promoted <c>P50KEdit</c> and <c>R50KBase</c> to exact encodings
    /// and the probe was extended to distinguish all five - see the <see cref="Probe"/> remarks.)
    /// </para>
    /// </remarks>
    public class TiktokenCounterGoldenTests
    {
        private readonly ITestOutputHelper _output;

        public TiktokenCounterGoldenTests(ITestOutputHelper output) => _output = output;

        /// <summary>
        /// Single ASCII probe shared by every resolution pin, chosen so that each of the five
        /// supported encodings produces a <i>different</i> count.
        /// </summary>
        /// <remarks>
        /// This matters more than it looks. Ordinary English prose tokenizes identically across
        /// the encodings - "The quick brown fox jumps over the lazy dog." is 10 tokens in every
        /// one of them - so a prose probe would pin 10 for all 23 TokenizerTypes and silently fail
        /// to notice a resolution change, which is the only thing these pins exist to catch.
        /// Each ingredient separates a specific pair: contractions separate o200k from cl100k; the
        /// long run of a repeated character separates the p50k/r50k family (which merges 4 at a
        /// time) from cl100k and o200k (8 at a time); the whitespace run separates r50k from p50k
        /// (whose vocabulary added whitespace-run merges); and the fim marker separates p50k_edit
        /// from p50k_base, because edit-specific special tokens are their only difference.
        /// <see cref="Probe_ProducesADifferentCountForEveryEncoding"/> enforces the property.
        /// </remarks>
        private static readonly string Probe =
            "don't won't it's they're I'll we've " + new string('a', 64) + "\n\n\t\t   <|fim_middle|>";

        /// <summary>
        /// Token count of <see cref="Probe"/> for each TokenizerType, through the full counter.
        /// </summary>
        private static readonly IReadOnlyDictionary<string, int> ExpectedByTokenizer =
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["None"] = 25,
                ["Cl100KBase"] = 25,
                ["P50KBase"] = 41,
                ["P50KEdit"] = 34,
                ["R50KBase"] = 42,
                ["O200KBase"] = 24,
                ["Claude"] = 25,
                ["Claude3"] = 25,
                ["Gemini"] = 25,
                ["PaLM"] = 25,
                ["LLaMA"] = 25,
                ["LLaMA2"] = 25,
                ["LLaMA3"] = 25,
                ["Mistral"] = 25,
                ["Cohere"] = 25,
                ["O200KHarmony"] = 24,
                ["Kimi"] = 25,
                ["Groq"] = 25,
                ["Cerebras"] = 25,
                ["MiniMax"] = 25,
                ["SentencePiece"] = 25,
                ["BPE"] = 25,
                ["WordPiece"] = 25,
                ["Tiktoken"] = 25,
            };

        /// <summary>
        /// Message shapes exercising the per-message +4, the +1 for Name, and the +3 reply priming.
        /// All content is ASCII so the deltas between rows are pure arithmetic.
        /// </summary>
        private static readonly IReadOnlyDictionary<string, List<Message>> MessageShapes =
            new Dictionary<string, List<Message>>(StringComparer.Ordinal)
            {
                ["msgs-empty-list"] = new(),
                ["msgs-single-user"] = new()
                {
                    new Message { Role = "user", Content = "Hello there." }
                },
                ["msgs-conversation"] = new()
                {
                    new Message { Role = "system", Content = "You are a helpful assistant." },
                    new Message { Role = "user", Content = "What is the capital of France?" },
                    new Message { Role = "assistant", Content = "Paris." },
                    new Message { Role = "user", Content = "And of Germany?" }
                },
                ["msgs-named"] = new()
                {
                    new Message { Role = "tool", Content = "42", Name = "calculator" }
                },
                ["msgs-null-content"] = new()
                {
                    new Message { Role = "assistant", Content = null }
                },
                // Empty string and null take different branches in the counter.
                ["msgs-empty-content"] = new()
                {
                    new Message { Role = "user", Content = "" }
                },
            };

        private static readonly IReadOnlyDictionary<string, int> ExpectedByMessageShape =
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["msgs-conversation"] = 42,
                ["msgs-empty-content"] = 8,
                ["msgs-empty-list"] = 0,
                ["msgs-named"] = 11,
                ["msgs-null-content"] = 8,
                ["msgs-single-user"] = 11,
            };

        /// <summary>
        /// Multimodal content delivered as a <see cref="JsonElement"/>, which is how it arrives when
        /// a request body is deserialized. This path is otherwise untested.
        /// </summary>
        /// <remarks>
        /// Two of these rows pin known defects rather than desired behaviour, so that fixing them is
        /// a visible decision: <c>json-unknown-part</c> shows that an audio content part contributes
        /// zero tokens, and <c>json-text-missing-text</c> shows the same for a malformed text part.
        /// <c>json-one-image</c> and <c>json-three-images</c> pin the hard-coded 65-tokens-per-image
        /// constant, which does not match the real vision formula used elsewhere in the codebase.
        /// </remarks>
        private static readonly IReadOnlyDictionary<string, string> JsonContentShapes =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["json-bare-string"] = "\"hello there\"",
                ["json-text-part"] =
                    "[{\"type\":\"text\",\"text\":\"hello there\"}]",
                ["json-one-image"] =
                    "[{\"type\":\"text\",\"text\":\"hello there\"},"
                    + "{\"type\":\"image_url\",\"image_url\":{\"url\":\"https://example.test/a.png\"}}]",
                ["json-three-images"] =
                    "[{\"type\":\"image_url\",\"image_url\":{\"url\":\"https://example.test/a.png\"}},"
                    + "{\"type\":\"image_url\",\"image_url\":{\"url\":\"https://example.test/b.png\"}},"
                    + "{\"type\":\"image_url\",\"image_url\":{\"url\":\"https://example.test/c.png\"}}]",
                ["json-image-only"] =
                    "[{\"type\":\"image_url\",\"image_url\":{\"url\":\"https://example.test/a.png\"}}]",
                ["json-unknown-part"] =
                    "[{\"type\":\"input_audio\",\"input_audio\":{\"data\":\"AAAA\",\"format\":\"wav\"}}]",
                ["json-text-missing-text"] = "[{\"type\":\"text\"}]",
            };

        private static readonly IReadOnlyDictionary<string, int> ExpectedByJsonShape =
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["json-bare-string"] = 10,
                ["json-image-only"] = 73,
                ["json-one-image"] = 75,
                ["json-text-missing-text"] = 8,
                ["json-text-part"] = 10,
                ["json-three-images"] = 203,
                ["json-unknown-part"] = 8,
            };

        public static TheoryData<string> TokenizerTypes()
        {
            var data = new TheoryData<string>();
            foreach (TokenizerType tokenizerType in Enum.GetValues<TokenizerType>())
            {
                data.Add(tokenizerType.ToString());
            }
            return data;
        }

        public static TheoryData<string> MessageShapeIds() => Keys(MessageShapes.Keys);

        public static TheoryData<string> JsonShapeIds() => Keys(JsonContentShapes.Keys);

        private static TheoryData<string> Keys(IEnumerable<string> keys)
        {
            var data = new TheoryData<string>();
            foreach (var key in keys.OrderBy(k => k, StringComparer.Ordinal))
            {
                data.Add(key);
            }
            return data;
        }

        [Theory]
        [MemberData(nameof(TokenizerTypes))]
        public async Task Resolution_ProducesPinnedTokenCount(string tokenizerType)
        {
            var counter = CounterFor(tokenizerType);

            var actual = await counter.EstimateTokenCountAsync($"probe-{tokenizerType}", Probe);

            Assert.Equal(ExpectedByTokenizer[tokenizerType], actual);
        }

        [Theory]
        [MemberData(nameof(MessageShapeIds))]
        public async Task MessageShape_ProducesPinnedTokenCount(string shapeId)
        {
            var counter = CounterFor("cl100k_base");

            var actual = await counter.EstimateTokenCountAsync("probe-messages", MessageShapes[shapeId]);

            Assert.Equal(ExpectedByMessageShape[shapeId], actual);
        }

        [Theory]
        [MemberData(nameof(JsonShapeIds))]
        public async Task JsonContentShape_ProducesPinnedTokenCount(string shapeId)
        {
            var counter = CounterFor("cl100k_base");

            var actual = await counter.EstimateTokenCountAsync("probe-json", JsonMessage(shapeId));

            Assert.Equal(ExpectedByJsonShape[shapeId], actual);
        }

        /// <summary>
        /// Guards the property that makes the resolution pins meaningful: the probe must produce a
        /// different count under each supported encoding.
        /// </summary>
        /// <remarks>
        /// Without this, a probe whose count happens to coincide across encodings would make every
        /// resolution pin identical, and a change that silently re-pointed a TokenizerType at the
        /// wrong tokenizer would still be green. If this test fails, do not relax it - pick a probe
        /// that separates the encodings again and re-pin.
        /// </remarks>
        [Fact]
        public async Task Probe_ProducesADifferentCountForEveryEncoding()
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var encoding in new[] { "cl100k_base", "p50k_base", "p50k_edit", "r50k_base", "o200k_base" })
            {
                counts[encoding] = await CounterFor(encoding)
                    .EstimateTokenCountAsync($"probe-{encoding}", Probe);
            }

            Assert.Equal(counts.Count, counts.Values.Distinct().Count());
        }

        /// <summary>
        /// Fails if a shape was added without a pinned count, so no table can outgrow its coverage.
        /// </summary>
        [Fact]
        public void EveryShape_HasAPinnedCount()
        {
            var missing = new List<string>();

            foreach (TokenizerType tokenizerType in Enum.GetValues<TokenizerType>())
            {
                if (!ExpectedByTokenizer.ContainsKey(tokenizerType.ToString()))
                {
                    missing.Add($"tokenizer:{tokenizerType}");
                }
            }

            missing.AddRange(MessageShapes.Keys
                .Where(k => !ExpectedByMessageShape.ContainsKey(k)).Select(k => $"messages:{k}"));
            missing.AddRange(JsonContentShapes.Keys
                .Where(k => !ExpectedByJsonShape.ContainsKey(k)).Select(k => $"json:{k}"));

            Assert.True(missing.Count == 0, "Unpinned shapes: " + string.Join(", ", missing));
        }

        [Fact(Skip = "Baseline emitter. Un-skip locally, run, paste each block into the matching "
                   + "table, re-skip. Never leave un-skipped: it asserts nothing.")]
        public async Task EmitBaseline()
        {
            _output.WriteLine("--- RESOLUTION ---");
            foreach (TokenizerType tokenizerType in Enum.GetValues<TokenizerType>())
            {
                var name = tokenizerType.ToString();
                var count = await CounterFor(name).EstimateTokenCountAsync($"probe-{name}", Probe);
                _output.WriteLine($"                [\"{name}\"] = {count},");
            }

            _output.WriteLine("--- MESSAGES ---");
            foreach (var id in MessageShapes.Keys.OrderBy(k => k, StringComparer.Ordinal))
            {
                var count = await CounterFor("cl100k_base")
                    .EstimateTokenCountAsync("probe-messages", MessageShapes[id]);
                _output.WriteLine($"                [\"{id}\"] = {count},");
            }

            _output.WriteLine("--- JSON ---");
            foreach (var id in JsonContentShapes.Keys.OrderBy(k => k, StringComparer.Ordinal))
            {
                var count = await CounterFor("cl100k_base")
                    .EstimateTokenCountAsync("probe-json", JsonMessage(id));
                _output.WriteLine($"                [\"{id}\"] = {count},");
            }
        }

        /// <summary>Wraps a JSON content payload in the single user message the counter will see.</summary>
        private static List<Message> JsonMessage(string shapeId) => new()
        {
            new Message
            {
                Role = "user",
                // Deserializing to JsonElement detaches it from the JsonDocument, so it stays valid.
                Content = JsonSerializer.Deserialize<JsonElement>(JsonContentShapes[shapeId])
            }
        };

        /// <summary>
        /// Builds a counter pinned to one tokenizer. The capability service is the only input that
        /// selects one, and it accepts either a TokenizerType name or an encoding identifier.
        /// </summary>
        private static ITokenCounter CounterFor(string tokenizerType)
        {
            var capabilities = new Mock<IModelCapabilityService>();
            capabilities
                .Setup(c => c.GetTokenizerTypeAsync(It.IsAny<string>()))
                .ReturnsAsync(tokenizerType);

            return new TiktokenCounter(NullLogger<TiktokenCounter>.Instance, capabilities.Object);
        }
    }
}
