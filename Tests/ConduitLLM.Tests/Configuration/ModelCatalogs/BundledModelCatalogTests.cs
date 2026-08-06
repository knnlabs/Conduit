using ConduitLLM.Configuration.ModelCatalogs;

namespace ConduitLLM.Tests.Configuration.ModelCatalogs;

public sealed class BundledModelCatalogTests
{
    [Fact]
    public async Task LoadAsync_IncludesEveryConfiguredProviderAndModel()
    {
        var catalogs = await new BundledModelCatalog().LoadAsync();

        Assert.Equal(
            ["cerebras", "cloudflare", "groq", "meta", "openrouter", "sambanova"],
            catalogs.Select(x => x.Name).ToArray());
        Assert.Equal(445, catalogs.Sum(x => x.Models.Count));
        Assert.All(catalogs, catalog =>
        {
            Assert.NotEmpty(catalog.Models);
            Assert.True(catalog.Configuration.ProviderType > 0);
        });

        var openRouter = catalogs.Single(catalog => catalog.Name == "openrouter");
        Assert.Equal(343, openRouter.Models.Count);
        Assert.All(openRouter.Models.Values, model =>
        {
            Assert.NotNull(model.InputModalities);
            Assert.NotNull(model.OutputModalities);
        });

        var cloudflare = catalogs.Single(catalog => catalog.Name == "cloudflare");
        Assert.Equal(73, cloudflare.Models.Count);
        Assert.All(cloudflare.Models.Keys, identifier => Assert.StartsWith("@", identifier));
        var cloudflareLlama = cloudflare.Models["@cf/meta/llama-3.3-70b-instruct-fp8-fast"];
        Assert.True(cloudflareLlama.SupportsChat);
        Assert.True(cloudflareLlama.SupportsFunctionCalling);
        var cloudflareWhisper = cloudflare.Models["@cf/openai/whisper-large-v3-turbo"];
        Assert.True(cloudflareWhisper.SupportsSpeechToText);
        Assert.Contains("audio", cloudflareWhisper.InputModalities!);

        var metaVideoReader = catalogs.Single(catalog => catalog.Name == "meta").Models["muse-spark-1.1"];
        Assert.Contains("video", metaVideoReader.InputModalities!);
        Assert.DoesNotContain("video", metaVideoReader.OutputModalities!);
    }
}
