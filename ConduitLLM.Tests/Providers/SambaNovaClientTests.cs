using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Providers.SambaNova;

using Moq;

using Xunit.Abstractions;

namespace ConduitLLM.Tests.Providers
{
    /// <summary>
    /// Unit tests for the SambaNovaClient class, covering ultra-fast inference scenarios.
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Component", "Providers")]
    public class SambaNovaClientTests : TestBase
    {
        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
        private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
        private readonly HttpClient _httpClient;

        public SambaNovaClientTests(ITestOutputHelper output) : base(output)
        {
            _httpClientFactoryMock = new Mock<IHttpClientFactory>();
            _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_httpMessageHandlerMock.Object)
            {
                BaseAddress = new Uri("https://api.sambanova.ai/v1/")
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
                ProviderType = ProviderType.SambaNova
            };
            
            var keyCredential = new ProviderKeyCredential
            {
                Id = 1,
                ProviderId = 1,
                ApiKey = "test-api-key"
            };
            
            var modelId = "DeepSeek-R1";
            var logger = CreateLogger<SambaNovaClient>();

            // Act
            var client = new SambaNovaClient(
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
                ProviderType = ProviderType.SambaNova
            };
            
            var keyCredential = new ProviderKeyCredential
            {
                Id = 1,
                ProviderId = 1,
                ApiKey = "" // Empty API key
            };
            
            var modelId = "DeepSeek-R1";
            var logger = CreateLogger<SambaNovaClient>();

            // Act & Assert
            var ex = Assert.Throws<ConfigurationException>(() =>
                new SambaNovaClient(
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
            var modelId = "DeepSeek-R1";
            var logger = CreateLogger<SambaNovaClient>();

            // Act & Assert
            Assert.ThrowsAny<Exception>(() =>
                new SambaNovaClient(
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
                ProviderType = ProviderType.SambaNova
            };
            
            var keyCredential = new ProviderKeyCredential
            {
                Id = 1,
                ProviderId = 1,
                ApiKey = "test-api-key"
            };
            
            var modelId = "DeepSeek-R1";

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new SambaNovaClient(
                    provider,
                    keyCredential,
                    modelId,
                    null!,
                    _httpClientFactoryMock.Object));
        }

        #endregion

        #region Model Support Tests

        [Theory]
        [InlineData("DeepSeek-R1")]
        [InlineData("DeepSeek-V3-0324")]
        [InlineData("DeepSeek-R1-Distill-Llama-70B")]
        [InlineData("Meta-Llama-3.3-70B-Instruct")]
        [InlineData("Meta-Llama-3.1-8B-Instruct")]
        [InlineData("Llama-3.3-Swallow-70B-Instruct-v0.4")]
        [InlineData("Qwen3-32B")]
        [InlineData("E5-Mistral-7B-Instruct")]
        [InlineData("Llama-4-Maverick-17B-128E-Instruct")]
        public void SupportedModels_AreRecognized(string modelId)
        {
            // Arrange
            var provider = new Provider
            {
                Id = 1,
                ProviderType = ProviderType.SambaNova
            };
            
            var keyCredential = new ProviderKeyCredential
            {
                Id = 1,
                ProviderId = 1,
                ApiKey = "test-api-key"
            };
            
            var logger = CreateLogger<SambaNovaClient>();

            // Act
            var client = new SambaNovaClient(
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