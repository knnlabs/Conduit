using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.DTOs.VirtualKey;
using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.WebUI.Options;
using ConduitLLM.WebUI.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text.Json;

// Alias to avoid ambiguity
using ItProtected = Moq.Protected.ItExpr;

namespace ConduitLLM.Tests.WebUI
{
    public class AdminApiClientErrorHandlingTests
    {
        private readonly Mock<ILogger<AdminApiClient>> _loggerMock;
        private readonly Mock<IOptions<AdminApiOptions>> _optionsMock;
        private readonly Mock<HttpMessageHandler> _handlerMock;
        private readonly HttpClient _httpClient;
        private readonly AdminApiClient _apiClient;
        private readonly AdminApiOptions _options;
        private readonly JsonSerializerOptions _jsonOptions;

        public AdminApiClientErrorHandlingTests()
        {
            _loggerMock = new Mock<ILogger<AdminApiClient>>();
            _options = new AdminApiOptions
            {
                BaseUrl = "http://localhost:5000",
                MasterKey = "test-master-key",
                TimeoutSeconds = 30
            };
            _optionsMock = new Mock<IOptions<AdminApiOptions>>();
            _optionsMock.Setup(o => o.Value).Returns(_options);

            _handlerMock = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_handlerMock.Object)
            {
                BaseAddress = new Uri(_options.BaseUrl)
            };

            _apiClient = new AdminApiClient(_httpClient, _optionsMock.Object, _loggerMock.Object);

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        [Fact]
        public async Task ApiClient_HandlesHttpRequestException()
        {
            // Arrange
            _handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItProtected.IsAny<HttpRequestMessage>(),
                    ItProtected.IsAny<CancellationToken>())
                .ThrowsAsync(new HttpRequestException("Network error"));

            // Act
            var result = await _apiClient.GetAllVirtualKeysAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Error retrieving virtual keys")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task ApiClient_HandlesTimeout()
        {
            // Arrange
            _handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItProtected.IsAny<HttpRequestMessage>(),
                    ItProtected.IsAny<CancellationToken>())
                .ThrowsAsync(new TaskCanceledException("The request timed out"));

            // Act
            var result = await _apiClient.GetAllVirtualKeysAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Error retrieving virtual keys")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task ApiClient_HandlesInvalidJson()
        {
            // Arrange
            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("Invalid JSON")
            };

            _handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItProtected.IsAny<HttpRequestMessage>(),
                    ItProtected.IsAny<CancellationToken>())
                .ReturnsAsync(response);

            // Act
            var result = await _apiClient.GetAllVirtualKeysAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Error retrieving virtual keys")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task ApiClient_HandlesServerError()
        {
            // Arrange
            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError,
                Content = new StringContent("Server error")
            };

            _handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItProtected.IsAny<HttpRequestMessage>(),
                    ItProtected.IsAny<CancellationToken>())
                .ReturnsAsync(response);

            // Act
            var result = await _apiClient.GetAllVirtualKeysAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Error retrieving virtual keys")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task ApiClient_HandlesUnauthorized()
        {
            // Arrange
            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.Unauthorized,
                Content = new StringContent("Unauthorized")
            };

            _handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItProtected.IsAny<HttpRequestMessage>(),
                    ItProtected.IsAny<CancellationToken>())
                .ReturnsAsync(response);

            // Act
            var result = await _apiClient.GetAllVirtualKeysAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Error retrieving virtual keys")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task ApiClient_HandlesNotFound_ForGetAll()
        {
            // Arrange
            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound,
                Content = new StringContent("Not found")
            };

            _handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItProtected.IsAny<HttpRequestMessage>(),
                    ItProtected.IsAny<CancellationToken>())
                .ReturnsAsync(response);

            // Act
            var result = await _apiClient.GetAllVirtualKeysAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Error retrieving virtual keys")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task ApiClient_ReturnsEmptyCollection_WhenApiReturnsNull()
        {
            // Arrange
            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("null")
            };

            _handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItProtected.IsAny<HttpRequestMessage>(),
                    ItProtected.IsAny<CancellationToken>())
                .ReturnsAsync(response);

            // Act
            var result = await _apiClient.GetAllVirtualKeysAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
            // No error should be logged since this is a valid response
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Never);
        }

        [Fact]
        public async Task ApiClient_SetsHeadersCorrectly()
        {
            // Arrange
            HttpRequestMessage capturedRequest = null;

            _handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItProtected.IsAny<HttpRequestMessage>(),
                    ItProtected.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((request, _) => capturedRequest = request)
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("[]")
                });

            // Act
            await _apiClient.GetAllVirtualKeysAsync();

            // Assert
            Assert.NotNull(capturedRequest);
            Assert.True(capturedRequest.Headers.Contains("X-Master-Key"));
            Assert.Equal("test-master-key", capturedRequest.Headers.GetValues("X-Master-Key").First());
        }

        [Fact]
        public async Task ApiClient_ConstructsUrlsCorrectly()
        {
            // Arrange
            HttpRequestMessage capturedRequest = null;

            _handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItProtected.IsAny<HttpRequestMessage>(),
                    ItProtected.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((request, _) => capturedRequest = request)
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("null")
                });

            // Act
            await _apiClient.GetVirtualKeyByIdAsync(123);

            // Assert
            Assert.NotNull(capturedRequest);
            Assert.Equal(HttpMethod.Get, capturedRequest.Method);
            Assert.Equal(new Uri("http://localhost:5000/api/virtualkeys/123"), capturedRequest.RequestUri);
        }

        [Fact]
        public async Task ApiClient_HandlesUriEscaping()
        {
            // Arrange
            HttpRequestMessage capturedRequest = null;

            _handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItProtected.IsAny<HttpRequestMessage>(),
                    ItProtected.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((request, _) => capturedRequest = request)
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("null")
                });

            // Act
            await _apiClient.GetGlobalSettingByKeyAsync("system/config path");

            // Assert
            Assert.NotNull(capturedRequest);
            Assert.Equal(HttpMethod.Get, capturedRequest.Method);
            Assert.Equal(new Uri("http://localhost:5000/api/globalsettings/system%2Fconfig%20path"), capturedRequest.RequestUri);
        }
    }
}