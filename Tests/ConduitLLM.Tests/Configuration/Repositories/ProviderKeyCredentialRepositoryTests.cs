using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Repositories;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Moq;

namespace ConduitLLM.Tests.Configuration.Repositories
{
    public partial class ProviderKeyCredentialRepositoryTests : IDisposable
    {
        private readonly ConduitDbContext _context;
        private readonly DbContextOptions<ConduitDbContext> _options;
        private readonly Mock<IDbContextFactory<ConduitDbContext>> _mockContextFactory;
        private readonly ProviderKeyCredentialRepository _repository;
        private readonly Mock<ILogger<ProviderKeyCredentialRepository>> _mockLogger;

        public ProviderKeyCredentialRepositoryTests()
        {
            _options = new DbContextOptionsBuilder<ConduitDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(warnings => warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _context = new ConduitDbContext(_options);
            _context.IsTestEnvironment = true;

            _mockContextFactory = new Mock<IDbContextFactory<ConduitDbContext>>();
            // The factory must return a new context each time but sharing the same in-memory database
            _mockContextFactory.Setup(x => x.CreateDbContextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    var ctx = new ConduitDbContext(_options);
                    ctx.IsTestEnvironment = true;
                    return ctx;
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
            var ctx = new ConduitDbContext(_options);
            ctx.IsTestEnvironment = true;
            return ctx;
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}
