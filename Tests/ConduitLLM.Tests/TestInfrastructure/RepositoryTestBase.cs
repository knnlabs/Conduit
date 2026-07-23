using ConduitLLM.Configuration;
using Microsoft.EntityFrameworkCore;

namespace ConduitLLM.Tests.TestInfrastructure
{
    /// <summary>
    /// Base class for repository tests that provides isolated DbContext instances
    /// Each test gets its own independent SQLite in-memory database
    /// </summary>
    public abstract class RepositoryTestBase : IDisposable
    {
        private readonly SqliteTestDatabase _database;
        private bool _disposed;

        protected RepositoryTestBase()
        {
            _database = new SqliteTestDatabase();
        }

        protected SqliteTestDatabase Database => _database;

        /// <summary>
        /// Creates a fresh DbContext instance
        /// </summary>
        protected ConduitDbContext CreateContext()
        {
            return _database.CreateContext();
        }

        /// <summary>
        /// Creates a DbContextFactory for repositories
        /// </summary>
        protected IDbContextFactory<ConduitDbContext> CreateDbContextFactory()
        {
            return _database.CreateDbContextFactory();
        }

        /// <summary>
        /// Seeds test data using foreign key IDs to avoid tracking issues
        /// </summary>
        protected void SeedData(Action<ConduitDbContext> seedAction)
        {
            _database.Seed(seedAction);
        }

        /// <summary>
        /// Seeds test data asynchronously.
        /// </summary>
        protected Task SeedDataAsync(
            Func<ConduitDbContext, Task> seedAction,
            CancellationToken cancellationToken = default)
        {
            return _database.SeedAsync(seedAction, cancellationToken);
        }

        /// <summary>
        /// Adds additional test data
        /// </summary>
        protected void AddTestData(Action<ConduitDbContext> seedAction)
        {
            _database.Seed(seedAction);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _database.Dispose();
                }
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Test-specific DbContext that marks itself as a test environment
    /// </summary>
    public class TestConduitDbContext : ConduitDbContext
    {
        public TestConduitDbContext(DbContextOptions<ConduitDbContext> options) 
            : base(options)
        {
            IsTestEnvironment = true;
        }
    }

    /// <summary>
    /// Test implementation of IDbContextFactory
    /// </summary>
    public class TestDbContextFactory : IDbContextFactory<ConduitDbContext>
    {
        private readonly Func<ConduitDbContext> _createContext;

        public TestDbContextFactory(DbContextOptions<ConduitDbContext> options)
            : this(() => new TestConduitDbContext(options))
        {
        }

        public TestDbContextFactory(Func<ConduitDbContext> createContext)
        {
            _createContext = createContext;
        }

        public ConduitDbContext CreateDbContext()
        {
            return _createContext();
        }

        public Task<ConduitDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CreateDbContext());
        }
    }
}
