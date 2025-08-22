using ConduitLLM.Core.Configuration;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;

using MassTransit;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;

namespace ConduitLLM.Tests.Core.Services
{
    public partial class VideoGenerationOrchestratorTests
    {
        private readonly Mock<ILLMClientFactory> _mockClientFactory;
        private readonly Mock<IAsyncTaskService> _mockTaskService;
        private readonly Mock<IMediaStorageService> _mockStorageService;
        private readonly Mock<IPublishEndpoint> _mockPublishEndpoint;
        private readonly Mock<ConduitLLM.Configuration.Interfaces.IModelProviderMappingService> _mockModelMappingService;
        private readonly Mock<IVirtualKeyService> _mockVirtualKeyService;
        private readonly Mock<ICostCalculationService> _mockCostService;
        private readonly Mock<ICancellableTaskRegistry> _mockTaskRegistry;
        private readonly Mock<IWebhookNotificationService> _mockWebhookService;
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
        private readonly Mock<ILogger<VideoGenerationOrchestrator>> _mockLogger;
        private readonly Mock<IOptions<VideoGenerationRetryConfiguration>> _mockRetryOptions;
        private readonly VideoGenerationRetryConfiguration _retryConfiguration;
        private readonly VideoGenerationOrchestrator _orchestrator;

        public VideoGenerationOrchestratorTests()
        {
            _mockClientFactory = new Mock<ILLMClientFactory>();
            _mockTaskService = new Mock<IAsyncTaskService>();
            _mockStorageService = new Mock<IMediaStorageService>();
            _mockPublishEndpoint = new Mock<IPublishEndpoint>();
            _mockModelMappingService = new Mock<ConduitLLM.Configuration.Interfaces.IModelProviderMappingService>();
            _mockVirtualKeyService = new Mock<IVirtualKeyService>();
            _mockCostService = new Mock<ICostCalculationService>();
            _mockTaskRegistry = new Mock<ICancellableTaskRegistry>();
            _mockWebhookService = new Mock<IWebhookNotificationService>();
            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _mockLogger = new Mock<ILogger<VideoGenerationOrchestrator>>();
            _mockRetryOptions = new Mock<IOptions<VideoGenerationRetryConfiguration>>();

            _retryConfiguration = new VideoGenerationRetryConfiguration
            {
                EnableRetries = true,
                MaxRetries = 3,
                BaseDelaySeconds = 1,
                MaxDelaySeconds = 300
            };

            _mockRetryOptions.Setup(x => x.Value).Returns(_retryConfiguration);

            _orchestrator = new VideoGenerationOrchestrator(
                _mockClientFactory.Object,
                _mockTaskService.Object,
                _mockStorageService.Object,
                _mockPublishEndpoint.Object,
                _mockModelMappingService.Object,
                _mockVirtualKeyService.Object,
                _mockCostService.Object,
                _mockTaskRegistry.Object,
                _mockWebhookService.Object,
                _mockRetryOptions.Object,
                _mockHttpClientFactory.Object,
                _mockLogger.Object);
        }
    }
}