using ConduitLLM.Admin.Services;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Messaging;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Tests.TestInfrastructure;


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
        private readonly Mock<IEventBus> _mockPublishEndpoint;
        private readonly Mock<ILogger<AdminModelCostService>> _mockLogger;
        private readonly AdminModelCostService _service;
        private readonly DbContextOptions<ConduitDbContext> _dbContextOptions;
        private readonly SqliteTestDatabase _database;

        public AdminModelCostServiceTests()
        {
            _mockModelCostRepository = new Mock<IModelCostRepository>();
            _mockRequestLogRepository = new Mock<IRequestLogRepository>();
            _mockDbContextFactory = new Mock<IDbContextFactory<ConduitDbContext>>();
            _mockPublishEndpoint = new Mock<IEventBus>();
            _mockLogger = new Mock<ILogger<AdminModelCostService>>();

            _database = new SqliteTestDatabase();
            _dbContextOptions = _database.Options;

            // Setup factory to create new contexts each time
            _mockDbContextFactory.Setup(x => x.CreateDbContextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => _database.CreateContext());

            _service = new AdminModelCostService(
                _mockModelCostRepository.Object,
                _mockRequestLogRepository.Object,
                _mockDbContextFactory.Object,
                _mockLogger.Object,
                _mockPublishEndpoint.Object);
        }

        public void Dispose()
        {
            _database.Dispose();
        }

        private ConduitDbContext CreateDbContext()
        {
            return _database.CreateContext();
        }

        private static void AddModels(ConduitDbContext context, params int[] modelIds)
        {
            foreach (var modelId in modelIds)
            {
                context.Models.Add(new Model
                {
                    Id = modelId,
                    Name = $"admin-cost-model-{modelId}",
                    Series = new ModelSeries
                    {
                        Id = modelId,
                        Name = $"admin-cost-series-{modelId}",
                        Parameters = "{}",
                        Author = new ModelAuthor
                        {
                            Id = modelId,
                            Name = $"admin-cost-author-{modelId}"
                        }
                    }
                });
            }
        }
    }
}
