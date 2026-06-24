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
using Microsoft.OpenApi;

using Xunit;

namespace ConduitLLM.Tests.Admin.OpenApi
{
    /// <summary>
    /// Unit tests for the Tier 2b (#905) <see cref="DefaultErrorResponsesOperationTransformer"/>, which
    /// documents the universal 500 once so controllers can drop ~150
    /// <c>[ProducesResponseType(Status500InternalServerError)]</c> attributes.
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Component", "OpenApi")]
    public class DefaultErrorResponsesOperationTransformerTests
    {
        // The transformer ignores the context, so passing null is safe and keeps the test focused.
        private static async Task TransformAsync(OpenApiOperation operation)
            => await new DefaultErrorResponsesOperationTransformer()
                .TransformAsync(operation, null!, CancellationToken.None);

        [Fact]
        public async Task TransformAsync_AddsA500_WhenAbsent()
        {
            var operation = new OpenApiOperation
            {
                Responses = new OpenApiResponses
                {
                    ["200"] = new OpenApiResponse { Description = "OK" }
                }
            };

            await TransformAsync(operation);

            operation.Responses.Should().ContainKey("500");
            operation.Responses["500"].Description.Should().Contain("ErrorResponseDto");
        }

        [Fact]
        public async Task TransformAsync_InitializesResponses_WhenNull()
        {
            var operation = new OpenApiOperation { Responses = null };

            await TransformAsync(operation);

            operation.Responses.Should().NotBeNull();
            operation.Responses.Should().ContainKey("500");
        }

        [Fact]
        public async Task TransformAsync_DoesNotOverwrite_ExistingTyped500()
        {
            var operation = new OpenApiOperation
            {
                Responses = new OpenApiResponses
                {
                    ["500"] = new OpenApiResponse { Description = "custom-existing" }
                }
            };

            await TransformAsync(operation);

            // A controller that declares its own 500 keeps it — the transformer only fills the gap.
            operation.Responses["500"].Description.Should().Be("custom-existing");
        }
    }

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
                    responses.TryGetProperty("500", out _).Should().BeTrue(
                        $"operation {member.Name.ToUpperInvariant()} {path.Name} should document a 500");
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
