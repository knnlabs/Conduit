using System.Text.Json;
using System.Text.Json.Nodes;

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
                // Agentic history: the assistant's tool call and the tool result both travel back
                // to the provider as prompt context, so both must contribute tokens (#1229).
                ["msgs-tool-call-round-trip"] = new()
                {
                    new Message { Role = "user", Content = "What is the weather in Paris?" },
                    new Message
                    {
                        Role = "assistant",
                        Content = null,
                        ToolCalls = new List<ToolCall>
                        {
                            new ToolCall
                            {
                                Id = "call_1",
                                Function = new FunctionCall { Name = "get_weather", Arguments = "{\"city\":\"Paris\"}" }
                            }
                        }
                    },
                    new Message { Role = "tool", Content = "{\"temp_c\":21}", ToolCallId = "call_1" }
                },
                ["msgs-parallel-tool-calls"] = new()
                {
                    new Message
                    {
                        Role = "assistant",
                        Content = null,
                        ToolCalls = new List<ToolCall>
                        {
                            new ToolCall
                            {
                                Id = "call_1",
                                Function = new FunctionCall { Name = "get_weather", Arguments = "{\"city\":\"Paris\"}" }
                            },
                            new ToolCall
                            {
                                Id = "call_2",
                                Function = new FunctionCall { Name = "get_weather", Arguments = "{\"city\":\"Berlin\"}" }
                            }
                        }
                    }
                },
                // Typed content parts (the SDK-side shape) must count the same as their
                // deserialized JSON equivalents: text tokenized, image priced by the vision
                // formula (85 for low detail here), not dropped as the pre-#1231 text-only
                // extraction did.
                ["msgs-typed-content-parts"] = new()
                {
                    new Message
                    {
                        Role = "user",
                        Content = new List<object>
                        {
                            new TextContentPart { Text = "hello there" },
                            new ImageUrlContentPart
                            {
                                ImageUrl = new ImageUrl { Url = "https://example.test/a.png", Detail = "low" }
                            }
                        }
                    }
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
                ["msgs-parallel-tool-calls"] = 28,
                ["msgs-single-user"] = 11,
                ["msgs-tool-call-round-trip"] = 41,
                ["msgs-typed-content-parts"] = 95,
            };

        /// <summary>
        /// Tool-definition shapes counted alongside a fixed single-user message
        /// (<c>msgs-single-user</c>, pinned at 11), so each pin isolates what the tools add (#1229).
        /// </summary>
        /// <remarks>
        /// The heuristic being pinned: 10 tokens of scaffolding when any tools are present, 6 per
        /// function, plus the tokenized name, description, and compact-serialized parameter schema.
        /// Counting the raw JSON schema over-counts slightly against OpenAI's TypeScript-style
        /// compaction — deliberate, since these counts feed reservations and fallback billing.
        /// </remarks>
        private static readonly IReadOnlyDictionary<string, IReadOnlyList<Tool>> ToolShapes =
            new Dictionary<string, IReadOnlyList<Tool>>(StringComparer.Ordinal)
            {
                ["tools-name-only"] = new List<Tool>
                {
                    new Tool { Function = new FunctionDefinition { Name = "get_weather" } }
                },
                ["tools-with-description"] = new List<Tool>
                {
                    new Tool
                    {
                        Function = new FunctionDefinition
                        {
                            Name = "get_weather",
                            Description = "Get the current weather for a city."
                        }
                    }
                },
                ["tools-with-parameters"] = new List<Tool>
                {
                    new Tool
                    {
                        Function = new FunctionDefinition
                        {
                            Name = "get_weather",
                            Description = "Get the current weather for a city.",
                            Parameters = new JsonObject
                            {
                                ["type"] = "object",
                                ["properties"] = new JsonObject
                                {
                                    ["city"] = new JsonObject
                                    {
                                        ["type"] = "string",
                                        ["description"] = "City name"
                                    }
                                },
                                ["required"] = new JsonArray("city")
                            }
                        }
                    }
                },
                ["tools-two-functions"] = new List<Tool>
                {
                    new Tool
                    {
                        Function = new FunctionDefinition
                        {
                            Name = "get_weather",
                            Description = "Get the current weather for a city."
                        }
                    },
                    new Tool
                    {
                        Function = new FunctionDefinition
                        {
                            Name = "get_forecast",
                            Description = "Get a five day forecast for a city."
                        }
                    }
                },
            };

        private static readonly IReadOnlyDictionary<string, int> ExpectedByToolShape =
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["tools-name-only"] = 29,
                ["tools-two-functions"] = 55,
                ["tools-with-description"] = 37,
                ["tools-with-parameters"] = 60,
            };

        /// <summary>
        /// Multimodal content delivered as a <see cref="JsonElement"/>, which is how it arrives when
        /// a request body is deserialized. This path is otherwise untested.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Two of these rows pin known defects rather than desired behaviour, so that fixing them is
        /// a visible decision: <c>json-unknown-part</c> shows that an audio content part contributes
        /// zero tokens, and <c>json-text-missing-text</c> shows the same for a malformed text part.
        /// </para>
        /// <para>
        /// Image parts are priced by the vision formula (#1231): low detail is the fixed 85, a
        /// base64 data URL has its dimensions read from the embedded header and is tiled
        /// (512x512 → 170 + 1x170 = 340), and a remote URL — whose geometry cannot be known
        /// without fetching it — is charged the conservative high-detail default of 850. The
        /// remote-URL rows moved from the previous 65-per-image constant, which under-counted
        /// high-detail images roughly 10-17x.
        /// </para>
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
                ["json-image-low-detail"] =
                    "[{\"type\":\"image_url\",\"image_url\":{\"url\":\"https://example.test/a.png\",\"detail\":\"low\"}}]",
                ["json-image-base64-512"] =
                    "[{\"type\":\"image_url\",\"image_url\":{\"url\":\"" + PngDataUrl(512, 512) + "\"}}]",
                ["json-unknown-part"] =
                    "[{\"type\":\"input_audio\",\"input_audio\":{\"data\":\"AAAA\",\"format\":\"wav\"}}]",
                ["json-text-missing-text"] = "[{\"type\":\"text\"}]",
            };

        private static readonly IReadOnlyDictionary<string, int> ExpectedByJsonShape =
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["json-bare-string"] = 10,
                ["json-image-base64-512"] = 348,
                ["json-image-low-detail"] = 93,
                ["json-image-only"] = 858,
                ["json-one-image"] = 860,
                ["json-text-missing-text"] = 8,
                ["json-text-part"] = 10,
                ["json-three-images"] = 2558,
                ["json-unknown-part"] = 8_200,
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

        public static TheoryData<string> ToolShapeIds() => Keys(ToolShapes.Keys);

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

            Assert.Equal(ExpectedByTokenizer[tokenizerType], actual.Tokens);
        }

        [Theory]
        [MemberData(nameof(MessageShapeIds))]
        public async Task MessageShape_ProducesPinnedTokenCount(string shapeId)
        {
            var counter = CounterFor("cl100k_base");

            var actual = await counter.EstimateTokenCountAsync("probe-messages", MessageShapes[shapeId]);

            Assert.Equal(ExpectedByMessageShape[shapeId], actual.Tokens);
        }

        [Theory]
        [MemberData(nameof(JsonShapeIds))]
        public async Task JsonContentShape_ProducesPinnedTokenCount(string shapeId)
        {
            var counter = CounterFor("cl100k_base");

            var actual = await counter.EstimateTokenCountAsync("probe-json", JsonMessage(shapeId));

            Assert.Equal(ExpectedByJsonShape[shapeId], actual.Tokens);
        }

        [Theory]
        [MemberData(nameof(ToolShapeIds))]
        public async Task ToolShape_ProducesPinnedTokenCount(string shapeId)
        {
            var counter = CounterFor("cl100k_base");

            var actual = await counter.EstimateTokenCountAsync(
                "probe-tools", MessageShapes["msgs-single-user"], ToolShapes[shapeId]);

            Assert.Equal(ExpectedByToolShape[shapeId], actual.Tokens);
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
                counts[encoding] = (await CounterFor(encoding)
                    .EstimateTokenCountAsync($"probe-{encoding}", Probe)).Tokens;
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
            missing.AddRange(ToolShapes.Keys
                .Where(k => !ExpectedByToolShape.ContainsKey(k)).Select(k => $"tools:{k}"));

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
                _output.WriteLine($"                [\"{name}\"] = {count.Tokens},");
            }

            _output.WriteLine("--- MESSAGES ---");
            foreach (var id in MessageShapes.Keys.OrderBy(k => k, StringComparer.Ordinal))
            {
                var count = await CounterFor("cl100k_base")
                    .EstimateTokenCountAsync("probe-messages", MessageShapes[id]);
                _output.WriteLine($"                [\"{id}\"] = {count.Tokens},");
            }

            _output.WriteLine("--- JSON ---");
            foreach (var id in JsonContentShapes.Keys.OrderBy(k => k, StringComparer.Ordinal))
            {
                var count = await CounterFor("cl100k_base")
                    .EstimateTokenCountAsync("probe-json", JsonMessage(id));
                _output.WriteLine($"                [\"{id}\"] = {count.Tokens},");
            }

            _output.WriteLine("--- TOOLS ---");
            foreach (var id in ToolShapes.Keys.OrderBy(k => k, StringComparer.Ordinal))
            {
                var count = await CounterFor("cl100k_base")
                    .EstimateTokenCountAsync("probe-tools", MessageShapes["msgs-single-user"], ToolShapes[id]);
                _output.WriteLine($"                [\"{id}\"] = {count.Tokens},");
            }
        }

        /// <summary>
        /// A remote-URL image can only be priced at the conservative high-detail default —
        /// measuring it would take a network fetch the counting path must not make — so the
        /// count degrades to <see cref="TokenCountFidelity.ApproximateVocabulary"/> and billing
        /// consumers buffer it (#1231, #1233).
        /// </summary>
        [Fact]
        public async Task Image_WithUnknownGeometry_DegradesFidelityToApproximateVocabulary()
        {
            var counter = CounterFor("cl100k_base");

            var actual = await counter.EstimateTokenCountAsync("probe-json", JsonMessage("json-image-only"));

            Assert.Equal(TokenCountFidelity.ApproximateVocabulary, actual.Fidelity);
        }

        /// <summary>
        /// Low-detail images (fixed cost) and base64 images (dimensions readable locally) are
        /// priced by the exact formula, so they must not degrade an otherwise exact count.
        /// </summary>
        [Theory]
        [InlineData("json-image-low-detail")]
        [InlineData("json-image-base64-512")]
        public async Task Image_WithLocallyKnownGeometry_KeepsExactFidelity(string shapeId)
        {
            var counter = CounterFor("cl100k_base");

            var actual = await counter.EstimateTokenCountAsync("probe-json", JsonMessage(shapeId));

            Assert.Equal(TokenCountFidelity.Exact, actual.Fidelity);
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
        /// Builds a data URL holding just a PNG header with the given dimensions — enough for
        /// the counter to read the geometry, with no image body.
        /// </summary>
        private static string PngDataUrl(int width, int height)
        {
            var bytes = new byte[]
            {
                0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, // PNG signature
                0x00, 0x00, 0x00, 0x0D,                         // IHDR chunk length
                0x49, 0x48, 0x44, 0x52,                         // "IHDR"
                (byte)(width >> 24), (byte)(width >> 16), (byte)(width >> 8), (byte)width,
                (byte)(height >> 24), (byte)(height >> 16), (byte)(height >> 8), (byte)height,
            };
            return "data:image/png;base64," + Convert.ToBase64String(bytes);
        }

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
