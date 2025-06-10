using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ConduitLLM.Admin.Tests
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
            
            Assert.True(healthResponse.RootElement.TryGetProperty("status", out _));
            Assert.True(healthResponse.RootElement.TryGetProperty("results", out _));
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
        public async Task HealthAudio_ReturnsOk_WithAudioCheckResults()
        {
            // Arrange
            var client = _factory.CreateClient();

            // Act
            var response = await client.GetAsync("/health/audio");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

            var content = await response.Content.ReadAsStringAsync();
            var healthResponse = JsonDocument.Parse(content);
            
            // Should have audio-specific health check results
            Assert.True(healthResponse.RootElement.TryGetProperty("status", out _));
            Assert.True(healthResponse.RootElement.TryGetProperty("results", out _));
        }

        [Fact]
        public async Task Health_IncludesAllConfiguredChecks()
        {
            // Arrange
            var client = _factory.CreateClient();

            // Act
            var response = await client.GetAsync("/health");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var content = await response.Content.ReadAsStringAsync();
            var healthResponse = JsonDocument.Parse(content);
            
            Assert.True(healthResponse.RootElement.TryGetProperty("results", out var results));
            
            // Should include database and providers checks at minimum
            Assert.True(results.TryGetProperty("database", out _));
            Assert.True(results.TryGetProperty("providers", out _));
        }
    }
}