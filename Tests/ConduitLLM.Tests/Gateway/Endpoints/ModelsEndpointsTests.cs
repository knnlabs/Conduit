using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;

using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Gateway.Endpoints;
using ConduitLLM.Gateway.Services;

using FluentAssertions;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;

namespace ConduitLLM.Tests.Gateway.Endpoints;

[Trait("Category", "Integration")]
[Trait("Component", "Models")]
public sealed class ModelsEndpointsTests : IDisposable
{
    private readonly Mock<IModelProviderMappingRepository> _repository = new();
    private readonly Mock<IModelMetadataService> _metadata = new();
    private readonly TestServer _server;
    private readonly HttpClient _client;

    public ModelsEndpointsTests()
    {
        var host = new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.ConfigureServices(services =>
                {
                    services.AddLogging();
                    services.AddRouting();
                    services.AddSingleton(_repository.Object);
                    services.AddSingleton(_metadata.Object);
                    services.AddAuthentication("VirtualKey")
                        .AddScheme<AuthenticationSchemeOptions, VirtualKeyTestHandler>(
                            "VirtualKey", null);
                    services.AddAuthorization(options =>
                        options.AddPolicy("VirtualKeyAuthentication", policy =>
                        {
                            policy.AuthenticationSchemes.Add("VirtualKey");
                            policy.RequireAuthenticatedUser();
                        }));
                });
                webHost.Configure(app =>
                {
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints => endpoints.MapModelsEndpoints());
                });
            })
            .Start();

        _server = host.GetTestServer();
        _client = _server.CreateClient();
    }

    [Fact]
    public async Task ListModels_ReturnsDistinctOpenAIModelEnvelope()
    {
        _repository.Setup(repository => repository.GetPaginatedAsync(
                1, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<ModelProviderMapping>
            {
                new() { ModelAlias = "gpt-4o" },
                new() { ModelAlias = "gpt-4o" },
                new() { ModelAlias = "claude-3" }
            }, 3));

        var response = await _client.GetAsync("/v1/models");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("object").GetString().Should().Be("list");
        json.RootElement.GetProperty("data").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task GetMetadata_WhenMissing_ReturnsOpenAI404()
    {
        _metadata.Setup(service => service.GetModelMetadataAsync("missing"))
            .ReturnsAsync((object?)null);

        var response = await _client.GetAsync("/v1/models/missing/metadata");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var error = json.RootElement.GetProperty("error");
        error.GetProperty("code").GetString().Should().Be("model_not_found");
        error.GetProperty("type").GetString().Should().Be("invalid_request_error");
    }

    public void Dispose()
    {
        _client.Dispose();
        _server.Dispose();
    }

    private sealed class VirtualKeyTestHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public VirtualKeyTestHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new ClaimsIdentity(new[]
            {
                new Claim("VirtualKeyId", "1"),
                new Claim("VirtualKey", "test-key")
            }, Scheme.Name);
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
