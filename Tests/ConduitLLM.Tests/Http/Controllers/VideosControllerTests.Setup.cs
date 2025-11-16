using ConduitLLM.Core.Interfaces;
using ConduitLLM.Http.Controllers;

using Microsoft.Extensions.Logging;

using Moq;

using Xunit.Abstractions;

namespace ConduitLLM.Tests.Http.Controllers
{
    [Trait("Category", "Unit")]
    [Trait("Component", "Http")]
    [Trait("Phase", "2")]
    public partial class VideosControllerTests : ControllerTestBase
    {
        private readonly Mock<IVideoGenerationService> _mockVideoService;
        private readonly Mock<IAsyncTaskService> _mockTaskService;
        private readonly Mock<IOperationTimeoutProvider> _mockTimeoutProvider;
        private readonly Mock<ICancellableTaskRegistry> _mockTaskRegistry;
        private readonly Mock<ILogger<VideosController>> _mockLogger;
        private readonly VideosController _controller;

        public VideosControllerTests(ITestOutputHelper output) : base(output)
        {
            _mockVideoService = new Mock<IVideoGenerationService>();
            _mockTaskService = new Mock<IAsyncTaskService>();
            _mockTimeoutProvider = new Mock<IOperationTimeoutProvider>();
            _mockTaskRegistry = new Mock<ICancellableTaskRegistry>();
            _mockLogger = CreateLogger<VideosController>();
            var mockModelMappingService = new Mock<ConduitLLM.Configuration.Interfaces.IModelProviderMappingService>();

            _controller = new VideosController(
                _mockVideoService.Object,
                _mockTaskService.Object,
                _mockTimeoutProvider.Object,
                _mockTaskRegistry.Object,
                _mockLogger.Object,
                mockModelMappingService.Object);

            // Setup default controller context
            _controller.ControllerContext = CreateControllerContext();
        }
    }
}