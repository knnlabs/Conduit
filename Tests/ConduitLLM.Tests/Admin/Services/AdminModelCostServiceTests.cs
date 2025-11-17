using ConduitLLM.Admin.Services;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Interfaces;

using MassTransit;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Moq;

namespace ConduitLLM.Tests.Admin.Services
{
    public partial class AdminModelCostServiceTests : IDisposable
    {
        private readonly Mock<IModelCostRepository> _mockModelCostRepository;
        private readonly Mock<IRequestLogRepository> _mockRequestLogRepository;
        private readonly Mock<IDbContextFactory<ConduitDbContext>> _mockDbContextFactory;
        private readonly Mock<IPublishEndpoint> _mockPublishEndpoint;
        private readonly Mock<ILogger<AdminModelCostService>> _mockLogger;
        private readonly AdminModelCostService _service;
        private readonly DbContextOptions<ConduitDbContext> _dbContextOptions;

        public AdminModelCostServiceTests()
        {
            _mockModelCostRepository = new Mock<IModelCostRepository>();
            _mockRequestLogRepository = new Mock<IRequestLogRepository>();
            _mockDbContextFactory = new Mock<IDbContextFactory<ConduitDbContext>>();
            _mockPublishEndpoint = new Mock<IPublishEndpoint>();
            _mockLogger = new Mock<ILogger<AdminModelCostService>>();

            // Setup in-memory database options for testing
            _dbContextOptions = new DbContextOptionsBuilder<ConduitDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            // Setup factory to create new contexts each time
            _mockDbContextFactory.Setup(x => x.CreateDbContextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new ConduitDbContext(_dbContextOptions));

            _service = new AdminModelCostService(
                _mockModelCostRepository.Object,
                _mockRequestLogRepository.Object,
                _mockDbContextFactory.Object,
                _mockPublishEndpoint.Object,
                _mockLogger.Object);
        }

        public void Dispose()
        {
            // Cleanup any remaining contexts if needed
        }

        private ConduitDbContext CreateDbContext()
        {
            return new ConduitDbContext(_dbContextOptions);
        }
    }
}