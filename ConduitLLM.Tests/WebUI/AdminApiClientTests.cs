using System.Net;
using System.Text.Json;

using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.WebUI.Options;
using ConduitLLM.WebUI.Services;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;
using Moq.Protected;
// Alias to avoid ambiguity
using ItProtected = Moq.Protected.ItExpr;

namespace ConduitLLM.Tests.WebUI
{
    public class AdminApiClientTests
    {
        private readonly Mock<ILogger<AdminApiClient>> _loggerMock;
        private readonly Mock<IOptions<AdminApiOptions>> _optionsMock;
        private readonly Mock<HttpMessageHandler> _handlerMock;
        private readonly HttpClient _httpClient;
        private readonly AdminApiClient _apiClient;
        private readonly AdminApiOptions _options;
        private readonly JsonSerializerOptions _jsonOptions;

        public AdminApiClientTests()
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
        public async Task GetAllVirtualKeysAsync_ReturnsVirtualKeys_WhenApiReturnsSuccess()
        {
            // Arrange
            var expectedKeys = new List<ConfigDTOs.VirtualKey.VirtualKeyDto>
            {
                new ConfigDTOs.VirtualKey.VirtualKeyDto { Id = 1, Name = "Test Key 1", IsActive = true },
                new ConfigDTOs.VirtualKey.VirtualKeyDto { Id = 2, Name = "Test Key 2", IsActive = false }
            };

            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(expectedKeys, _jsonOptions))
            };

            _handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItProtected.Is<HttpRequestMessage>(req =>
                        req.Method == HttpMethod.Get &&
                        req.RequestUri != null &&
                        req.RequestUri.ToString() == "http://localhost:5000/api/virtualkeys"),
                    ItProtected.IsAny<CancellationToken>())
                .ReturnsAsync(response);

            // Act
            var result = await _apiClient.GetAllVirtualKeysAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count());
            Assert.Equal(1, result.First().Id);
            Assert.Equal("Test Key 1", result.First().Name);
            Assert.True(result.First().IsActive);
        }

        [Fact]
        public async Task GetAllVirtualKeysAsync_ReturnsEmptyList_WhenApiThrowsException()
        {
            // Arrange
            _handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItProtected.IsAny<HttpRequestMessage>(),
                    ItProtected.IsAny<CancellationToken>())
                .ThrowsAsync(new HttpRequestException("Test exception"));

            // Act
            var result = await _apiClient.GetAllVirtualKeysAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v != null && v.ToString() != null && v.ToString()!.Contains("Error retrieving virtual keys")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GetVirtualKeyByIdAsync_ReturnsVirtualKey_WhenApiReturnsSuccess()
        {
            // Arrange
            var expectedKey = new ConfigDTOs.VirtualKey.VirtualKeyDto { Id = 1, Name = "Test Key", IsActive = true };
            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(expectedKey, _jsonOptions))
            };

            _handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItProtected.Is<HttpRequestMessage>(req =>
                        req.Method == HttpMethod.Get &&
                        req.RequestUri != null && req.RequestUri.ToString() == "http://localhost:5000/api/virtualkeys/1"),
                    ItProtected.IsAny<CancellationToken>())
                .ReturnsAsync(response);

            // Act
            var result = await _apiClient.GetVirtualKeyByIdAsync(1);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1, result.Id);
            Assert.Equal("Test Key", result.Name);
            Assert.True(result.IsActive);
        }

        [Fact]
        public async Task GetVirtualKeyByIdAsync_ReturnsNull_WhenApiReturnsNotFound()
        {
            // Arrange
            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound
            };

            _handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItProtected.Is<HttpRequestMessage>(req =>
                        req.Method == HttpMethod.Get &&
                        req.RequestUri != null && req.RequestUri.ToString() == "http://localhost:5000/api/virtualkeys/999"),
                    ItProtected.IsAny<CancellationToken>())
                .ReturnsAsync(response);

            // Act
            var result = await _apiClient.GetVirtualKeyByIdAsync(999);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task CreateVirtualKeyAsync_ReturnsCreatedKey_WhenApiReturnsSuccess()
        {
            // Arrange
            var createDto = new ConfigDTOs.VirtualKey.CreateVirtualKeyRequestDto
            {
                KeyName = "New Key"
            };

            var keyInfo = new ConfigDTOs.VirtualKey.VirtualKeyDto
            {
                Id = 1,
                Name = "New Key",
                IsActive = true
            };

            var expectedResponse = new ConfigDTOs.VirtualKey.CreateVirtualKeyResponseDto
            {
                VirtualKey = "gen-key-123456",
                KeyInfo = keyInfo
            };

            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.Created,
                Content = new StringContent(JsonSerializer.Serialize(expectedResponse, _jsonOptions))
            };

            _handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItProtected.Is<HttpRequestMessage>(req =>
                        req.Method == HttpMethod.Post &&
                        req.RequestUri != null && req.RequestUri.ToString() == "http://localhost:5000/api/virtualkeys"),
                    ItProtected.IsAny<CancellationToken>())
                .ReturnsAsync(response);

            // Act
            var result = await _apiClient.CreateVirtualKeyAsync(createDto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("gen-key-123456", result.VirtualKey);
            Assert.NotNull(result.KeyInfo);
            Assert.Equal(1, result.KeyInfo.Id);
            Assert.Equal("New Key", result.KeyInfo.Name);
            Assert.True(result.KeyInfo.IsActive);
        }

        [Fact]
        public async Task UpdateVirtualKeyAsync_ReturnsTrue_WhenApiReturnsSuccess()
        {
            // Arrange
            var updateDto = new ConfigDTOs.VirtualKey.UpdateVirtualKeyRequestDto
            {
                KeyName = "Updated Key",
                IsEnabled = false
            };

            var response = new HttpResponseMessage { StatusCode = HttpStatusCode.OK };

            _handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItProtected.Is<HttpRequestMessage>(req =>
                        req.Method == HttpMethod.Put &&
                        req.RequestUri != null && req.RequestUri.ToString() == "http://localhost:5000/api/virtualkeys/1"),
                    ItProtected.IsAny<CancellationToken>())
                .ReturnsAsync(response);

            // Act
            var result = await _apiClient.UpdateVirtualKeyAsync(1, updateDto);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task DeleteVirtualKeyAsync_ReturnsTrue_WhenApiReturnsSuccess()
        {
            // Arrange
            var response = new HttpResponseMessage { StatusCode = HttpStatusCode.OK };

            _handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItProtected.Is<HttpRequestMessage>(req =>
                        req.Method == HttpMethod.Delete &&
                        req.RequestUri != null && req.RequestUri.ToString() == "http://localhost:5000/api/virtualkeys/1"),
                    ItProtected.IsAny<CancellationToken>())
                .ReturnsAsync(response);

            // Act
            var result = await _apiClient.DeleteVirtualKeyAsync(1);

            // Assert
            Assert.True(result);
        }
    }
}
