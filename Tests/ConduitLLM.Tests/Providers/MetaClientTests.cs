using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Providers.Meta;

using Moq;

using Xunit.Abstractions;

namespace ConduitLLM.Tests.Providers
{
    /// <summary>
    /// Unit tests for the MetaClient class, covering the Meta Model API (Muse Spark models).
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Component", "Providers")]
    public class MetaClientTests : TestBase
    {
        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
        private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
        private readonly HttpClient _httpClient;

        public MetaClientTests(ITestOutputHelper output) : base(output)
        {
            _httpClientFactoryMock = new Mock<IHttpClientFactory>();
            _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_httpMessageHandlerMock.Object)
            {
                BaseAddress = new Uri("https://api.meta.ai/v1/")
            };
            _httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(_httpClient);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidCredentials_InitializesCorrectly()
        {
            // Arrange
            var provider = new Provider
            {
                Id = 1,
                ProviderType = ProviderType.Meta
            };

            var keyCredential = new ProviderKeyCredential
            {
                Id = 1,
                ProviderId = 1,
                ApiKey = "test-api-key"
            };

            var modelId = "muse-spark-1.1";
            var logger = CreateLogger<MetaClient>();

            // Act
            var client = new MetaClient(
                provider,
                keyCredential,
                modelId,
                logger.Object,
                _httpClientFactoryMock.Object);

            // Assert
            Assert.NotNull(client);
        }

        [Fact]
        public void Constructor_WithMissingApiKey_ThrowsConfigurationException()
        {
            // Arrange
            var provider = new Provider
            {
                Id = 1,
                ProviderType = ProviderType.Meta
            };

            var keyCredential = new ProviderKeyCredential
            {
                Id = 1,
                ProviderId = 1,
                ApiKey = "" // Empty API key
            };

            var modelId = "muse-spark-1.1";
            var logger = CreateLogger<MetaClient>();

            // Act & Assert
            var ex = Assert.Throws<ConfigurationException>(() =>
                new MetaClient(
                    provider,
                    keyCredential,
                    modelId,
                    logger.Object,
                    _httpClientFactoryMock.Object));

            Assert.Contains("API key is missing", ex.Message);
        }

        [Fact]
        public void Constructor_WithNullCredentials_ThrowsException()
        {
            // Arrange
            var modelId = "muse-spark-1.1";
            var logger = CreateLogger<MetaClient>();

            // Act & Assert
            Assert.ThrowsAny<Exception>(() =>
                new MetaClient(
                    null!,
                    null!,
                    modelId,
                    logger.Object,
                    _httpClientFactoryMock.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Arrange
            var provider = new Provider
            {
                Id = 1,
                ProviderType = ProviderType.Meta
            };

            var keyCredential = new ProviderKeyCredential
            {
                Id = 1,
                ProviderId = 1,
                ApiKey = "test-api-key"
            };

            var modelId = "muse-spark-1.1";

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new MetaClient(
                    provider,
                    keyCredential,
                    modelId,
                    null!,
                    _httpClientFactoryMock.Object));
        }

        #endregion

        #region Model Support Tests

        [Theory]
        [InlineData("muse-spark-1.1")]
        [InlineData("muse-spark-1")]
        public void SupportedModels_AreRecognized(string modelId)
        {
            // Arrange
            var provider = new Provider
            {
                Id = 1,
                ProviderType = ProviderType.Meta
            };

            var keyCredential = new ProviderKeyCredential
            {
                Id = 1,
                ProviderId = 1,
                ApiKey = "test-api-key"
            };

            var logger = CreateLogger<MetaClient>();

            // Act
            var client = new MetaClient(
                provider,
                keyCredential,
                modelId,
                logger.Object,
                _httpClientFactoryMock.Object);

            // Assert
            Assert.NotNull(client);
        }

        #endregion
    }
}
