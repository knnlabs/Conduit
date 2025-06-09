using ConduitLLM.Admin.Services;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ConduitLLM.Admin.Tests.Services
{
    public class AdminProviderCredentialServiceTests
    {
        private readonly Mock<IProviderCredentialRepository> _mockProviderCredentialRepository;
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
        private readonly Mock<ILogger<AdminProviderCredentialService>> _mockLogger;
        private readonly AdminProviderCredentialService _service;

        public AdminProviderCredentialServiceTests()
        {
            _mockProviderCredentialRepository = new Mock<IProviderCredentialRepository>();
            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _mockLogger = new Mock<ILogger<AdminProviderCredentialService>>();
            _service = new AdminProviderCredentialService(
                _mockProviderCredentialRepository.Object,
                _mockHttpClientFactory.Object,
                _mockLogger.Object);
        }

        [Fact]
        public async Task TestProviderConnectionAsync_OpenRouter_ValidKey_ReturnsSuccess()
        {
            // Arrange
            var providerCredential = new ProviderCredentialDto
            {
                Id = 1,
                ProviderName = "OpenRouter",
                ApiKey = "valid-key",
                ApiBase = null
            };

            var dbCredential = new ProviderCredential
            {
                Id = 1,
                ProviderName = "OpenRouter",
                ApiKey = "valid-key",
                BaseUrl = null
            };

            _mockProviderCredentialRepository
                .Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(dbCredential);

            // Mock HttpClient for models endpoint (public, returns 200)
            var mockModelsHandler = new Mock<HttpMessageHandler>();
            mockModelsHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri != null && req.RequestUri.ToString().Contains("/models")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("{\"data\": []}")
                });

            // Mock HttpClient for chat completions (auth required, returns 200 for valid key)
            mockModelsHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri != null && req.RequestUri.ToString().Contains("/chat/completions")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.BadRequest, // Bad request due to invalid temperature
                    Content = new StringContent("{\"error\": {\"message\": \"Invalid temperature\"}}")
                });

            var httpClient = new HttpClient(mockModelsHandler.Object);
            _mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

            // Act
            var result = await _service.TestProviderConnectionAsync(providerCredential);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.Contains("Connected successfully", result.Message);
        }

        [Fact]
        public async Task TestProviderConnectionAsync_OpenRouter_InvalidKey_ReturnsFailure()
        {
            // Arrange
            var providerCredential = new ProviderCredentialDto
            {
                Id = 1,
                ProviderName = "OpenRouter",
                ApiKey = "invalid-key",
                ApiBase = null
            };

            var dbCredential = new ProviderCredential
            {
                Id = 1,
                ProviderName = "OpenRouter",
                ApiKey = "invalid-key",
                BaseUrl = null
            };

            _mockProviderCredentialRepository
                .Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(dbCredential);

            // Mock HttpClient for models endpoint (public, returns 200)
            var mockHandler = new Mock<HttpMessageHandler>();
            mockHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri != null && req.RequestUri.ToString().Contains("/models")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("{\"data\": []}")
                });

            // Mock HttpClient for chat completions (auth required, returns 401 for invalid key)
            mockHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri != null && req.RequestUri.ToString().Contains("/chat/completions")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.Unauthorized,
                    Content = new StringContent("{\"error\": {\"message\": \"No auth credentials found\"}}")
                });

            var httpClient = new HttpClient(mockHandler.Object);
            _mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

            // Act
            var result = await _service.TestProviderConnectionAsync(providerCredential);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Equal("Authentication failed", result.Message);
            Assert.Contains("Invalid API key", result.ErrorDetails);
        }

        [Fact]
        public async Task TestProviderConnectionAsync_OtherProvider_UsesSimpleCheck()
        {
            // Arrange
            var providerCredential = new ProviderCredentialDto
            {
                Id = 1,
                ProviderName = "OpenAI",
                ApiKey = "sk-test",
                ApiBase = null
            };

            var dbCredential = new ProviderCredential
            {
                Id = 1,
                ProviderName = "OpenAI",
                ApiKey = "sk-test",
                BaseUrl = null
            };

            _mockProviderCredentialRepository
                .Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(dbCredential);

            // Mock HttpClient for models endpoint
            var mockHandler = new Mock<HttpMessageHandler>();
            mockHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("{\"data\": []}")
                });

            var httpClient = new HttpClient(mockHandler.Object);
            _mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

            // Act
            var result = await _service.TestProviderConnectionAsync(providerCredential);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.Contains("Connected successfully", result.Message);
            // Verify that chat completions endpoint was NOT called for non-OpenRouter providers
            mockHandler.Protected().Verify(
                "SendAsync",
                Times.Never(),
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri != null && req.RequestUri.ToString().Contains("/chat/completions")),
                ItExpr.IsAny<CancellationToken>());
        }
    }
}