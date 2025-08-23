using ConduitLLM.Core.Interfaces;

using Moq;

using Xunit.Abstractions;

namespace ConduitLLM.Tests.Providers
{
    /// <summary>
    /// Unit tests for the OpenAIClient class, covering standard OpenAI and Azure OpenAI scenarios.
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Component", "Providers")]
    public partial class OpenAIClientTests : TestBase
    {
        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
        private readonly Mock<IModelCapabilityService> _capabilityServiceMock;
        private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
        private readonly HttpClient _httpClient;

        public OpenAIClientTests(ITestOutputHelper output) : base(output)
        {
            _httpClientFactoryMock = new Mock<IHttpClientFactory>();
            _capabilityServiceMock = new Mock<IModelCapabilityService>();
            _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_httpMessageHandlerMock.Object)
            {
                BaseAddress = new Uri("https://api.openai.com/v1/")
            };
            _httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(_httpClient);
        }
    }
}