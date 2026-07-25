using ConduitLLM.Configuration.ModelCatalogs;

namespace ConduitLLM.Tests.Configuration.ModelCatalogs;

/// <summary>
/// Tests for #1232: tokenizer metadata must not silently degrade. The importer's mapping
/// distinguishes explicit "None" from unrecognized strings, and the bundled catalogs may
/// only ship values the importer recognizes.
/// </summary>
public sealed class BundledModelCatalogTokenizerTests
{
    [Theory]
    [InlineData("Cl100KBase", TokenizerType.Cl100KBase)]
    [InlineData("cl100k_base", TokenizerType.Cl100KBase)]
    [InlineData("O200KBase", TokenizerType.O200KBase)]
    [InlineData("O200KHarmony", TokenizerType.O200KHarmony)]
    [InlineData("Claude3", TokenizerType.Claude3)]
    [InlineData("Gemini", TokenizerType.Gemini)]
    [InlineData("LLaMA3", TokenizerType.LLaMA3)]
    [InlineData("Mistral", TokenizerType.Mistral)]
    [InlineData("Kimi", TokenizerType.Kimi)]
    [InlineData("MiniMax", TokenizerType.MiniMax)]
    [InlineData("Cohere", TokenizerType.Cohere)]
    [InlineData("BPE", TokenizerType.BPE)]
    [InlineData("Tiktoken", TokenizerType.Tiktoken)]
    public void MapTokenizer_RecognizedValue_Maps(string value, TokenizerType expected)
    {
        Assert.Equal(expected, BundledModelCatalogImporter.MapTokenizer(value));
    }

    [Theory]
    [InlineData("None")]
    [InlineData("")]
    [InlineData(null)]
    public void MapTokenizer_ExplicitlyUnset_MapsToNone(string? value)
    {
        Assert.Equal(TokenizerType.None, BundledModelCatalogImporter.MapTokenizer(value));
    }

    [Theory]
    [InlineData("Cl100kBase")] // wrong casing = typo, not a silent None
    [InlineData("o200k_harmony")]
    [InlineData("SomeNewVocabulary")]
    public void MapTokenizer_UnrecognizedValue_ReturnsNullSoTheImportWarns(string value)
    {
        Assert.Null(BundledModelCatalogImporter.MapTokenizer(value));
    }

    [Fact]
    public async Task BundledCatalogs_ShipOnlyRecognizedTokenizerValues()
    {
        var catalogs = await new BundledModelCatalog().LoadAsync();

        Assert.All(
            catalogs.SelectMany(catalog => catalog.Models.Select(entry => (catalog.Name, entry))),
            item => Assert.True(
                BundledModelCatalogImporter.MapTokenizer(item.entry.Value.TokenizerType) is not null,
                $"{item.Name}/{item.entry.Key}: unrecognized tokenizerType '{item.entry.Value.TokenizerType}'"));
    }

    [Fact]
    public async Task BundledCatalogs_NoneIsReservedForModelsWithoutAKnownFamily()
    {
        // #1232 backfilled the family-inferable "None" entries. This pins the audited
        // residue so a catalog refresh that regresses the backfill fails loudly. The
        // remaining None entries are non-text models, meta-routers, and vocabularies
        // OpenRouter itself reports as "Other".
        var catalogs = await new BundledModelCatalog().LoadAsync();

        var noneTextChatModels = catalogs
            .SelectMany(catalog => catalog.Models.Select(entry => (catalog.Name, entry)))
            .Where(item => item.entry.Value.TokenizerType == "None" && item.entry.Value.SupportsChat)
            .Select(item => $"{item.Name}/{item.entry.Key}")
            .ToList();

        // Families with a Conduit TokenizerType must not appear here — they belong to the
        // slug-inference backfill (see fetch-openrouter-models.cs InferTokenizerFromId).
        Assert.All(noneTextChatModels, id =>
        {
            var lower = id.ToLowerInvariant();
            Assert.False(
                lower.Contains("kimi") || lower.Contains("minimax") || lower.Contains("llama") ||
                lower.Contains("claude") || lower.Contains("gemma") || lower.Contains("gemini") ||
                lower.Contains("mistral") || lower.Contains("qwen") || lower.Contains("deepseek") ||
                lower.Contains("grok"),
                $"{id} has tokenizerType None but names a family with a known TokenizerType");
        });
    }
}
