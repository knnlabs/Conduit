using System.Net;
using System.Net.Http.Json;
using ConduitLLM.Configuration;
using ConduitLLM.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace ConduitLLM.IntegrationTests.Tests;

/// <summary>
/// Basic integration tests to verify the test infrastructure works correctly.
/// </summary>
public class BasicIntegrationTests : IntegrationTestBase
{
    private readonly SimpleTestDataBuilder _dataBuilder;

    public BasicIntegrationTests(ITestOutputHelper output) : base(output)
    {
        _dataBuilder = new SimpleTestDataBuilder();
    }

    protected override bool UseRealInfrastructure => false; // Use in-memory for basic tests

    [IntegrationFact]
    public async Task HealthCheck_ReturnsHealthy()
    {
        // Act
        var response = await Client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        Output.WriteLine($"Health check response: {content}");
        content.Should().Contain("Healthy");
    }

    [IntegrationFact]
    public async Task TestInfrastructure_DatabaseConnection_Works()
    {
        // Arrange
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ConfigurationDbContext>();

        // Act
        var canConnect = await context.Database.CanConnectAsync();

        // Assert
        canConnect.Should().BeTrue();
        Output.WriteLine("Database connection successful");
    }

    [IntegrationFact]
    public void TestInfrastructure_TestDataBuilder_CreatesValidData()
    {
        // Act
        var (apiKey, keyHash, keyData) = _dataBuilder.CreateVirtualKeyData("Test Key", 100m);
        var chatRequest = _dataBuilder.CreateChatRequest();

        // Assert
        apiKey.Should().NotBeNull();
        apiKey.Should().StartWith("ck-");
        apiKey.Should().HaveLength(51); // ck- prefix + 48 chars

        keyHash.Should().NotBeNull();
        keyHash.Should().HaveLength(44); // Base64 encoded SHA256

        chatRequest.Should().NotBeNull();
    }

    [IntegrationFact]
    public async Task MockLLMServer_RespondsToChatCompletion()
    {
        // Arrange
        using var mockServer = new MockLLMServer();
        mockServer.SetupChatCompletion("Hello from mock server!");

        var httpClient = new HttpClient { BaseAddress = new Uri(mockServer.BaseUrl) };

        // Act
        var request = new
        {
            model = "gpt-3.5-turbo",
            messages = new[]
            {
                new { role = "user", content = "Test message" }
            }
        };

        var response = await httpClient.PostAsJsonAsync("/v1/chat/completions", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Hello from mock server!");

        mockServer.GetRequestCount("/v1/chat/completions").Should().Be(1);
    }

    [IntegrationFact]
    public async Task ApiAuthentication_WithoutKey_ReturnsUnauthorized()
    {
        // Act
        var response = await Client.GetAsync("/v1/models");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [IntegrationFact]
    public async Task ApiAuthentication_WithInvalidKey_ReturnsUnauthorized()
    {
        // Arrange
        var client = CreateAuthenticatedClient("invalid-key-12345");

        // Act
        var response = await client.GetAsync("/v1/models");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}