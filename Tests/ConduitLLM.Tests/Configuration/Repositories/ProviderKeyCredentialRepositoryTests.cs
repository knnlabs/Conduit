using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Tests.TestInfrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Moq;

namespace ConduitLLM.Tests.Configuration.Repositories
{
    public partial class ProviderKeyCredentialRepositoryTests : IDisposable
    {
        private readonly ConduitDbContext _context;
        private readonly DbContextOptions<ConduitDbContext> _options;
        private readonly SqliteTestDatabase _database;
        private readonly FailingSaveChangesInterceptor _saveFailure;
        private readonly Mock<IDbContextFactory<ConduitDbContext>> _mockContextFactory;
        private readonly ProviderKeyCredentialRepository _repository;
        private readonly Mock<ILogger<ProviderKeyCredentialRepository>> _mockLogger;

        public ProviderKeyCredentialRepositoryTests()
        {
            _saveFailure = new FailingSaveChangesInterceptor();
            _database = new SqliteTestDatabase(_saveFailure);
            _options = _database.Options;
            _context = _database.CreateContext();

            _mockContextFactory = new Mock<IDbContextFactory<ConduitDbContext>>();
            // The factory must return a new context each time but sharing the same in-memory database
            _mockContextFactory.Setup(x => x.CreateDbContextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    return _database.CreateContext();
                });

            _mockLogger = new Mock<ILogger<ProviderKeyCredentialRepository>>();
            _repository = new ProviderKeyCredentialRepository(_mockContextFactory.Object, _mockLogger.Object);
        }

        /// <summary>
        /// Creates a fresh context to verify database state after repository operations.
        /// This is needed because the repository uses its own contexts through the factory.
        /// </summary>
        protected ConduitDbContext CreateVerificationContext()
        {
            return _database.CreateContext();
        }

        public void Dispose()
        {
            _context.Dispose();
            _database.Dispose();
        }
    }
}
