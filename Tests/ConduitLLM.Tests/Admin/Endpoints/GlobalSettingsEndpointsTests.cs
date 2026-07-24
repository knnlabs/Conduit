using System.Net;
using System.Net.Http.Json;

using ConduitLLM.Admin.DTOs;
using ConduitLLM.Admin.Endpoints;
using ConduitLLM.Configuration.Messaging;
using ConduitLLM.Core.Events;

using Microsoft.Extensions.DependencyInjection;

using Moq;

namespace ConduitLLM.Tests.Admin.Endpoints;

public sealed class GlobalSettingsEndpointsTests : IDisposable
{
    private readonly Mock<IEventBus> _eventBus = new();
    private readonly AdminEndpointTestHost _host;

    public GlobalSettingsEndpointsTests()
    {
        _eventBus.Setup(bus => bus.PublishAsync(
                It.IsAny<GlobalSettingsReloadRequested>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _host = AdminEndpointTestHost.Create(
            services => services.AddSingleton(_eventBus.Object),
            endpoints => endpoints.MapGlobalSettingsEndpoints());
    }

    [Fact]
    public async Task Definitions_ReturnServerOwnedTypedRegistry()
    {
        var response = await _host.Client.GetAsync("/v1/admin/global-settings/definitions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<DefinitionPage>();
        Assert.Contains(body!.Data, item =>
            item.Key == "Agentic.MaxIterations"
            && item.Type == "integer"
            && item.Minimum == 1
            && item.Maximum == 100);
    }

    [Fact]
    public async Task Reload_ReturnsAcceptedAndPublishesClusterRequest()
    {
        var response = await _host.Client.PostAsync(
            "/v1/admin/global-settings/cache/reload",
            null);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var body = await response.Content
            .ReadFromJsonAsync<GlobalSettingsReloadAcceptedResponse>();
        Assert.False(string.IsNullOrWhiteSpace(body!.RequestId));
        _eventBus.Verify(bus => bus.PublishAsync(
            It.Is<GlobalSettingsReloadRequested>(request =>
                request.CorrelationId == body.RequestId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    public void Dispose() => _host.Dispose();

    private sealed class DefinitionPage
    {
        public List<GlobalSettingDefinitionDto> Data { get; init; } = [];
    }
}
