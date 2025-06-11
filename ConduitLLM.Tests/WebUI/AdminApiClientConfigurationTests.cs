using ConduitLLM.WebUI.Options;
using ConduitLLM.WebUI.Services;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;

namespace ConduitLLM.Tests.WebUI
{
    public class AdminApiClientConfigurationTests
    {
        private readonly Mock<ILogger<AdminApiClient>> _loggerMock;
        private readonly Mock<HttpMessageHandler> _handlerMock;

        public AdminApiClientConfigurationTests()
        {
            _loggerMock = new Mock<ILogger<AdminApiClient>>();
            _handlerMock = new Mock<HttpMessageHandler>();
        }

        [Fact]
        public void ApiClient_ConfiguresBaseAddress_FromOptions()
        {
            // Arrange
            var options = new AdminApiOptions
            {
                BaseUrl = "http://custom-admin-api:8080",
                MasterKey = "test-key"
            };
            var optionsMock = new Mock<IOptions<AdminApiOptions>>();
            optionsMock.Setup(o => o.Value).Returns(options);

            // Act
            using var httpClient = new HttpClient(_handlerMock.Object);
            var apiClient = new AdminApiClient(httpClient, optionsMock.Object, _loggerMock.Object);

            // Assert
            Assert.Equal(new Uri("http://custom-admin-api:8080"), httpClient.BaseAddress);
        }

        [Fact]
        public void ApiClient_ConfiguresTimeout_FromOptions()
        {
            // Arrange
            var options = new AdminApiOptions
            {
                BaseUrl = "http://localhost:5000",
                MasterKey = "test-key",
                TimeoutSeconds = 60
            };
            var optionsMock = new Mock<IOptions<AdminApiOptions>>();
            optionsMock.Setup(o => o.Value).Returns(options);

            // Act
            using var httpClient = new HttpClient(_handlerMock.Object);
            var apiClient = new AdminApiClient(httpClient, optionsMock.Object, _loggerMock.Object);

            // Assert
            Assert.Equal(TimeSpan.FromSeconds(60), httpClient.Timeout);
        }

        [Fact]
        public void ApiClient_ConfiguresMasterKey_FromOptions()
        {
            // Arrange
            var options = new AdminApiOptions
            {
                BaseUrl = "http://localhost:5000",
                MasterKey = "custom-master-key",
                TimeoutSeconds = 30
            };
            var optionsMock = new Mock<IOptions<AdminApiOptions>>();
            optionsMock.Setup(o => o.Value).Returns(options);

            // Act
            using var httpClient = new HttpClient(_handlerMock.Object);
            var apiClient = new AdminApiClient(httpClient, optionsMock.Object, _loggerMock.Object);

            // Assert - Use X-API-Key header (as expected by AdminAuthenticationMiddleware) instead of X-Master-Key
            var headerExists = httpClient.DefaultRequestHeaders.TryGetValues("X-API-Key", out var values);
            Assert.True(headerExists);
            Assert.NotNull(values);
            Assert.Equal("custom-master-key", values!.First());
        }

        [Fact]
        public void ApiClient_HandlesNullOptions_WithDefaults()
        {
            // Arrange
            var optionsMock = new Mock<IOptions<AdminApiOptions>>();
            optionsMock.Setup(o => o.Value).Returns(new AdminApiOptions());

            // Act
            using var httpClient = new HttpClient(_handlerMock.Object);
            var apiClient = new AdminApiClient(httpClient, optionsMock.Object, _loggerMock.Object);

            // This test passes if no exception is thrown
            // Assert default values if needed
            Assert.Equal(TimeSpan.FromSeconds(30), httpClient.Timeout);
        }

        [Fact]
        public void ApiClient_ThrowsArgumentNullException_WhenHttpClientIsNull()
        {
            // Arrange
            var options = new AdminApiOptions();
            var optionsMock = new Mock<IOptions<AdminApiOptions>>();
            optionsMock.Setup(o => o.Value).Returns(options);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new AdminApiClient(null!, optionsMock.Object, _loggerMock.Object));
        }

        [Fact]
        public void ApiClient_ThrowsArgumentNullException_WhenLoggerIsNull()
        {
            // Arrange
            var options = new AdminApiOptions();
            var optionsMock = new Mock<IOptions<AdminApiOptions>>();
            optionsMock.Setup(o => o.Value).Returns(options);
            using var httpClient = new HttpClient(_handlerMock.Object);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new AdminApiClient(httpClient, optionsMock.Object, null!));
        }

        [Fact]
        public void ApiClient_DoesNotSetHeader_WhenMasterKeyIsEmpty()
        {
            // Arrange
            var options = new AdminApiOptions
            {
                BaseUrl = "http://localhost:5000",
                MasterKey = string.Empty,
                TimeoutSeconds = 30
            };
            var optionsMock = new Mock<IOptions<AdminApiOptions>>();
            optionsMock.Setup(o => o.Value).Returns(options);

            // Act
            using var httpClient = new HttpClient(_handlerMock.Object);
            var apiClient = new AdminApiClient(httpClient, optionsMock.Object, _loggerMock.Object);

            // Assert - Check X-API-Key header instead of X-Master-Key
            var headerExists = httpClient.DefaultRequestHeaders.TryGetValues("X-API-Key", out _);
            Assert.False(headerExists);
        }

        [Fact]
        public void ApiClient_DoesNotChangeBaseAddress_WhenBaseUrlIsEmpty()
        {
            // Arrange
            var options = new AdminApiOptions
            {
                BaseUrl = string.Empty,
                MasterKey = "test-key",
                TimeoutSeconds = 30
            };
            var optionsMock = new Mock<IOptions<AdminApiOptions>>();
            optionsMock.Setup(o => o.Value).Returns(options);

            // Act
            using var httpClient = new HttpClient(_handlerMock.Object);
            var originalBaseAddress = httpClient.BaseAddress;
            var apiClient = new AdminApiClient(httpClient, optionsMock.Object, _loggerMock.Object);

            // Assert
            Assert.Equal(originalBaseAddress, httpClient.BaseAddress);
        }
    }
}
