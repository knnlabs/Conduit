using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace ConduitLLM.Tests.Http
{
    public class HealthCheckIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public HealthCheckIntegrationTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task Health_ReturnsOk_WithJsonResponse()
        {
            // Arrange
            var client = _factory.CreateClient();

            // Act
            var response = await client.GetAsync("/health");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

            var content = await response.Content.ReadAsStringAsync();
            var healthResponse = JsonDocument.Parse(content);
            
            Assert.True(healthResponse.RootElement.TryGetProperty("status", out var status));
            Assert.Contains(status.GetString(), new[] { "Healthy", "Degraded", "Unhealthy" });
        }

        [Fact]
        public async Task HealthReady_ReturnsOk_WithJsonResponse()
        {
            // Arrange
            var client = _factory.CreateClient();

            // Act
            var response = await client.GetAsync("/health/ready");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

            var content = await response.Content.ReadAsStringAsync();
            var healthResponse = JsonDocument.Parse(content);
            
            Assert.True(healthResponse.RootElement.TryGetProperty("status", out var status));
            Assert.True(healthResponse.RootElement.TryGetProperty("results", out var results));
        }

        [Fact]
        public async Task HealthLive_AlwaysReturnsHealthy()
        {
            // Arrange
            var client = _factory.CreateClient();

            // Act
            var response = await client.GetAsync("/health/live");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

            var content = await response.Content.ReadAsStringAsync();
            var healthResponse = JsonDocument.Parse(content);
            
            Assert.True(healthResponse.RootElement.TryGetProperty("status", out var status));
            Assert.Equal("Healthy", status.GetString());
        }

        [Fact]
        public async Task Health_IncludesDetailedCheckResults()
        {
            // Arrange
            var client = _factory.CreateClient();

            // Act
            var response = await client.GetAsync("/health");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var content = await response.Content.ReadAsStringAsync();
            var healthResponse = JsonDocument.Parse(content);
            
            // Check for expected properties in the response
            Assert.True(healthResponse.RootElement.TryGetProperty("status", out _));
            Assert.True(healthResponse.RootElement.TryGetProperty("totalDuration", out _));
            Assert.True(healthResponse.RootElement.TryGetProperty("results", out var results));
            
            // Should have at least database check
            Assert.True(results.TryGetProperty("database", out var databaseCheck));
            Assert.True(databaseCheck.TryGetProperty("status", out _));
            Assert.True(databaseCheck.TryGetProperty("duration", out _));
        }
    }
}