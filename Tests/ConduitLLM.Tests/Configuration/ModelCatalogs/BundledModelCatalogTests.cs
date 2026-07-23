using ConduitLLM.Configuration.ModelCatalogs;

namespace ConduitLLM.Tests.Configuration.ModelCatalogs;

public sealed class BundledModelCatalogTests
{
    [Fact]
    public async Task LoadAsync_IncludesEveryConfiguredProviderAndModel()
    {
        var catalogs = await new BundledModelCatalog().LoadAsync();

        Assert.Equal(
            ["cerebras", "groq", "meta", "openrouter", "sambanova"],
            catalogs.Select(x => x.Name).ToArray());
        Assert.Equal(372, catalogs.Sum(x => x.Models.Count));
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

        var metaVideoReader = catalogs.Single(catalog => catalog.Name == "meta").Models["muse-spark-1.1"];
        Assert.Contains("video", metaVideoReader.InputModalities!);
        Assert.DoesNotContain("video", metaVideoReader.OutputModalities!);
    }
}
