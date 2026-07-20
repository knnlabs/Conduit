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
        Assert.Equal(345, catalogs.Sum(x => x.Models.Count));
        Assert.All(catalogs, catalog =>
        {
            Assert.NotEmpty(catalog.Models);
            Assert.True(catalog.Configuration.ProviderType > 0);
        });
    }
}
