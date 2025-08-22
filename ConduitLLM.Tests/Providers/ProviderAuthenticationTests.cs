using System.Net;
using System.Text;
using System.Text.Json;

using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Providers.OpenAI;

using Moq;
using Moq.Protected;

using Xunit.Abstractions;

namespace ConduitLLM.Tests.Providers
{
    /// <summary>
    /// Unit tests for provider authentication verification across all providers.
    /// These tests ensure that authentication verification works correctly with various
    /// configurations, especially around BaseUrl and HttpClient setup.
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Component", "ProviderAuthentication")]
    public class ProviderAuthenticationTests : TestBase
    {
        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
        private readonly Mock<IModelCapabilityService> _capabilityServiceMock;

        public ProviderAuthenticationTests(ITestOutputHelper output) : base(output)
        {
            _httpClientFactoryMock = new Mock<IHttpClientFactory>();
            _capabilityServiceMock = new Mock<IModelCapabilityService>();
        }

        #region OpenAI Provider Tests

        [Fact]
        public async Task OpenAI_VerifyAuthentication_WithValidApiKey_ReturnsSuccess()
        {
            // Arrange
            var mockHandler = new Mock<HttpMessageHandler>();
            var httpClient = new HttpClient(mockHandler.Object);
            _httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

            var provider = new Provider
            {
                Id = 1,
                ProviderType = ProviderType.OpenAI
            };
            
            var keyCredential = new ProviderKeyCredential
            {
                Id = 1,
                ProviderId = 1,
                ApiKey = "sk-test-key"
            };

            var modelsResponse = new
            {
                data = new[]
                {
                    new { id = "gpt-4", @object = "model" }
                }
            };

            mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => 
                        req.Method == HttpMethod.Get &&
                        req.RequestUri.ToString().Contains("/models")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonSerializer.Serialize(modelsResponse), Encoding.UTF8, "application/json")
                });

            var client = new OpenAIClient(
                provider,
                keyCredential,
                "gpt-4",
                CreateLogger<OpenAIClient>().Object,
                _httpClientFactoryMock.Object,
                _capabilityServiceMock.Object);

            // Act
            var result = await client.VerifyAuthenticationAsync();

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Contains("successfully", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task OpenAI_VerifyAuthentication_WithInvalidApiKey_ReturnsFailure()
        {
            // Arrange
            var mockHandler = new Mock<HttpMessageHandler>();
            var httpClient = new HttpClient(mockHandler.Object);
            _httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

            var provider = new Provider
            {
                Id = 1,
                ProviderType = ProviderType.OpenAI
            };
            
            var keyCredential = new ProviderKeyCredential
            {
                Id = 1,
                ProviderId = 1,
                ApiKey = "invalid-key"
            };

            mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.Unauthorized,
                    Content = new StringContent("{\"error\": {\"message\": \"Invalid API key\"}}", Encoding.UTF8, "application/json")
                });

            var client = new OpenAIClient(
                provider,
                keyCredential,
                "gpt-4",
                CreateLogger<OpenAIClient>().Object,
                _httpClientFactoryMock.Object,
                _capabilityServiceMock.Object);

            // Act
            var result = await client.VerifyAuthenticationAsync();

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains("Invalid API key", result.ErrorDetails);
        }

        [Fact]
        public async Task OpenAI_VerifyAuthentication_WithNullBaseUrl_UsesDefaultUrl()
        {
            // Arrange
            var mockHandler = new Mock<HttpMessageHandler>();
            var httpClient = new HttpClient(mockHandler.Object);
            _httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

            var provider = new Provider
            {
                Id = 1,
                ProviderType = ProviderType.OpenAI
            };
            
            var keyCredential = new ProviderKeyCredential
            {
                Id = 1,
                ProviderId = 1,
                ApiKey = "sk-test-key"
                // BaseUrl is null by default
            };

            var expectedUrl = "https://api.openai.com/v1/models";
            HttpRequestMessage capturedRequest = null;

            mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("{\"data\": []}", Encoding.UTF8, "application/json")
                });

            var client = new OpenAIClient(
                provider,
                keyCredential,
                "gpt-4",
                CreateLogger<OpenAIClient>().Object,
                _httpClientFactoryMock.Object,
                _capabilityServiceMock.Object);

            // Act
            await client.VerifyAuthenticationAsync();

            // Assert
            Assert.NotNull(capturedRequest);
            Assert.Equal(expectedUrl, capturedRequest.RequestUri.ToString());
        }

        [Fact]
        public async Task OpenAI_VerifyAuthentication_WithCustomBaseUrl_UsesProvidedUrl()
        {
            // Arrange
            var mockHandler = new Mock<HttpMessageHandler>();
            var httpClient = new HttpClient(mockHandler.Object);
            _httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

            var customBaseUrl = "https://custom.openai.proxy.com/v1";
            var provider = new Provider
            {
                Id = 1,
                ProviderType = ProviderType.OpenAI
            };
            
            var keyCredential = new ProviderKeyCredential
            {
                Id = 1,
                ProviderId = 1,
                ApiKey = "sk-test-key",
                BaseUrl = customBaseUrl
            };

            HttpRequestMessage capturedRequest = null;

            mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("{\"data\": []}", Encoding.UTF8, "application/json")
                });

            var client = new OpenAIClient(
                provider,
                keyCredential,
                "gpt-4",
                CreateLogger<OpenAIClient>().Object,
                _httpClientFactoryMock.Object,
                _capabilityServiceMock.Object);

            // Act
            await client.VerifyAuthenticationAsync(baseUrl: customBaseUrl);

            // Assert
            Assert.NotNull(capturedRequest);
            Assert.StartsWith(customBaseUrl, capturedRequest.RequestUri.ToString());
        }

        #endregion

        #region HttpClient Configuration Tests

        [Fact]
        public void CreateHttpClient_ForTestValidation_ShouldNotSetBaseAddress()
        {
            // This test verifies that when creating an HttpClient for validation,
            // we should NOT set BaseAddress if we're using absolute URLs
            
            // Arrange
            var mockHandler = new Mock<HttpMessageHandler>();
            var httpClient = new HttpClient(mockHandler.Object);
            
            // Initially, the HttpClient should have no BaseAddress
            Assert.Null(httpClient.BaseAddress);
            
            _httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

            var provider = new Provider
            {
                Id = 1,
                ProviderType = ProviderType.OpenAI
            };
            
            var keyCredential = new ProviderKeyCredential
            {
                Id = 1,
                ProviderId = 1,
                ApiKey = "test-key",
                BaseUrl = "https://api.openai.com/v1"
            };

            // Act - Create the client
            var client = new OpenAIClient(
                provider,
                keyCredential,
                "gpt-4",
                CreateLogger<OpenAIClient>().Object,
                _httpClientFactoryMock.Object,
                _capabilityServiceMock.Object);

            // The HttpClient used for validation should not have BaseAddress set
            // when using absolute URLs
            var validationClient = httpClient;
            
            // Assert
            // When using absolute URLs, BaseAddress should remain null
            Assert.Null(validationClient.BaseAddress);
        }

        #endregion

        #region Factory Test Client Creation Tests

        // TODO: These tests were removed when LLMClientFactory was deleted in favor of DatabaseAwareLLMClientFactory
        // Consider adding similar tests for DatabaseAwareLLMClientFactory if needed

        #endregion
    }
}