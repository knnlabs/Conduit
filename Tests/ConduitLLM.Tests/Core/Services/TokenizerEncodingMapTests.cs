using ConduitLLM.Core.Services;

using Xunit;

namespace ConduitLLM.Tests.Core.Services
{
    /// <summary>
    /// Unit tests for <see cref="TokenizerEncodingMap"/> (#1051).
    /// </summary>
    /// <remarks>
    /// Model metadata surfaces <c>TokenizerType.ToString()</c>, so these tests use the enum
    /// names exactly as <c>DatabaseModelCapabilityService.GetTokenizerTypeAsync</c> emits them.
    /// </remarks>
    public class TokenizerEncodingMapTests
    {
        // The five encodings whose vocabulary ships in a referenced
        // Microsoft.ML.Tokenizers.Data.* package. Any other value would send the counter back
        // down the exception path this issue removed.
        private static readonly string[] SupportedEncodings =
            { "cl100k_base", "p50k_base", "p50k_edit", "r50k_base", "o200k_base" };

        [Theory]
        [InlineData("Cl100KBase", "cl100k_base")]
        [InlineData("P50KBase", "p50k_base")]
        [InlineData("P50KEdit", "p50k_edit")]
        [InlineData("R50KBase", "r50k_base")]
        [InlineData("O200KBase", "o200k_base")]
        public void Resolve_OpenAiEncoding_ReturnsExactEncoding(string tokenizerType, string expected)
        {
            var result = TokenizerEncodingMap.Resolve(tokenizerType);

            Assert.Equal(expected, result.EncodingName);
            Assert.False(result.IsApproximation);
            Assert.True(result.IsRecognized);
        }

        [Theory]
        [InlineData("Claude", "cl100k_base")]
        [InlineData("Claude3", "cl100k_base")]
        [InlineData("Gemini", "cl100k_base")]
        [InlineData("LLaMA3", "cl100k_base")]
        [InlineData("Mistral", "cl100k_base")]
        [InlineData("Kimi", "cl100k_base")]
        [InlineData("O200KHarmony", "o200k_base")]
        public void Resolve_NonTiktokenTokenizer_ReturnsDocumentedApproximation(string tokenizerType, string expected)
        {
            var result = TokenizerEncodingMap.Resolve(tokenizerType);

            Assert.Equal(expected, result.EncodingName);
            Assert.True(result.IsApproximation);
            Assert.True(result.IsRecognized);
        }

        [Fact]
        public void Resolve_EveryTokenizerType_MapsToASupportedEncoding()
        {
            foreach (TokenizerType tokenizerType in Enum.GetValues<TokenizerType>())
            {
                var result = TokenizerEncodingMap.Resolve(tokenizerType.ToString());

                Assert.True(result.IsRecognized, $"{tokenizerType} is not mapped");
                Assert.Contains(result.EncodingName, SupportedEncodings);
            }
        }

        [Theory]
        [InlineData("cl100k_base")]
        [InlineData("CL100K_BASE")]
        [InlineData("  Cl100KBase  ")]
        [InlineData("cl100kbase")]
        public void Resolve_IsCaseAndWhitespaceInsensitive(string tokenizerType)
        {
            var result = TokenizerEncodingMap.Resolve(tokenizerType);

            Assert.Equal("cl100k_base", result.EncodingName);
            Assert.True(result.IsRecognized);
        }

        [Theory]
        [InlineData("o200k_harmony", "o200k_base")]
        public void Resolve_UnimplementedEncodingIdentifier_ReturnsNearestSupported(string encoding, string expected)
        {
            var result = TokenizerEncodingMap.Resolve(encoding);

            Assert.Equal(expected, result.EncodingName);
            Assert.True(result.IsApproximation);
            Assert.True(result.IsRecognized);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Resolve_MissingTokenizer_ReturnsDefaultEncoding(string? tokenizerType)
        {
            var result = TokenizerEncodingMap.Resolve(tokenizerType);

            Assert.Equal(TokenizerEncodingMap.DefaultEncoding, result.EncodingName);
            Assert.True(result.IsRecognized);
        }

        [Fact]
        public void Resolve_UnknownTokenizer_ReportsUnrecognizedAndFallsBack()
        {
            var result = TokenizerEncodingMap.Resolve("some-tokenizer-that-does-not-exist");

            Assert.Equal(TokenizerEncodingMap.DefaultEncoding, result.EncodingName);
            Assert.True(result.IsApproximation);
            Assert.False(result.IsRecognized);
        }

        [Fact]
        public void Resolve_ResultIsIdempotent()
        {
            // Feeding a resolved encoding name back in must not degrade it — this is what
            // happens when configuration is written from a previous resolution.
            foreach (TokenizerType tokenizerType in Enum.GetValues<TokenizerType>())
            {
                var first = TokenizerEncodingMap.Resolve(tokenizerType.ToString());
                var second = TokenizerEncodingMap.Resolve(first.EncodingName);

                Assert.Equal(first.EncodingName, second.EncodingName);
                Assert.True(second.IsRecognized);
            }
        }
    }
}
