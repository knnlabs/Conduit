using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;

using ConduitLLM.Admin.Endpoints;
using ConduitLLM.Admin.Middleware;
using ConduitLLM.Admin.Models.ModelAuthors;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Converters;

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

using Xunit;

namespace ConduitLLM.Tests.Admin.Integration
{
    /// <summary>
    /// HTTP-level integration tests for the Tier 3 Minimal-API <see cref="ModelAuthorEndpoints"/>
    /// (#906), which replaced the MVC <c>ModelAuthorController</c>. These lock down the migrated
    /// behavior end-to-end — routing, model binding, the not-found/conflict branches, and the
    /// throw → <c>AdminExceptionMiddleware</c> → standardized <c>ErrorResponseDto</c> mapping that
    /// the endpoints rely on (previously only unit-level controller coverage, now none existed).
    /// </summary>
    /// <remarks>
    /// Uses an in-process <see cref="TestServer"/> with the real <see cref="AdminExceptionMiddleware"/>,
    /// the real <see cref="OperationLoggingEndpointFilter"/>, an always-authenticated test scheme, and
    /// a mocked <see cref="IModelAuthorRepository"/>. This mirrors the existing
    /// <c>EphemeralMasterKeyIntegrationTests</c> harness pattern (the repo has no
    /// <c>WebApplicationFactory&lt;Program&gt;</c> suite — Program runs Postgres migrations inline at startup).
    /// </remarks>
    [Trait("Category", "Integration")]
    [Trait("Component", "ModelAuthor")]
    public class ModelAuthorEndpointsTests : IDisposable
    {
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        private readonly Mock<IModelAuthorRepository> _repository = new();
        private readonly TestServer _server;
        private readonly HttpClient _client;

        public ModelAuthorEndpointsTests()
        {
            var host = new HostBuilder()
                .ConfigureWebHost(webHost =>
                {
                    webHost.UseTestServer();
                    webHost.UseEnvironment("Production");
                    webHost.ConfigureServices(services =>
                    {
                        services.AddLogging();
                        services.AddRouting();
                        services.AddSingleton(_repository.Object);
                        services.ConfigureHttpJsonOptions(options =>
                        {
                            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                            options.SerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
                            options.SerializerOptions.PropertyNameCaseInsensitive = true;
                            options.SerializerOptions.Converters.Add(new UtcDateTimeConverter());
                            options.SerializerOptions.Converters.Add(new NullableUtcDateTimeConverter());
                        });

                        services.AddAuthentication("Test")
                            .AddScheme<AuthenticationSchemeOptions, AlwaysAuthenticatedHandler>("Test", null);
                        services.AddAuthorization(options =>
                            options.AddPolicy("MasterKeyPolicy", policy => policy.RequireAuthenticatedUser()));
                    });
                    webHost.Configure(app =>
                    {
                        app.UseRouting();
                        // Real global handler: endpoint throws map to ErrorResponseDto exactly as in production.
                        app.UseAdminExceptionHandling();
                        app.UseAuthentication();
                        app.UseAuthorization();
                        app.UseEndpoints(endpoints => endpoints.MapModelAuthorEndpoints());
                    });
                })
                .Start();

            _server = host.GetTestServer();
            _client = _server.CreateClient();
        }

        // ---- GetAll -------------------------------------------------------------------------

        [Fact]
        public async Task GetAll_ReturnsOk_WithMappedAuthors()
        {
            _repository
                .Setup(r => r.GetPaginatedAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((new List<ModelAuthor>
                {
                    new() { Id = 1, Name = "OpenAI" },
                    new() { Id = 2, Name = "Anthropic" }
                }, 2));

            var response = await _client.GetAsync("/api/ModelAuthor");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var authors = await response.Content.ReadFromJsonAsync<List<ModelAuthorDto>>(Json);
            authors.Should().HaveCount(2);
            authors!.Select(a => a.Name).Should().Contain(new[] { "OpenAI", "Anthropic" });
        }

        // ---- GetById ------------------------------------------------------------------------

        [Fact]
        public async Task GetById_WhenFound_ReturnsOk()
        {
            _repository
                .Setup(r => r.GetByIdAsync(7, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ModelAuthor { Id = 7, Name = "Meta", WebsiteUrl = "https://ai.meta.com" });

            var response = await _client.GetAsync("/api/ModelAuthor/7");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var author = await response.Content.ReadFromJsonAsync<ModelAuthorDto>(Json);
            author!.Id.Should().Be(7);
            author.Name.Should().Be("Meta");
        }

        [Fact]
        public async Task GetById_SerializesDtoDatesAsCamelCaseUtc()
        {
            _repository
                .Setup(r => r.GetByIdAsync(7, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ModelAuthor
                {
                    Id = 7,
                    Name = "Meta"
                });

            var response = await _client.GetAsync("/api/ModelAuthor/7");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            body.RootElement.GetProperty("createdAt").GetString()
                .Should().Be("0001-01-01T00:00:00Z");
            body.RootElement.GetProperty("updatedAt").GetString()
                .Should().Be("0001-01-01T00:00:00Z");
        }

        [Fact]
        public async Task GetById_WhenMissing_Returns404_NotFoundCode()
        {
            _repository
                .Setup(r => r.GetByIdAsync(999, It.IsAny<CancellationToken>()))
                .ReturnsAsync((ModelAuthor?)null);

            var response = await _client.GetAsync("/api/ModelAuthor/999");

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await ReadCodeAsync(response)).Should().Be("not_found");
        }

        // ---- GetSeriesByAuthor --------------------------------------------------------------

        [Fact]
        public async Task GetSeriesByAuthor_WhenFound_ReturnsOk_WithSeries()
        {
            _repository
                .Setup(r => r.GetSeriesByAuthorAsync(3, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ModelSeries>
                {
                    new() { Id = 10, Name = "GPT", Description = "d", TokenizerType = TokenizerType.None }
                });

            var response = await _client.GetAsync("/api/ModelAuthor/3/series");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var series = await response.Content.ReadFromJsonAsync<List<SimpleModelSeriesDto>>(Json);
            series.Should().ContainSingle(s => s.Name == "GPT" && s.Id == 10);
        }

        [Fact]
        public async Task GetSeriesByAuthor_WhenAuthorMissing_Returns404()
        {
            _repository
                .Setup(r => r.GetSeriesByAuthorAsync(999, It.IsAny<CancellationToken>()))
                .ReturnsAsync((List<ModelSeries>?)null);

            var response = await _client.GetAsync("/api/ModelAuthor/999/series");

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await ReadCodeAsync(response)).Should().Be("not_found");
        }

        // ---- Create -------------------------------------------------------------------------

        [Fact]
        public async Task Create_WhenNameUnique_Returns201_WithLocationAndBody()
        {
            _repository
                .Setup(r => r.GetByNameAsync("Mistral AI", It.IsAny<CancellationToken>()))
                .ReturnsAsync((ModelAuthor?)null);
            _repository
                .Setup(r => r.CreateAsync(It.IsAny<ModelAuthor>(), It.IsAny<CancellationToken>()))
                .Callback<ModelAuthor, CancellationToken>((a, _) => a.Id = 42)
                .ReturnsAsync(42);

            var response = await _client.PostAsJsonAsync("/api/ModelAuthor",
                new CreateModelAuthorDto { Name = "Mistral AI", WebsiteUrl = "https://mistral.ai" });

            response.StatusCode.Should().Be(HttpStatusCode.Created);
            response.Headers.Location!.ToString().Should().EndWith("/api/ModelAuthor/42");
            var author = await response.Content.ReadFromJsonAsync<ModelAuthorDto>(Json);
            author!.Id.Should().Be(42);
            author.Name.Should().Be("Mistral AI");
            _repository.Verify(r => r.CreateAsync(It.IsAny<ModelAuthor>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Create_WhenNameExists_Returns400_InvalidOperation()
        {
            _repository
                .Setup(r => r.GetByNameAsync("OpenAI", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ModelAuthor { Id = 1, Name = "OpenAI" });

            var response = await _client.PostAsJsonAsync("/api/ModelAuthor",
                new CreateModelAuthorDto { Name = "OpenAI" });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await ReadCodeAsync(response)).Should().Be("invalid_operation");
            _repository.Verify(r => r.CreateAsync(It.IsAny<ModelAuthor>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        // ---- Update -------------------------------------------------------------------------

        [Fact]
        public async Task Update_WhenIdMismatch_Returns400()
        {
            var response = await _client.PutAsJsonAsync("/api/ModelAuthor/1",
                new UpdateModelAuthorDto { Id = 2, Name = "X" });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            _repository.Verify(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Update_WhenMissing_Returns404()
        {
            _repository
                .Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>()))
                .ReturnsAsync((ModelAuthor?)null);

            var response = await _client.PutAsJsonAsync("/api/ModelAuthor/5",
                new UpdateModelAuthorDto { Id = 5, Name = "X" });

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await ReadCodeAsync(response)).Should().Be("not_found");
        }

        [Fact]
        public async Task Update_WhenValid_Returns204_AndPersists()
        {
            _repository
                .Setup(r => r.GetByIdAsync(8, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ModelAuthor { Id = 8, Name = "Cohere" });
            _repository
                .Setup(r => r.UpdateAsync(It.IsAny<ModelAuthor>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Same name → skips the name-conflict branch; only Description changes.
            var response = await _client.PutAsJsonAsync("/api/ModelAuthor/8",
                new UpdateModelAuthorDto { Id = 8, Name = "Cohere", Description = "Enterprise LLMs" });

            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
            _repository.Verify(r => r.UpdateAsync(
                It.Is<ModelAuthor>(a => a.Id == 8 && a.Description == "Enterprise LLMs"),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        // ---- Delete -------------------------------------------------------------------------

        [Fact]
        public async Task Delete_WhenMissing_Returns404()
        {
            _repository
                .Setup(r => r.GetByIdAsync(404, It.IsAny<CancellationToken>()))
                .ReturnsAsync((ModelAuthor?)null);

            var response = await _client.DeleteAsync("/api/ModelAuthor/404");

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await ReadCodeAsync(response)).Should().Be("not_found");
        }

        [Fact]
        public async Task Delete_WhenAuthorHasSeries_Returns400_InvalidOperation()
        {
            _repository
                .Setup(r => r.GetByIdAsync(6, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ModelAuthor { Id = 6, Name = "Meta" });
            _repository
                .Setup(r => r.GetSeriesByAuthorAsync(6, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ModelSeries> { new() { Id = 1, Name = "Llama" } });

            var response = await _client.DeleteAsync("/api/ModelAuthor/6");

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await ReadCodeAsync(response)).Should().Be("invalid_operation");
            _repository.Verify(r => r.DeleteAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Delete_WhenValid_Returns204()
        {
            _repository
                .Setup(r => r.GetByIdAsync(9, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ModelAuthor { Id = 9, Name = "Stability AI" });
            _repository
                .Setup(r => r.GetSeriesByAuthorAsync(9, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ModelSeries>());
            _repository
                .Setup(r => r.DeleteAsync(9, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var response = await _client.DeleteAsync("/api/ModelAuthor/9");

            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
            _repository.Verify(r => r.DeleteAsync(9, It.IsAny<CancellationToken>()), Times.Once);
        }

        /// <summary>Reads the <c>code</c> field from an <c>ErrorResponseDto</c> body, tolerant of casing.</summary>
        private static async Task<string?> ReadCodeAsync(HttpResponseMessage response)
        {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            foreach (var name in new[] { "code", "Code" })
            {
                if (doc.RootElement.TryGetProperty(name, out var value))
                {
                    return value.GetString();
                }
            }

            return null;
        }

        public void Dispose()
        {
            _client.Dispose();
            _server.Dispose();
        }
    }

    /// <summary>Test authentication handler that always succeeds, satisfying the MasterKeyPolicy.</summary>
    internal sealed class AlwaysAuthenticatedHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public AlwaysAuthenticatedHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "TestAdmin") }, Scheme.Name);
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
