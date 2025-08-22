using ConduitLLM.Admin.Controllers;
using ConduitLLM.Admin.Services;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Repositories;

using MassTransit;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

using Moq;

namespace ConduitLLM.Tests.Admin.Integration
{
    /// <summary>
    /// Integration tests for the Model Cost feature, testing the complete flow
    /// from controller through service to repository including model-cost mappings
    /// </summary>
    public partial class ModelCostIntegrationTests : IDisposable
    {
        private readonly DbContextOptions<ConduitDbContext> _dbContextOptions;
        private readonly ConduitDbContext _dbContext;
        private readonly IModelCostRepository _modelCostRepository;
        private readonly IRequestLogRepository _requestLogRepository;
        private readonly IModelProviderMappingRepository _modelMappingRepository;
        private readonly AdminModelCostService _service;
        private readonly ModelCostsController _controller;
        private readonly Mock<IPublishEndpoint> _mockPublishEndpoint;
        private readonly Mock<ILogger<AdminModelCostService>> _mockServiceLogger;
        private readonly Mock<ILogger<ModelCostsController>> _mockControllerLogger;
        private readonly Mock<ILogger<ModelCostRepository>> _mockCostRepoLogger;
        private readonly Mock<ILogger<RequestLogRepository>> _mockRequestLogRepoLogger;
        private readonly Mock<ILogger<ModelProviderMappingRepository>> _mockMappingRepoLogger;

        public ModelCostIntegrationTests()
        {
            // Setup in-memory database with transaction warning suppressed
            _dbContextOptions = new DbContextOptionsBuilder<ConduitDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            _dbContext = new ConduitDbContext(_dbContextOptions);

            // Setup repository loggers
            _mockCostRepoLogger = new Mock<ILogger<ModelCostRepository>>();
            _mockRequestLogRepoLogger = new Mock<ILogger<RequestLogRepository>>();
            _mockMappingRepoLogger = new Mock<ILogger<ModelProviderMappingRepository>>();

            // Create DbContextFactory for repositories and service
            var mockDbContextFactory = new Mock<IDbContextFactory<ConduitDbContext>>();
            mockDbContextFactory.Setup(x => x.CreateDbContextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new ConduitDbContext(_dbContextOptions));

            // Create real repositories with required dependencies
            _modelCostRepository = new ModelCostRepository(mockDbContextFactory.Object, _mockCostRepoLogger.Object);
            _requestLogRepository = new RequestLogRepository(mockDbContextFactory.Object, _mockRequestLogRepoLogger.Object);
            _modelMappingRepository = new ModelProviderMappingRepository(mockDbContextFactory.Object, _mockMappingRepoLogger.Object);

            // Setup mocks for non-essential dependencies
            _mockPublishEndpoint = new Mock<IPublishEndpoint>();
            _mockServiceLogger = new Mock<ILogger<AdminModelCostService>>();
            _mockControllerLogger = new Mock<ILogger<ModelCostsController>>();

            // Create real service with real repositories
            _service = new AdminModelCostService(
                _modelCostRepository,
                _requestLogRepository,
                mockDbContextFactory.Object,
                _mockPublishEndpoint.Object,
                _mockServiceLogger.Object);

            // Create controller with real service
            _controller = new ModelCostsController(_service, _mockControllerLogger.Object);
        }

        public void Dispose()
        {
            _dbContext?.Dispose();
        }

        #region Setup Helpers

        private async Task<int> SetupTestDataAsync()
        {
            // Create a test provider
            var provider = new Provider
            {
                ProviderName = "Test Provider",
                ProviderType = ProviderType.OpenAI,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.Providers.Add(provider);
            await _dbContext.SaveChangesAsync();

            // Create test model provider mappings
            var mappings = new List<ModelProviderMapping>
            {
                new ModelProviderMapping
                {
                    ModelAlias = "gpt-4",
                    ModelId = 1,
                    ProviderModelId = "gpt-4",
                    ProviderId = provider.Id,
                    IsEnabled = true,
                    CreatedAt = DateTime.UtcNow
                },
                new ModelProviderMapping
                {
                    ModelAlias = "gpt-3.5-turbo",
                    ModelId = 1,
                    ProviderModelId = "gpt-3.5-turbo",
                    ProviderId = provider.Id,
                    IsEnabled = true,
                    CreatedAt = DateTime.UtcNow
                },
                new ModelProviderMapping
                {
                    ModelAlias = "text-embedding-ada-002",
                    ModelId = 1,
                    ProviderModelId = "text-embedding-ada-002",
                    ProviderId = provider.Id,
                    IsEnabled = true,
                    CreatedAt = DateTime.UtcNow
                }
            };
            _dbContext.ModelProviderMappings.AddRange(mappings);
            await _dbContext.SaveChangesAsync();

            return provider.Id;
        }

        #endregion

    }
}