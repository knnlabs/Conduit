using ConduitLLM.Core.Interfaces;
using ConduitLLM.Http.Controllers;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

using Moq;

using Xunit.Abstractions;

namespace ConduitLLM.Tests.Http.Controllers
{
    [Trait("Category", "Unit")]
    [Trait("Component", "Http")]
    [Trait("Phase", "2")]
    public partial class HybridAudioControllerTests : ControllerTestBase
    {
        private readonly Mock<IHybridAudioService> _mockHybridAudioService;
        private readonly Mock<ConduitLLM.Configuration.Interfaces.IVirtualKeyService> _mockVirtualKeyService;
        private readonly Mock<ILogger<HybridAudioController>> _mockLogger;
        private readonly HybridAudioController _controller;

        public HybridAudioControllerTests(ITestOutputHelper output) : base(output)
        {
            _mockHybridAudioService = new Mock<IHybridAudioService>();
            _mockVirtualKeyService = new Mock<ConduitLLM.Configuration.Interfaces.IVirtualKeyService>();
            _mockLogger = CreateLogger<HybridAudioController>();

            _controller = new HybridAudioController(
                _mockHybridAudioService.Object,
                _mockVirtualKeyService.Object,
                _mockLogger.Object);

            _controller.ControllerContext = CreateControllerContext();
        }




        #region Helper Methods

        private IFormFile CreateFormFile(string fileName, byte[] content, string contentType)
        {
            var stream = new MemoryStream(content);
            var formFile = new FormFile(stream, 0, content.Length, "file", fileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = contentType
            };
            return formFile;
        }

        #endregion
    }
}