using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ConduitLLM.Admin.Security;
using ConduitLLM.Admin.Services;

namespace ConduitLLM.Tests.Admin.Integration
{
    public class EphemeralMasterKeyIntegrationTests : IDisposable
    {
        private readonly TestServer _server;
        private readonly HttpClient _client;
        private readonly IServiceProvider _serviceProvider;

        public EphemeralMasterKeyIntegrationTests()
        {
            var hostBuilder = new HostBuilder()
                .ConfigureWebHost(webHost =>
                {
                    webHost.UseTestServer();
                    webHost.ConfigureServices(services =>
                    {
                        // Set up master key
                        Environment.SetEnvironmentVariable("CONDUIT_API_TO_API_BACKEND_AUTH_KEY", "test-master-key");

                        // Add required services
                        services.AddDistributedMemoryCache(); // Use in-memory cache for testing
                        services.AddSingleton<IEphemeralMasterKeyService, EphemeralMasterKeyService>();
                        
                        // Add authentication
                        services.AddAuthentication("MasterKey")
                            .AddScheme<AuthenticationSchemeOptions, TestMasterKeyAuthenticationHandler>("MasterKey", null);

                        // Add authorization
                        services.AddAuthorization(options =>
                        {
                            options.AddPolicy("MasterKeyPolicy", policy =>
                                policy.Requirements.Add(new MasterKeyRequirement()));
                        });
                        services.AddSingleton<IAuthorizationHandler, MasterKeyAuthorizationHandler>();

                        services.AddControllers();
                        services.AddLogging();
                    });
                    webHost.Configure(app =>
                    {
                        app.UseRouting();
                        app.UseAuthentication();
                        app.UseAuthorization();
                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapControllers();
                        });
                    });
                });

            var host = hostBuilder.Start();
            _server = host.GetTestServer();
            _client = _server.CreateClient();
            _serviceProvider = _server.Services;
        }

        [Fact]
        public async Task EphemeralMasterKey_FullFlow_WorksCorrectly()
        {
            // Step 1: Generate ephemeral master key
            var ephemeralKeyService = _serviceProvider.GetRequiredService<IEphemeralMasterKeyService>();
            var keyResponse = await ephemeralKeyService.CreateEphemeralMasterKeyAsync();
            
            Assert.NotNull(keyResponse);
            Assert.StartsWith("emk_", keyResponse.EphemeralMasterKey);

            // Step 2: Use ephemeral key to access protected endpoint
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/test");
            request.Headers.Add("X-Master-Key", keyResponse.EphemeralMasterKey);

            var response = await _client.SendAsync(request);

            // Step 3: Verify response
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            // Step 4: Verify key is consumed (second use should fail)
            var secondRequest = new HttpRequestMessage(HttpMethod.Get, "/api/test");
            secondRequest.Headers.Add("X-Master-Key", keyResponse.EphemeralMasterKey);

            var secondResponse = await _client.SendAsync(secondRequest);
            Assert.Equal(HttpStatusCode.Unauthorized, secondResponse.StatusCode);
        }

        [Fact]
        public async Task RegularMasterKey_CanBeReused()
        {
            // First request with master key
            var request1 = new HttpRequestMessage(HttpMethod.Get, "/api/test");
            request1.Headers.Add("X-Master-Key", "test-master-key");

            var response1 = await _client.SendAsync(request1);
            Assert.Equal(HttpStatusCode.OK, response1.StatusCode);

            // Second request with same master key
            var request2 = new HttpRequestMessage(HttpMethod.Get, "/api/test");
            request2.Headers.Add("X-Master-Key", "test-master-key");

            var response2 = await _client.SendAsync(request2);
            Assert.Equal(HttpStatusCode.OK, response2.StatusCode);
        }

        [Fact]
        public async Task InvalidEphemeralKey_ReturnsUnauthorized()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/test");
            request.Headers.Add("X-Master-Key", "emk_invalid_key_that_doesnt_exist");

            var response = await _client.SendAsync(request);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task NoKey_ReturnsUnauthorized()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/test");
            // No key header

            var response = await _client.SendAsync(request);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("CONDUIT_API_TO_API_BACKEND_AUTH_KEY", null);
            _client?.Dispose();
            _server?.Dispose();
        }
    }

    // Test controller for integration tests
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "MasterKeyPolicy")]
    public class TestController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get()
        {
            return Ok(new { message = "Success" });
        }
    }

    // Simplified test authentication handler
    public class TestMasterKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        private readonly IEphemeralMasterKeyService _ephemeralMasterKeyService;

        public TestMasterKeyAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            IEphemeralMasterKeyService ephemeralMasterKeyService)
            : base(options, logger, encoder)
        {
            _ephemeralMasterKeyService = ephemeralMasterKeyService;
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            string providedKey = null;

            if (Context.Request.Headers.TryGetValue("X-Master-Key", out var masterKeyValues))
            {
                providedKey = masterKeyValues.FirstOrDefault();
            }
            else if (Context.Request.Headers.TryGetValue("X-API-Key", out var apiKeyValues))
            {
                providedKey = apiKeyValues.FirstOrDefault();
            }

            if (string.IsNullOrEmpty(providedKey))
            {
                return AuthenticateResult.Fail("Missing master key");
            }

            // Check if it's an ephemeral master key
            if (providedKey.StartsWith("emk_", StringComparison.Ordinal))
            {
                var isValid = await _ephemeralMasterKeyService.ValidateAndConsumeKeyAsync(providedKey);
                
                if (!isValid)
                {
                    return AuthenticateResult.Fail("Invalid or expired ephemeral master key");
                }

                var claims = new[]
                {
                    new Claim(ClaimTypes.Name, "AdminUser"),
                    new Claim("MasterKey", "true")
                };

                var identity = new ClaimsIdentity(claims, Scheme.Name);
                var principal = new ClaimsPrincipal(identity);
                var ticket = new AuthenticationTicket(principal, Scheme.Name);

                return AuthenticateResult.Success(ticket);
            }

            // Check regular master key
            var masterKey = Environment.GetEnvironmentVariable("CONDUIT_API_TO_API_BACKEND_AUTH_KEY");
            if (providedKey == masterKey)
            {
                var claims = new[]
                {
                    new Claim(ClaimTypes.Name, "AdminUser"),
                    new Claim("MasterKey", "true")
                };

                var identity = new ClaimsIdentity(claims, Scheme.Name);
                var principal = new ClaimsPrincipal(identity);
                var ticket = new AuthenticationTicket(principal, Scheme.Name);

                return AuthenticateResult.Success(ticket);
            }

            return AuthenticateResult.Fail("Invalid master key");
        }
    }
}