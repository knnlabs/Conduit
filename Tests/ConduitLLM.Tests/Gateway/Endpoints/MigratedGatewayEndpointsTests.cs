using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;

namespace ConduitLLM.Tests.Gateway.Endpoints;

[Trait("Category", "Integration")]
public sealed class MigratedGatewayEndpointsTests
{
    [Fact]
    public async Task BillableEndpoint_RequiresVirtualKeyAuthentication()
    {
        await using var host = await GatewayEndpointTestHost.StartAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/chat/completions");
        request.Headers.Add("X-Test-Anonymous", "true");
        request.Content = JsonContent.Create(new { model = "test", messages = Array.Empty<object>() });

        var response = await host.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ResponsesEndpoint_RequiresVirtualKeyAuthentication()
    {
        await using var host = await GatewayEndpointTestHost.StartAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/responses");
        request.Headers.Add("X-Test-Anonymous", "true");
        request.Content = JsonContent.Create(new
        {
            model = "test",
            input = "hello",
            store = false
        });

        var response = await host.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LegacyCompletions_PreservesNotImplementedResponse()
    {
        await using var host = await GatewayEndpointTestHost.StartAsync();

        var response = await host.Client.PostAsync("/v1/completions", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotImplemented);
    }

    [Fact]
    public async Task MediaGetAndHead_RemainAnonymous()
    {
        await using var host = await GatewayEndpointTestHost.StartAsync();
        using var get = new HttpRequestMessage(HttpMethod.Get, "/v1/conduit/media/missing");
        get.Headers.Add("X-Test-Anonymous", "true");
        using var head = new HttpRequestMessage(HttpMethod.Head, "/v1/conduit/media/missing");
        head.Headers.Add("X-Test-Anonymous", "true");

        var getResponse = await host.Client.SendAsync(get);
        var headResponse = await host.Client.SendAsync(head);

        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        headResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
