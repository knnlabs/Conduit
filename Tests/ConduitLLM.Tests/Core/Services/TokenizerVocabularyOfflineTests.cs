using ConduitLLM.Core.Services;

using Microsoft.ML.Tokenizers;

using Xunit;

namespace ConduitLLM.Tests.Core.Services
{
    /// <summary>
    /// Guards the offline guarantee behind #1227: every encoding the resolution table can hand to
    /// the counter must construct from vocabulary data bundled with the application, with no
    /// network access and no files written outside the build output.
    /// </summary>
    /// <remarks>
    /// TiktokenSharp downloaded its BPE files from Azure Blob Storage on first use; when that
    /// fetch failed, billing silently degraded to a chars/4 estimate. Microsoft.ML.Tokenizers
    /// ships each vocabulary in a <c>Microsoft.ML.Tokenizers.Data.*</c> package instead. This
    /// test fails if a <see cref="TokenizerEncodingMap"/> entry is added without referencing the
    /// matching data package, or if a future tokenizer change reintroduces a runtime download.
    /// </remarks>
    public class TokenizerVocabularyOfflineTests
    {
        [Fact]
        public void EveryResolvableEncoding_ConstructsFromBundledData()
        {
            foreach (TokenizerType tokenizerType in Enum.GetValues<TokenizerType>())
            {
                var resolved = TokenizerEncodingMap.Resolve(tokenizerType.ToString());

                // Throws (failing the test) when the encoding's data package is not referenced.
                var tokenizer = TiktokenTokenizer.CreateForEncoding(resolved.EncodingName);

                Assert.True(tokenizer.CountTokens("offline vocabulary check") > 0,
                    $"{resolved.EncodingName} produced no tokens for {tokenizerType}");
            }
        }

        [Fact]
        public void ConstructingEncodings_LeavesNoDownloadArtifacts()
        {
            foreach (var encoding in new[] { "cl100k_base", "p50k_base", "p50k_edit", "r50k_base", "o200k_base" })
            {
                TiktokenTokenizer.CreateForEncoding(encoding).CountTokens("offline vocabulary check");
            }

            // TiktokenSharp materialized downloaded vocabularies into a bpe/ directory beside the
            // binaries (or the temp directory when that was read-only). Neither may reappear.
            var bpeDirectory = Path.Combine(AppContext.BaseDirectory, "bpe");
            Assert.False(Directory.Exists(bpeDirectory),
                $"Tokenizer construction created {bpeDirectory}; vocabulary data must ship in the package, not download at runtime");
        }
    }
}
