using ConduitLLM.Gateway.Endpoints;

using FluentAssertions;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;

namespace ConduitLLM.Tests.Gateway.Endpoints;

[Trait("Category", "Unit")]
[Trait("Component", "Gateway")]
public sealed class GatewayInternalOperationsEndpointsTests
{
    [Fact]
    public async Task EveryInternalOperationRequiresBackendAdminAuthAndIsExcludedFromOpenApi()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        var app = builder.Build();
        app.MapGatewayInternalOperationsEndpoints();

        await app.StartAsync();
        try
        {
            var endpoints = ((IEndpointRouteBuilder)app).DataSources
                .SelectMany(source => source.Endpoints)
                .OfType<RouteEndpoint>()
                .Where(endpoint =>
                    endpoint.RoutePattern.RawText?.StartsWith(
                        "/internal/", StringComparison.Ordinal) == true)
                .ToList();

            endpoints.Should().HaveCount(20);
            foreach (var endpoint in endpoints)
            {
                endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>()
                    .Should().Contain(item => item.Policy == "AdminOnly");
                endpoint.Metadata.GetMetadata<IAllowAnonymous>()
                    .Should().BeNull();
                endpoint.Metadata.GetMetadata<IExcludeFromDescriptionMetadata>()
                    .Should().NotBeNull();
            }
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }
}
