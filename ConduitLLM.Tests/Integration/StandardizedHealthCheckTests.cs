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

namespace ConduitLLM.Tests.Integration
{
    public class StandardizedHealthCheckTests : IClassFixture<TestWebApplicationFactory<Program>>
    {
        private readonly TestWebApplicationFactory<Program> _factory;

        public StandardizedHealthCheckTests(TestWebApplicationFactory<Program> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task HealthEndpoint_ReturnsOk_WhenApplicationIsHealthy()
        {
            // Arrange
            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    // Override health checks with test implementations
                    services.AddHealthChecks()
                        .AddCheck("database", new TestHealthCheck(HealthStatus.Healthy, "Test database is healthy"), tags: new[] { "db", "ready" })
                        .AddCheck("providers", new TestHealthCheck(HealthStatus.Healthy, "Test providers are healthy"), tags: new[] { "providers", "ready" })
                        .AddCheck("redis", new TestHealthCheck(HealthStatus.Healthy, "Test Redis is healthy"), tags: new[] { "cache", "ready" });
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
        public async Task HealthLiveEndpoint_ReturnsOk_WhenApplicationIsRunning()
        {
            // Arrange
            var client = _factory.CreateClient();

            // Act
            var response = await client.GetAsync("/health/live");

            // Assert
            response.EnsureSuccessStatusCode();
            Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

            var content = await response.Content.ReadAsStringAsync();
            var healthReport = JsonDocument.Parse(content);

            // Live endpoint should always return Healthy if the app is running
            Assert.Equal("Healthy", healthReport.RootElement.GetProperty("status").GetString());
        }

        [Fact]
        public async Task HealthReadyEndpoint_ReturnsOk_WhenDependenciesAreHealthy()
        {
            // Arrange
            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development"); // Use Development to enable health checks
                builder.ConfigureTestServices(services =>
                {
                    // Clear any existing health check registrations
                    var healthCheckServiceDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(HealthCheckService));
                    if (healthCheckServiceDescriptor != null)
                    {
                        services.Remove(healthCheckServiceDescriptor);
                    }
                    
                    // Override health checks with test implementations
                    services.AddHealthChecks()
                        .AddCheck("test_db", new TestHealthCheck(HealthStatus.Healthy, "Database is healthy"), tags: new[] { "ready" })
                        .AddCheck("test_redis", new TestHealthCheck(HealthStatus.Healthy, "Redis is healthy"), tags: new[] { "ready" });
                });
            }).CreateClient();

            // Add authentication header
            client.DefaultRequestHeaders.Add("Authorization", "Bearer test-api-key");

            // Act
            var response = await client.GetAsync("/health/ready");

            // Assert
            response.EnsureSuccessStatusCode();
            Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

            var content = await response.Content.ReadAsStringAsync();
            var healthReport = JsonDocument.Parse(content);

            Assert.Equal("Healthy", healthReport.RootElement.GetProperty("status").GetString());
        }

        [Fact]
        public async Task HealthReadyEndpoint_ReturnsServiceUnavailable_WhenDatabaseIsUnhealthy()
        {
            // Arrange
            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    // Clear existing health checks
                    services.AddHealthChecks()
                        .AddCheck("database", new TestHealthCheck(HealthStatus.Unhealthy, "Database connection failed"),
                            failureStatus: HealthStatus.Unhealthy,
                            tags: new[] { "db", "sql", "ready" });
                });
            }).CreateClient();

            // Add authentication header
            client.DefaultRequestHeaders.Add("Authorization", "Bearer test-api-key");

            // Act
            var response = await client.GetAsync("/health/ready");

            // Assert
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

            var content = await response.Content.ReadAsStringAsync();
            var healthReport = JsonDocument.Parse(content);

            Assert.Equal("Unhealthy", healthReport.RootElement.GetProperty("status").GetString());

            // Check that the database check is included in the response
            var checks = healthReport.RootElement.GetProperty("checks").EnumerateArray();
            Assert.Contains(checks, check =>
                check.GetProperty("name").GetString() == "database" &&
                check.GetProperty("status").GetString() == "Unhealthy");
        }

        [Fact]
        public async Task HealthReadyEndpoint_ReturnsDegraded_WhenNonCriticalServiceIsUnhealthy()
        {
            // Arrange
            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddHealthChecks()
                        .AddCheck("database", new TestHealthCheck(HealthStatus.Healthy, "Database is healthy"),
                            tags: new[] { "db", "ready" })
                        .AddCheck("redis", new TestHealthCheck(HealthStatus.Degraded, "Redis is slow but working"),
                            failureStatus: HealthStatus.Degraded,
                            tags: new[] { "cache", "ready" });
                });
            }).CreateClient();

            // Add authentication header
            client.DefaultRequestHeaders.Add("Authorization", "Bearer test-api-key");

            // Act
            var response = await client.GetAsync("/health/ready");

            // Assert
            response.EnsureSuccessStatusCode(); // Degraded should still return 200
            Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

            var content = await response.Content.ReadAsStringAsync();
            var healthReport = JsonDocument.Parse(content);

            Assert.Equal("Degraded", healthReport.RootElement.GetProperty("status").GetString());
        }

        [Fact]
        public async Task HealthEndpoint_IncludesAllChecks_InResponse()
        {
            // Arrange
            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddHealthChecks()
                        .AddCheck("database", new TestHealthCheck(HealthStatus.Healthy, "Database is healthy"))
                        .AddCheck("redis", new TestHealthCheck(HealthStatus.Healthy, "Redis is healthy"))
                        .AddCheck("providers", new TestHealthCheck(HealthStatus.Healthy, "All providers are healthy"));
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
            var checkNames = new List<string>();

            foreach (var check in checks)
            {
                checkNames.Add(check.GetProperty("name").GetString() ?? string.Empty);
            }

            Assert.Contains("database", checkNames);
            Assert.Contains("redis", checkNames);
            Assert.Contains("providers", checkNames);
        }

        [Fact]
        public async Task HealthEndpoint_IncludesDurationMetrics()
        {
            // Arrange
            var client = _factory.CreateClient();

            // Add authentication header
            client.DefaultRequestHeaders.Add("Authorization", "Bearer test-api-key");

            // Act
            var response = await client.GetAsync("/health");

            // Assert
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var healthReport = JsonDocument.Parse(content);

            // Check that totalDuration is included
            Assert.True(healthReport.RootElement.TryGetProperty("totalDuration", out var totalDuration));
            Assert.True(totalDuration.GetDouble() >= 0);

            // Check that individual check durations are included
            var checks = healthReport.RootElement.GetProperty("checks").EnumerateArray();
            foreach (var check in checks)
            {
                Assert.True(check.TryGetProperty("duration", out var duration));
                Assert.True(duration.GetDouble() >= 0);
            }
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
}
