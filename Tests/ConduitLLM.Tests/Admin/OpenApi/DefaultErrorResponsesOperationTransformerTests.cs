using System.Net;
using System.Text.Json;

using ConduitLLM.Admin.OpenApi;

using FluentAssertions;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace ConduitLLM.Tests.Admin.OpenApi
{
    /// <summary>
    /// Runtime verification (the "spot-check <c>/openapi/v1.json</c>" gap) that the same transformer
    /// registered in <c>Program.cs</c> actually injects a documented 500 into every operation of a
    /// generated OpenAPI document — across both Minimal-API and controller-style operations.
    /// </summary>
    [Trait("Category", "Integration")]
    [Trait("Component", "OpenApi")]
    public class DefaultErrorResponses500DocumentationTests : IDisposable
    {
        private static readonly string[] HttpMethods =
            { "get", "put", "post", "delete", "options", "head", "patch", "trace" };

        private readonly TestServer _server;
        private readonly HttpClient _client;

        public DefaultErrorResponses500DocumentationTests()
        {
            var host = new HostBuilder()
                .ConfigureWebHost(webHost =>
                {
                    webHost.UseTestServer();
                    webHost.ConfigureServices(services =>
                    {
                        services.AddLogging();
                        services.AddRouting();
                        services.AddEndpointsApiExplorer();
                        services.AddOpenApi("v1", options =>
                            options.AddOperationTransformer<DefaultErrorResponsesOperationTransformer>());
                    });
                    webHost.Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(endpoints =>
                        {
                            // Representative operations with varied explicit responses — none declares a 500.
                            endpoints.MapGet("/things", () => Results.Ok(new[] { "a" }));
                            endpoints.MapGet("/things/{id:int}", (int id) => Results.Ok(id));
                            endpoints.MapPost("/things", (SampleDto dto) => Results.Created("/things/1", dto));
                            endpoints.MapDelete("/things/{id:int}", (int id) => Results.NoContent());

                            endpoints.MapOpenApi();
                        });
                    });
                })
                .Start();

            _server = host.GetTestServer();
            _client = _server.CreateClient();
        }

        [Fact]
        public async Task GeneratedDocument_DocumentsA500_OnEveryOperation()
        {
            var response = await _client.GetAsync("/openapi/v1.json");
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var paths = doc.RootElement.GetProperty("paths");

            var operationCount = 0;
            foreach (var path in paths.EnumerateObject())
            {
                foreach (var member in path.Value.EnumerateObject())
                {
                    if (!HttpMethods.Contains(member.Name))
                    {
                        continue;
                    }

                    operationCount++;
                    member.Value.TryGetProperty("responses", out var responses).Should().BeTrue(
                        $"operation {member.Name.ToUpperInvariant()} {path.Name} should have responses");
                    responses.TryGetProperty("500", out var errorResponse).Should().BeTrue(
                        $"operation {member.Name.ToUpperInvariant()} {path.Name} should document a 500");
                    var schema = errorResponse.GetProperty("content")
                        .GetProperty("application/json")
                        .GetProperty("schema");
                    var properties = schema.GetProperty("properties");
                    properties.TryGetProperty("error", out _).Should().BeTrue();
                    properties.TryGetProperty("details", out _).Should().BeTrue();
                    properties.TryGetProperty("code", out _).Should().BeTrue();
                }
            }

            operationCount.Should().BeGreaterThan(0, "the document should contain operations to verify");
        }

        public void Dispose()
        {
            _client.Dispose();
            _server.Dispose();
        }

        private sealed class SampleDto
        {
            public string Name { get; set; } = string.Empty;
        }
    }
}
