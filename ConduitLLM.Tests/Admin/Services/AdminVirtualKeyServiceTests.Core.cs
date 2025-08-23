using ConduitLLM.Admin.Services;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Interfaces;

using MassTransit;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Moq;

namespace ConduitLLM.Tests.Admin.Services
{
    public partial class AdminVirtualKeyServiceTests
    {
        private readonly Mock<IVirtualKeyRepository> _mockVirtualKeyRepository;
        private readonly Mock<IVirtualKeySpendHistoryRepository> _mockSpendHistoryRepository;
        private readonly Mock<IVirtualKeyGroupRepository> _mockGroupRepository;
        private readonly Mock<IVirtualKeyCache> _mockCache;
        private readonly Mock<IPublishEndpoint> _mockPublishEndpoint;
        private readonly Mock<ILogger<AdminVirtualKeyService>> _mockLogger;
        private readonly Mock<IMediaLifecycleService> _mockMediaLifecycleService;
        private readonly Mock<IModelProviderMappingRepository> _mockModelProviderMappingRepository;
        private readonly Mock<IModelCapabilityService> _mockModelCapabilityService;
        private readonly Mock<IDbContextFactory<ConduitDbContext>> _mockDbContextFactory;
        private readonly AdminVirtualKeyService _service;

        public AdminVirtualKeyServiceTests()
        {
            _mockVirtualKeyRepository = new Mock<IVirtualKeyRepository>();
            _mockSpendHistoryRepository = new Mock<IVirtualKeySpendHistoryRepository>();
            _mockGroupRepository = new Mock<IVirtualKeyGroupRepository>();
            _mockCache = new Mock<IVirtualKeyCache>();
            _mockPublishEndpoint = new Mock<IPublishEndpoint>();
            _mockLogger = new Mock<ILogger<AdminVirtualKeyService>>();
            _mockMediaLifecycleService = new Mock<IMediaLifecycleService>();
            _mockModelProviderMappingRepository = new Mock<IModelProviderMappingRepository>();
            _mockModelCapabilityService = new Mock<IModelCapabilityService>();
            _mockDbContextFactory = new Mock<IDbContextFactory<ConduitDbContext>>();

            _service = new AdminVirtualKeyService(
                _mockVirtualKeyRepository.Object,
                _mockSpendHistoryRepository.Object,
                _mockGroupRepository.Object,
                _mockCache.Object,
                _mockPublishEndpoint.Object,
                _mockLogger.Object,
                _mockModelProviderMappingRepository.Object,
                _mockModelCapabilityService.Object,
                _mockDbContextFactory.Object,
                _mockMediaLifecycleService.Object);
        }
    }
}