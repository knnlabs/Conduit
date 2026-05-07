using ConduitLLM.Admin.Services;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Tests.TestInfrastructure;

using MassTransit;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Moq;

namespace ConduitLLM.Tests.Admin.Services
{
    public partial class AdminVirtualKeyServiceTests : IDisposable
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
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<ConduitDbContext> _dbContextOptions;
        private readonly TestDbContextFactory _dbContextFactory;
        private readonly AdminVirtualKeyService _service;
        private bool _disposed;

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

            // SQLite-backed factory so tests that hit ExecuteUpdateAsync (e.g. PerformMaintenanceAsync)
            // run against a real relational provider. EF's InMemory provider does not support it.
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            _dbContextOptions = new DbContextOptionsBuilder<ConduitDbContext>()
                .UseSqlite(_connection)
                .Options;
            using (var ctx = new TestConduitDbContext(_dbContextOptions))
            {
                ctx.Database.EnsureCreated();
            }
            _dbContextFactory = new TestDbContextFactory(_dbContextOptions);

            _service = new AdminVirtualKeyService(
                _mockVirtualKeyRepository.Object,
                _mockSpendHistoryRepository.Object,
                _mockGroupRepository.Object,
                _mockLogger.Object,
                _mockModelProviderMappingRepository.Object,
                _mockModelCapabilityService.Object,
                _dbContextFactory,
                _mockCache.Object,
                _mockPublishEndpoint.Object,
                _mockMediaLifecycleService.Object);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _connection.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}