using ConduitLLM.Admin.Controllers;
using ConduitLLM.Configuration.Messaging;
using ConduitLLM.Configuration.ModelCatalogs;
using ConduitLLM.Core.Events;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using Moq;

namespace ConduitLLM.Tests.Admin.Controllers;

public sealed class BundledModelCatalogControllerTests
{
    [Fact]
    public async Task Import_ReturnsCountsAndInvalidatesDiscoveryWhenIdentifiersWereCreated()
    {
        var expected = new BundledModelCatalogImportResult
        {
            ProvidersProcessed = 5,
            ModelsDiscovered = 345,
            Created = new CatalogImportCounts { Identifiers = 12 },
            SkippedExistingIdentifiers = 333
        };
        var importer = new Mock<IBundledModelCatalogImporter>();
        importer.Setup(x => x.ImportAsync(false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        var eventBus = new Mock<IEventBus>();
        var controller = new BundledModelCatalogController(
            importer.Object,
            eventBus.Object,
            Mock.Of<ILogger<BundledModelCatalogController>>());

        var response = await controller.Import(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        Assert.Same(expected, ok.Value);
        eventBus.Verify(
            x => x.PublishAsync(
                It.Is<DiscoveryCacheInvalidationRequested>(e => e.Reason.Contains("Bundled", StringComparison.Ordinal)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
