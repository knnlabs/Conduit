using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Xunit;
using Xunit.Abstractions;
using ConduitLLM.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;

namespace ConduitLLM.IntegrationTests.Api
{
    /// <summary>
    /// Integration tests for Admin API authentication and authorization.
    /// Verifies that all endpoints properly enforce Master Key authentication.
    /// </summary>
    [Trait("Category", "Integration")]
    [Trait("Component", "AdminApi")]
    [Trait("Focus", "Authentication")]
    public class AdminApiAuthenticationTests : IntegrationTestBase
    {
        private readonly string _validMasterKey = "test-master-key-12345";

        public AdminApiAuthenticationTests(ITestOutputHelper output) : base(output)
        {
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // Configure the Admin API with master key
            builder.ConfigureServices(services =>
            {
                // Override configuration with master key
                var configuration = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string>
                    {
                        ["CONDUIT_API_TO_API_BACKEND_AUTH_KEY"] = _validMasterKey,
                        ["AdminApi:MasterKey"] = _validMasterKey
                    })
                    .Build();

                services.AddSingleton<IConfiguration>(configuration);
            });
        }

        #region Health Endpoints (Should Allow Anonymous)

        [Theory]
        [InlineData("/health")]
        [InlineData("/health/live")]
        [InlineData("/health/ready")]
        public async Task HealthEndpoints_ShouldAllowAnonymousAccess(string endpoint)
        {
            // Act - Call without authentication header
            var response = await Client.GetAsync(endpoint);

            // Assert
            Output.WriteLine($"Endpoint: {endpoint}, Status: {response.StatusCode}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        #endregion

        #region IP Filter Check Endpoint (Should Allow Anonymous)

        [Fact]
        public async Task IpFilterCheck_ShouldAllowAnonymousAccess()
        {
            // Arrange
            var factory = CreateFactory(includeMasterKey: true);
            var client = factory.CreateClient();

            // Act
            var response = await client.GetAsync("/api/ipfilter/check/127.0.0.1");

            // Assert
            _output.WriteLine($"IP Filter Check Status: {response.StatusCode}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        #endregion

        #region Protected Endpoints Tests

        [Theory]
        [InlineData("/api/admin/virtualkeys", "GET")]
        [InlineData("/api/admin/providercredentials", "GET")]
        [InlineData("/api/admin/globalsettings", "GET")]
        [InlineData("/api/admin/dashboard", "GET")]
        [InlineData("/api/admin/configuration", "GET")]
        [InlineData("/v1/admin/tasks/cleanup", "POST")]
        [InlineData("/metrics", "GET")]
        [InlineData("/metrics/database/pool", "GET")]
        [InlineData("/api/admin/providertypes", "GET")]
        [InlineData("/api/admin/providertypes/diagnostics", "GET")]
        public async Task ProtectedEndpoints_WithoutAuth_ShouldReturn401(string endpoint, string method)
        {
            // Arrange
            var factory = CreateFactory(includeMasterKey: true);
            var client = factory.CreateClient();
            var request = new HttpRequestMessage(new HttpMethod(method), endpoint);

            // Act - Call without authentication header
            var response = await client.SendAsync(request);

            // Assert
            _output.WriteLine($"Endpoint: {endpoint}, Method: {method}, Status: {response.StatusCode}");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Theory]
        [InlineData("/api/admin/virtualkeys", "GET")]
        [InlineData("/api/admin/providercredentials", "GET")]
        [InlineData("/api/admin/globalsettings", "GET")]
        [InlineData("/api/admin/dashboard", "GET")]
        [InlineData("/api/admin/configuration", "GET")]
        [InlineData("/v1/admin/tasks/cleanup", "POST")]
        [InlineData("/metrics", "GET")]
        [InlineData("/metrics/database/pool", "GET")]
        [InlineData("/api/admin/providertypes", "GET")]
        [InlineData("/api/admin/providertypes/diagnostics", "GET")]
        public async Task ProtectedEndpoints_WithInvalidAuth_ShouldReturn401(string endpoint, string method)
        {
            // Arrange
            var factory = CreateFactory(includeMasterKey: true);
            var client = factory.CreateClient();
            var request = new HttpRequestMessage(new HttpMethod(method), endpoint);
            request.Headers.Add("X-API-Key", "invalid-key");

            // Act
            var response = await client.SendAsync(request);

            // Assert
            _output.WriteLine($"Endpoint: {endpoint}, Method: {method}, Status: {response.StatusCode}");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Theory]
        [InlineData("/api/admin/virtualkeys", "GET")]
        [InlineData("/api/admin/providercredentials", "GET")]
        [InlineData("/api/admin/globalsettings", "GET")]
        [InlineData("/api/admin/dashboard", "GET")]
        [InlineData("/api/admin/configuration", "GET")]
        [InlineData("/metrics", "GET")]
        [InlineData("/metrics/database/pool", "GET")]
        [InlineData("/api/admin/providertypes", "GET")]
        [InlineData("/api/admin/providertypes/diagnostics", "GET")]
        public async Task ProtectedEndpoints_WithValidAuth_ShouldSucceed(string endpoint, string method)
        {
            // Arrange
            var factory = CreateFactory(includeMasterKey: true);
            var client = factory.CreateClient();
            var request = new HttpRequestMessage(new HttpMethod(method), endpoint);
            request.Headers.Add("X-API-Key", _validMasterKey);

            // Act
            var response = await client.SendAsync(request);

            // Assert
            _output.WriteLine($"Endpoint: {endpoint}, Method: {method}, Status: {response.StatusCode}");
            // We expect 200 OK or 204 No Content for successful authentication
            // Some endpoints might return 404 if resources don't exist, which is fine
            Assert.True(
                response.StatusCode == HttpStatusCode.OK || 
                response.StatusCode == HttpStatusCode.NoContent ||
                response.StatusCode == HttpStatusCode.NotFound,
                $"Expected success status code, but got {response.StatusCode}");
        }

        #endregion

        #region Authentication Header Tests

        [Fact]
        public async Task Authentication_WithHeaderInQueryString_ShouldSucceed()
        {
            // Arrange
            var factory = CreateFactory(includeMasterKey: true);
            var client = factory.CreateClient();

            // Act - Pass API key in query string
            var response = await client.GetAsync($"/api/admin/configuration?apiKey={_validMasterKey}");

            // Assert
            _output.WriteLine($"Query String Auth Status: {response.StatusCode}");
            Assert.True(
                response.StatusCode == HttpStatusCode.OK || 
                response.StatusCode == HttpStatusCode.NoContent,
                $"Expected success status code, but got {response.StatusCode}");
        }

        [Fact]
        public async Task Authentication_WithAuthorizationBearer_ShouldFail()
        {
            // Arrange
            var factory = CreateFactory(includeMasterKey: true);
            var client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _validMasterKey);

            // Act
            var response = await client.GetAsync("/api/admin/configuration");

            // Assert
            _output.WriteLine($"Bearer Auth Status: {response.StatusCode}");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        #endregion

        #region Controller-Specific Tests

        [Fact]
        public async Task TasksController_CleanupEndpoint_RequiresAuthentication()
        {
            // Arrange
            var factory = CreateFactory(includeMasterKey: true);
            var client = factory.CreateClient();

            // Act - Without auth
            var responseWithoutAuth = await client.PostAsync("/v1/admin/tasks/cleanup", null);
            
            // Act - With auth
            var requestWithAuth = new HttpRequestMessage(HttpMethod.Post, "/v1/admin/tasks/cleanup");
            requestWithAuth.Headers.Add("X-API-Key", _validMasterKey);
            var responseWithAuth = await client.SendAsync(requestWithAuth);

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, responseWithoutAuth.StatusCode);
            Assert.True(
                responseWithAuth.StatusCode == HttpStatusCode.OK || 
                responseWithAuth.StatusCode == HttpStatusCode.NoContent,
                $"Expected success status code, but got {responseWithAuth.StatusCode}");
        }

        [Fact]
        public async Task MetricsController_AllEndpoints_RequireAuthentication()
        {
            // Arrange
            var factory = CreateFactory(includeMasterKey: true);
            var client = factory.CreateClient();
            var endpoints = new[] { "/metrics", "/metrics/database/pool" };

            foreach (var endpoint in endpoints)
            {
                // Act - Without auth
                var responseWithoutAuth = await client.GetAsync(endpoint);
                
                // Act - With auth
                var requestWithAuth = new HttpRequestMessage(HttpMethod.Get, endpoint);
                requestWithAuth.Headers.Add("X-API-Key", _validMasterKey);
                var responseWithAuth = await client.SendAsync(requestWithAuth);

                // Assert
                _output.WriteLine($"Testing {endpoint}");
                Assert.Equal(HttpStatusCode.Unauthorized, responseWithoutAuth.StatusCode);
                Assert.True(
                    responseWithAuth.StatusCode == HttpStatusCode.OK || 
                    responseWithAuth.StatusCode == HttpStatusCode.NoContent,
                    $"Expected success status code for {endpoint}, but got {responseWithAuth.StatusCode}");
            }
        }

        [Fact]
        public async Task ProviderTypesController_AllEndpoints_RequireAuthentication()
        {
            // Arrange
            var factory = CreateFactory(includeMasterKey: true);
            var client = factory.CreateClient();
            var endpoints = new[] 
            { 
                "/api/admin/providertypes",
                "/api/admin/providertypes/diagnostics",
                "/api/admin/providertypes/OpenAI/capabilities",
                "/api/admin/providertypes/OpenAI/auth-requirements",
                "/api/admin/providertypes/OpenAI/configuration-hints",
                "/api/admin/providertypes/by-feature/chat"
            };

            foreach (var endpoint in endpoints)
            {
                // Act - Without auth
                var responseWithoutAuth = await client.GetAsync(endpoint);
                
                // Act - With auth
                var requestWithAuth = new HttpRequestMessage(HttpMethod.Get, endpoint);
                requestWithAuth.Headers.Add("X-API-Key", _validMasterKey);
                var responseWithAuth = await client.SendAsync(requestWithAuth);

                // Assert
                _output.WriteLine($"Testing {endpoint}");
                Assert.Equal(HttpStatusCode.Unauthorized, responseWithoutAuth.StatusCode);
                // Some endpoints might return 404 for non-existent resources, which is fine
                Assert.True(
                    responseWithAuth.StatusCode == HttpStatusCode.OK || 
                    responseWithAuth.StatusCode == HttpStatusCode.NoContent ||
                    responseWithAuth.StatusCode == HttpStatusCode.NotFound,
                    $"Expected success status code for {endpoint}, but got {responseWithAuth.StatusCode}");
            }
        }

        #endregion
    }
}