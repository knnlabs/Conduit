using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Configuration;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Xunit;

namespace ConduitLLM.Tests.Core.Services
{
    public partial class ImageGenerationOrchestratorTests
    {
        private readonly Mock<ILLMClientFactory> _mockClientFactory;
        private readonly Mock<IAsyncTaskService> _mockTaskService;
        private readonly Mock<IMediaStorageService> _mockStorageService;
        private readonly Mock<IPublishEndpoint> _mockPublishEndpoint;
        private readonly Mock<ConduitLLM.Configuration.IModelProviderMappingService> _mockModelMappingService;
        private readonly Mock<IProviderDiscoveryService> _mockDiscoveryService;
        private readonly Mock<IVirtualKeyService> _mockVirtualKeyService;
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
        private readonly Mock<ICancellableTaskRegistry> _mockTaskRegistry;
        private readonly Mock<ICostCalculationService> _mockCostCalculationService;
        private readonly Mock<ConduitLLM.Configuration.IProviderService> _mockProviderService;
        private readonly Mock<ILogger<ImageGenerationOrchestrator>> _mockLogger;
        private readonly Mock<IOptions<ImageGenerationPerformanceConfiguration>> _mockPerformanceOptions;
        private readonly ImageGenerationPerformanceConfiguration _performanceConfig;
        private readonly ImageGenerationOrchestrator _orchestrator;

        public ImageGenerationOrchestratorTests()
        {
            _mockClientFactory = new Mock<ILLMClientFactory>();
            _mockTaskService = new Mock<IAsyncTaskService>();
            _mockStorageService = new Mock<IMediaStorageService>();
            _mockPublishEndpoint = new Mock<IPublishEndpoint>();
            _mockModelMappingService = new Mock<ConduitLLM.Configuration.IModelProviderMappingService>();
            _mockDiscoveryService = new Mock<IProviderDiscoveryService>();
            _mockVirtualKeyService = new Mock<IVirtualKeyService>();
            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _mockTaskRegistry = new Mock<ICancellableTaskRegistry>();
            _mockCostCalculationService = new Mock<ICostCalculationService>();
            _mockProviderService = new Mock<ConduitLLM.Configuration.IProviderService>();
            _mockLogger = new Mock<ILogger<ImageGenerationOrchestrator>>();
            _mockPerformanceOptions = new Mock<IOptions<ImageGenerationPerformanceConfiguration>>();

            _performanceConfig = new ImageGenerationPerformanceConfiguration
            {
                MaxConcurrentGenerations = 5,
                ProviderConcurrencyLimits = new Dictionary<string, int>
                {
                    ["openai"] = 3,
                    ["minimax"] = 2
                },
                ProviderDownloadTimeouts = new Dictionary<string, int>
                {
                    ["openai"] = 30,
                    ["minimax"] = 60
                }
            };

            _mockPerformanceOptions.Setup(x => x.Value).Returns(_performanceConfig);

            _orchestrator = new ImageGenerationOrchestrator(
                _mockClientFactory.Object,
                _mockTaskService.Object,
                _mockStorageService.Object,
                _mockPublishEndpoint.Object,
                _mockModelMappingService.Object,
                _mockDiscoveryService.Object,
                _mockVirtualKeyService.Object,
                _mockHttpClientFactory.Object,
                _mockTaskRegistry.Object,
                _mockCostCalculationService.Object,
                _mockProviderService.Object,
                _mockPerformanceOptions.Object,
                _mockLogger.Object);
        }
    }
}