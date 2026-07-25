using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;

using Microsoft.Extensions.Logging;

using Moq;

using Xunit;

namespace ConduitLLM.Tests.Core.Services
{
    /// <summary>
    /// Verifies that <see cref="TiktokenCounter"/> resolves tokenizers without exception-driven
    /// control flow, and that a resolution is attempted at most once per tokenizer (#1051).
    /// </summary>
    public class TiktokenCounterEncodingTests
    {
        private static readonly List<Message> Messages = new()
        {
            new Message { Role = MessageRole.User, Content = "The quick brown fox jumps over the lazy dog." }
        };

        private static (TiktokenCounter Counter, Mock<ILogger<TiktokenCounter>> Logger) CreateCounter(string? tokenizerType)
        {
            var logger = new Mock<ILogger<TiktokenCounter>>();
            var capabilities = new Mock<IModelCapabilityService>();
            capabilities
                .Setup(c => c.GetTokenizerTypeAsync(It.IsAny<string>()))
                .ReturnsAsync(tokenizerType);

            return (new TiktokenCounter(logger.Object, capabilities.Object), logger);
        }

        private static int CountLogs(Mock<ILogger<TiktokenCounter>> logger, LogLevel level) =>
            logger.Invocations.Count(i =>
                i.Method.Name == nameof(ILogger.Log) && (LogLevel)i.Arguments[0] == level);

        [Theory]
        [InlineData("Cl100KBase")]
        [InlineData("O200KBase")]
        [InlineData("P50KBase")]
        [InlineData("Claude")]
        [InlineData("Gemini")]
        [InlineData("LLaMA3")]
        [InlineData("O200KHarmony")]
        [InlineData("R50KBase")]
        public async Task EstimateTokenCountAsync_KnownTokenizer_CountsWithoutWarningOrError(string tokenizerType)
        {
            var (counter, logger) = CreateCounter(tokenizerType);

            var count = await counter.EstimateTokenCountAsync($"model-{tokenizerType}", Messages);

            Assert.True(count.Tokens > 0, $"{tokenizerType} produced no tokens");
            Assert.NotEqual(TokenCountFidelity.CharacterHeuristic, count.Fidelity);
            Assert.Equal(0, CountLogs(logger, LogLevel.Warning));
            Assert.Equal(0, CountLogs(logger, LogLevel.Error));
        }

        [Fact]
        public async Task EstimateTokenCountAsync_DefaultTokenizer_MatchesExplicitCl100KBase()
        {
            var (defaulted, _) = CreateCounter("Cl100KBase");
            var (approximated, _) = CreateCounter("LLaMA3");

            var exact = await defaulted.EstimateTokenCountAsync("model-exact", Messages);
            var approximate = await approximated.EstimateTokenCountAsync("model-approx", Messages);

            // LLaMA3 has no Tiktoken equivalent and is documented as a cl100k_base approximation,
            // so the two must agree — a divergence would mean the approximation silently changed.
            Assert.Equal(exact.Tokens, approximate.Tokens);

            // Same number, different trust: the stand-in vocabulary must be labeled as such (#1233).
            Assert.Equal(TokenCountFidelity.Exact, exact.Fidelity);
            Assert.Equal(TokenCountFidelity.ApproximateVocabulary, approximate.Fidelity);
        }

        [Theory]
        [InlineData("Cl100KBase", TokenCountFidelity.Exact)]
        [InlineData("P50KBase", TokenCountFidelity.Exact)]
        [InlineData("P50KEdit", TokenCountFidelity.Exact)]
        [InlineData("R50KBase", TokenCountFidelity.Exact)]
        [InlineData("O200KBase", TokenCountFidelity.Exact)]
        [InlineData("Claude3", TokenCountFidelity.ApproximateVocabulary)]
        [InlineData("Gemini", TokenCountFidelity.ApproximateVocabulary)]
        [InlineData("LLaMA3", TokenCountFidelity.ApproximateVocabulary)]
        [InlineData("O200KHarmony", TokenCountFidelity.ApproximateVocabulary)]
        [InlineData("None", TokenCountFidelity.ApproximateVocabulary)]
        public async Task EstimateTokenCountAsync_ReportsFidelityMatchingTheResolutionTier(
            string tokenizerType, TokenCountFidelity expected)
        {
            var (counter, _) = CreateCounter(tokenizerType);

            var count = await counter.EstimateTokenCountAsync($"model-{tokenizerType}", Messages);

            Assert.Equal(expected, count.Fidelity);
        }

        [Fact]
        public async Task EstimateTokenCountAsync_UnknownTokenizer_WarnsOnceAcrossManyCalls()
        {
            // A tokenizer name no TokenizerType parses to, unique to this test so it gets its
            // own slot in TiktokenCounter's process-wide encoding cache.
            const string Unknown = "conduit-test-unknown-tokenizer-1051";
            var (counter, logger) = CreateCounter(Unknown);

            for (var i = 0; i < 25; i++)
            {
                var count = await counter.EstimateTokenCountAsync("model-unknown", Messages);
                Assert.True(count.Tokens > 0);
                Assert.Equal(TokenCountFidelity.ApproximateVocabulary, count.Fidelity);
            }

            // Before #1051 the failed resolution was cached under the *resolved* name while the
            // lookup used the *requested* name, so every call re-threw and re-logged.
            Assert.Equal(1, CountLogs(logger, LogLevel.Warning));
            Assert.Equal(0, CountLogs(logger, LogLevel.Error));
        }
    }
}
