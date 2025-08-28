using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;
using ConduitLLM.Core.Validation;

using MassTransit;

using Microsoft.Extensions.Logging;

using Moq;

namespace ConduitLLM.Tests.Core.Services
{
    public partial class ImageGenerationOrchestratorTests
    {
        private readonly Mock<ILLMClientFactory> _mockClientFactory;
        private readonly Mock<IAsyncTaskService> _mockTaskService;
        private readonly Mock<IMediaStorageService> _mockStorageService;
        private readonly Mock<IPublishEndpoint> _mockPublishEndpoint;
        private readonly Mock<ConduitLLM.Configuration.Interfaces.IModelProviderMappingService> _mockModelMappingService;
        private readonly Mock<IVirtualKeyService> _mockVirtualKeyService;
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
        private readonly Mock<ICancellableTaskRegistry> _mockTaskRegistry;
        private readonly Mock<ICostCalculationService> _mockCostCalculationService;
        private readonly Mock<ConduitLLM.Configuration.Interfaces.IProviderService> _mockProviderService;
        private readonly Mock<ILogger<ImageGenerationOrchestrator>> _mockLogger;
        private readonly Mock<MinimalParameterValidator> _mockParameterValidator;
        private readonly ImageGenerationOrchestrator _orchestrator;

        public ImageGenerationOrchestratorTests()
        {
            _mockClientFactory = new Mock<ILLMClientFactory>();
            _mockTaskService = new Mock<IAsyncTaskService>();
            _mockStorageService = new Mock<IMediaStorageService>();
            _mockPublishEndpoint = new Mock<IPublishEndpoint>();
            _mockModelMappingService = new Mock<ConduitLLM.Configuration.Interfaces.IModelProviderMappingService>();
            _mockVirtualKeyService = new Mock<IVirtualKeyService>();
            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _mockTaskRegistry = new Mock<ICancellableTaskRegistry>();
            _mockCostCalculationService = new Mock<ICostCalculationService>();
            _mockProviderService = new Mock<ConduitLLM.Configuration.Interfaces.IProviderService>();
            _mockLogger = new Mock<ILogger<ImageGenerationOrchestrator>>();
            _mockParameterValidator = new Mock<MinimalParameterValidator>(new Mock<ILogger<MinimalParameterValidator>>().Object);

            _orchestrator = new ImageGenerationOrchestrator(
                _mockClientFactory.Object,
                _mockTaskService.Object,
                _mockStorageService.Object,
                _mockPublishEndpoint.Object,
                _mockModelMappingService.Object,
                _mockVirtualKeyService.Object,
                _mockHttpClientFactory.Object,
                _mockTaskRegistry.Object,
                _mockCostCalculationService.Object,
                _mockProviderService.Object,
                _mockParameterValidator.Object,
                _mockLogger.Object);
        }
    }
}