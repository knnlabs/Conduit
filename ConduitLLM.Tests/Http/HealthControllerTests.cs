using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Tests.TestUtilities;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using Xunit;

namespace ConduitLLM.Tests.Http
{
    /// <summary>
    /// Tests for the standardized health check middleware implementation.
    /// Note: The HealthController has been removed in favor of ASP.NET Core Health Checks middleware.
    /// </summary>
    public class HealthControllerTests : IClassFixture<TestWebApplicationFactory<Program>>
    {
        private readonly TestWebApplicationFactory<Program> _factory;

        public HealthControllerTests(TestWebApplicationFactory<Program> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task GetHealth_ReturnsOk_WhenAllServicesAreHealthy()
        {
            // Arrange
            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Test");
                builder.ConfigureTestServices(services =>
                {
                    // Clear existing health check registrations
                    var healthCheckServiceDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(HealthCheckService));
                    if (healthCheckServiceDescriptor != null)
                    {
                        services.Remove(healthCheckServiceDescriptor);
                    }
                    
                    // Override health checks for testing
                    services.AddHealthChecks()
                        .AddCheck("test_database", new TestHealthCheck(HealthStatus.Healthy, "Test database is healthy"))
                        .AddCheck("test_redis", new TestHealthCheck(HealthStatus.Healthy, "Test Redis is healthy"));
                });
            }).CreateClient();

            // Add authentication header
            client.DefaultRequestHeaders.Add("Authorization", "Bearer test-api-key");

            // Act
            var response = await client.GetAsync("/health");

            // Assert
            response.EnsureSuccessStatusCode();
            Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

            var content = await response.Content.ReadAsStringAsync();
            var healthReport = JsonDocument.Parse(content);

            Assert.Equal("Healthy", healthReport.RootElement.GetProperty("status").GetString());
        }

        [Fact]
        public async Task GetHealthReady_ReturnsServiceUnavailable_WhenDatabaseIsUnhealthy()
        {
            // Arrange
            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Test");
                builder.ConfigureTestServices(services =>
                {
                    services.AddHealthChecks()
                        .AddCheck("database",
                            new TestHealthCheck(HealthStatus.Unhealthy, "Cannot connect to database"),
                            failureStatus: HealthStatus.Unhealthy,
                            tags: new[] { "db", "ready" });
                });
            }).CreateClient();

            // Add authentication header
            client.DefaultRequestHeaders.Add("Authorization", "Bearer test-api-key");

            // Act
            var response = await client.GetAsync("/health/ready");

            // Assert
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

            var content = await response.Content.ReadAsStringAsync();
            var healthReport = JsonDocument.Parse(content);

            Assert.Equal("Unhealthy", healthReport.RootElement.GetProperty("status").GetString());

            // Verify the database check details
            var checks = healthReport.RootElement.GetProperty("checks").EnumerateArray();
            var databaseCheck = checks.FirstOrDefault(c => c.GetProperty("name").GetString() == "database");
            Assert.NotEqual(default(JsonElement), databaseCheck);
            Assert.Equal("Unhealthy", databaseCheck.GetProperty("status").GetString());
            Assert.Equal("Cannot connect to database", databaseCheck.GetProperty("description").GetString());
        }

        [Fact]
        public async Task GetHealthReady_ReturnsDegraded_WhenRedisIsUnhealthy()
        {
            // Arrange
            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Test");
                builder.ConfigureTestServices(services =>
                {
                    services.AddHealthChecks()
                        .AddCheck("database",
                            new TestHealthCheck(HealthStatus.Healthy, "Database is healthy"),
                            tags: new[] { "db", "ready" })
                        .AddCheck("redis",
                            new TestHealthCheck(HealthStatus.Degraded, "Redis connection is slow"),
                            failureStatus: HealthStatus.Degraded,
                            tags: new[] { "cache", "ready" });
                });
            }).CreateClient();

            // Add authentication header
            client.DefaultRequestHeaders.Add("Authorization", "Bearer test-api-key");

            // Act
            var response = await client.GetAsync("/health/ready");

            // Assert
            response.EnsureSuccessStatusCode(); // Degraded should return 200

            var content = await response.Content.ReadAsStringAsync();
            var healthReport = JsonDocument.Parse(content);

            Assert.Equal("Degraded", healthReport.RootElement.GetProperty("status").GetString());
        }

        [Fact]
        public async Task GetHealthLive_AlwaysReturnsOk_WhenApplicationIsRunning()
        {
            // Arrange
            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Test");
                builder.ConfigureTestServices(services =>
                {
                    // Even if dependencies are unhealthy, liveness should return OK
                    services.AddHealthChecks()
                        .AddCheck("database",
                            new TestHealthCheck(HealthStatus.Unhealthy, "Database is down"),
                            tags: new[] { "db" })
                        .AddCheck("redis",
                            new TestHealthCheck(HealthStatus.Unhealthy, "Redis is down"),
                            tags: new[] { "cache" });
                });
            }).CreateClient();

            // Act
            var response = await client.GetAsync("/health/live");

            // Assert
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var healthReport = JsonDocument.Parse(content);

            // Live endpoint has no checks, so it should always be healthy
            Assert.Equal("Healthy", healthReport.RootElement.GetProperty("status").GetString());
        }

        [Fact]
        public async Task HealthEndpoint_IncludesProviderHealth_WhenConfigured()
        {
            // Arrange
            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Test");
                builder.ConfigureTestServices(services =>
                {
                    services.AddHealthChecks()
                        .AddCheck("providers",
                            new TestHealthCheck(HealthStatus.Healthy, "All providers are online",
                                data: new Dictionary<string, object>
                                {
                                    { "openai", "online" },
                                    { "anthropic", "online" }
                                }),
                            tags: new[] { "providers", "ready" });
                });
            }).CreateClient();

            // Add authentication header
            client.DefaultRequestHeaders.Add("Authorization", "Bearer test-api-key");

            // Act
            var response = await client.GetAsync("/health");

            // Assert
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var healthReport = JsonDocument.Parse(content);

            var checks = healthReport.RootElement.GetProperty("checks").EnumerateArray();
            var providerCheck = checks.FirstOrDefault(c => c.GetProperty("name").GetString() == "providers");

            Assert.NotEqual(default(JsonElement), providerCheck);
            Assert.Equal("Healthy", providerCheck.GetProperty("status").GetString());

            // Check that provider data is included
            var data = providerCheck.GetProperty("data");
            Assert.Equal("online", data.GetProperty("openai").GetString());
            Assert.Equal("online", data.GetProperty("anthropic").GetString());
        }

        [Fact]
        public async Task HealthEndpoint_ReturnsDetailedError_WhenExceptionOccurs()
        {
            // Arrange
            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Test");
                builder.ConfigureTestServices(services =>
                {
                    services.AddHealthChecks()
                        .AddCheck("failing_check",
                            new FailingHealthCheck(new Exception("Test exception during health check")));
                });
            }).CreateClient();

            // Add authentication header
            client.DefaultRequestHeaders.Add("Authorization", "Bearer test-api-key");

            // Act
            var response = await client.GetAsync("/health");

            // Assert
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

            var content = await response.Content.ReadAsStringAsync();
            var healthReport = JsonDocument.Parse(content);

            Assert.Equal("Unhealthy", healthReport.RootElement.GetProperty("status").GetString());

            var checks = healthReport.RootElement.GetProperty("checks").EnumerateArray();
            var failingCheck = checks.FirstOrDefault(c => c.GetProperty("name").GetString() == "failing_check");

            Assert.NotEqual(default(JsonElement), failingCheck);
            Assert.Equal("Unhealthy", failingCheck.GetProperty("status").GetString());
            Assert.Contains("Test exception", failingCheck.GetProperty("exception").GetString());
        }
    }

    // Test health check implementations
    public class TestHealthCheck : IHealthCheck
    {
        private readonly HealthStatus _status;
        private readonly string _description;
        private readonly Exception? _exception;
        private readonly Dictionary<string, object>? _data;

        public TestHealthCheck(HealthStatus status, string description, Exception? exception = null, Dictionary<string, object>? data = null)
        {
            _status = status;
            _description = description;
            _exception = exception;
            _data = data;
        }

        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            var result = _status switch
            {
                HealthStatus.Healthy => HealthCheckResult.Healthy(_description, _data),
                HealthStatus.Degraded => HealthCheckResult.Degraded(_description, _exception, _data),
                HealthStatus.Unhealthy => HealthCheckResult.Unhealthy(_description, _exception, _data),
                _ => HealthCheckResult.Unhealthy(_description)
            };

            return Task.FromResult(result);
        }
    }

    public class FailingHealthCheck : IHealthCheck
    {
        private readonly Exception _exception;

        public FailingHealthCheck(Exception exception)
        {
            _exception = exception;
        }

        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            throw _exception;
        }
    }
}
