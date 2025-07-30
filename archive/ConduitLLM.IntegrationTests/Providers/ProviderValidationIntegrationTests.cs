using System.Threading.Tasks;
using ConduitLLM.Configuration;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.IntegrationTests.Infrastructure;
using ConduitLLM.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace ConduitLLM.IntegrationTests.Providers
{
    /// <summary>
    /// Integration tests for provider validation flow.
    /// These tests verify the entire flow from factory creation to authentication verification.
    /// </summary>
    [Trait("Category", "Integration")]
    [Trait("Component", "ProviderValidation")]
    public class ProviderValidationIntegrationTests : IntegrationTestBase
    {
        private ILLMClientFactory _clientFactory;
        private ILogger<ProviderValidationIntegrationTests> _logger;

        public ProviderValidationIntegrationTests(ITestOutputHelper output) : base(output)
        {
        }

        protected override Task OnInitializeAsync()
        {
            _clientFactory = GetService<ILLMClientFactory>();
            _logger = GetService<ILogger<ProviderValidationIntegrationTests>>();
            return Task.CompletedTask;
        }

        [IntegrationFact]
        public async Task ValidateOpenAIProvider_WithTestClient_HandlesUrlsCorrectly()
        {
            // This test verifies that the OpenAI provider validation works correctly
            // when using the test client creation flow
            
            if (!IntegrationTestConditions.IsOpenAIConfigured())
            {
                _logger.LogInformation("Skipping test - OpenAI credentials not configured");
                return;
            }

            // Arrange
            var credentials = new ProviderCredentials
            {
                ApiKey = IntegrationTestConditions.GetOpenAIApiKey(),
                ProviderType = ProviderType.OpenAI,
                BaseUrl = null // Let it use the default
            };

            // Act
            _logger.LogInformation("Creating test client for OpenAI validation");
            var testClient = _clientFactory.CreateTestClient(credentials);
            
            // Check if the client implements IAuthenticationVerifiable
            if (testClient is not IAuthenticationVerifiable authVerifiable)
            {
                _logger.LogWarning("OpenAI client does not implement IAuthenticationVerifiable");
                Assert.Fail("OpenAI client should implement IAuthenticationVerifiable");
                return;
            }
            
            _logger.LogInformation("Verifying authentication");
            var result = await authVerifiable.VerifyAuthenticationAsync(
                IntegrationTestConditions.GetOpenAIApiKey(), 
                null);

            // Assert
            Assert.NotNull(result);
            _logger.LogInformation("Authentication result: Success={Success}, Message={Message}", 
                result.IsSuccess, result.Message);
            
            // The test should either succeed (with valid API key) or fail gracefully
            // What we're really testing is that it doesn't throw an exception about invalid URIs
            if (!result.IsSuccess)
            {
                // If it fails, it should be due to invalid API key, not URI issues
                Assert.DoesNotContain("invalid request URI", result.ErrorDetails?.ToLower() ?? "");
                Assert.DoesNotContain("BaseAddress must be set", result.ErrorDetails ?? "");
            }
        }

        [IntegrationFact]
        public async Task ValidateOpenAIProvider_WithCustomBaseUrl_HandlesUrlsCorrectly()
        {
            if (!IntegrationTestConditions.IsOpenAIConfigured())
            {
                _logger.LogInformation("Skipping test - OpenAI credentials not configured");
                return;
            }

            // Arrange
            var customBaseUrl = "https://api.openai.com/v1"; // Explicit base URL
            var credentials = new ProviderCredentials
            {
                ApiKey = IntegrationTestConditions.GetOpenAIApiKey(),
                ProviderType = ProviderType.OpenAI,
                BaseUrl = customBaseUrl
            };

            // Act
            var testClient = _clientFactory.CreateTestClient(credentials);
            
            // Check if the client implements IAuthenticationVerifiable
            if (testClient is not IAuthenticationVerifiable authVerifiable)
            {
                _logger.LogWarning("OpenAI client does not implement IAuthenticationVerifiable");
                Assert.Fail("OpenAI client should implement IAuthenticationVerifiable");
                return;
            }
            
            var result = await authVerifiable.VerifyAuthenticationAsync(
                IntegrationTestConditions.GetOpenAIApiKey(), 
                customBaseUrl);

            // Assert
            Assert.NotNull(result);
            
            // Should not have URI-related errors
            if (!result.IsSuccess)
            {
                Assert.DoesNotContain("invalid request URI", result.ErrorDetails?.ToLower() ?? "");
                Assert.DoesNotContain("BaseAddress must be set", result.ErrorDetails ?? "");
            }
        }

        [Fact]
        public async Task ValidateOpenAIProvider_WithInvalidApiKey_ReturnsAuthenticationError()
        {
            // This test verifies that invalid API keys are handled correctly
            // and don't cause URI-related errors
            
            // Arrange
            var credentials = new ProviderCredentials
            {
                ApiKey = "sk-invalid-test-key-12345",
                ProviderType = ProviderType.OpenAI,
                BaseUrl = null
            };

            // Act
            var testClient = _clientFactory.CreateTestClient(credentials);
            
            // Check if the client implements IAuthenticationVerifiable
            if (testClient is not IAuthenticationVerifiable authVerifiable)
            {
                _logger.LogWarning("OpenAI client does not implement IAuthenticationVerifiable");
                Assert.Fail("OpenAI client should implement IAuthenticationVerifiable");
                return;
            }
            
            var result = await authVerifiable.VerifyAuthenticationAsync(
                "sk-invalid-test-key-12345", 
                null);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.IsSuccess);
            
            // Should fail due to invalid API key, not URI issues
            Assert.DoesNotContain("invalid request URI", result.ErrorDetails?.ToLower() ?? "");
            Assert.DoesNotContain("BaseAddress must be set", result.ErrorDetails ?? "");
            
            // Should contain authentication-related error
            Assert.True(
                result.Message.Contains("Authentication failed", System.StringComparison.OrdinalIgnoreCase) ||
                result.Message.Contains("Invalid API key", System.StringComparison.OrdinalIgnoreCase) ||
                result.ErrorDetails?.Contains("401") == true ||
                result.ErrorDetails?.Contains("Unauthorized") == true,
                $"Expected authentication error but got: {result.Message} / {result.ErrorDetails}");
        }
    }
}