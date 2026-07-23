using System.Net;
using System.Net.Http.Json;

using ConduitLLM.Admin.Endpoints;
using ConduitLLM.Configuration.Messaging;
using ConduitLLM.Configuration.ModelCatalogs;
using ConduitLLM.Core.Events;

using Microsoft.Extensions.DependencyInjection;

using Moq;

namespace ConduitLLM.Tests.Admin.Endpoints;

public sealed class BundledModelCatalogEndpointsTests : IDisposable
{
    private readonly Mock<IBundledModelCatalogImporter> _importer = new();
    private readonly Mock<IEventBus> _eventBus = new();
    private readonly AdminEndpointTestHost _host;

    public BundledModelCatalogEndpointsTests()
    {
        _host = AdminEndpointTestHost.Create(services =>
        {
            services.AddSingleton(_importer.Object);
            services.AddSingleton(_eventBus.Object);
        }, endpoints => endpoints.MapBundledModelCatalogEndpoints());
    }

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
        _importer.Setup(importer => importer.ImportAsync(false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var response = await _host.Client.PostAsync("/v1/admin/model-catalogs/import", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<BundledModelCatalogImportResult>();
        Assert.Equal(12, result!.Created.Identifiers);
        _eventBus.Verify(bus => bus.PublishAsync(
            It.Is<DiscoveryCacheInvalidationRequested>(request => request.Reason.Contains("Bundled")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    public void Dispose() => _host.Dispose();
}
