using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using ConduitLLM.Configuration.Data;
using ConduitLLM.Configuration;
using System.Threading;

namespace ConduitLLM.Tests.TestInfrastructure
{
    /// <summary>
    /// Base class for repository tests that provides isolated DbContext instances
    /// Each test gets its own independent SQLite in-memory database
    /// </summary>
    public abstract class RepositoryTestBase : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<ConduitDbContext> _options;
        private bool _disposed;

        protected RepositoryTestBase()
        {
            // Each test instance gets its own SQLite in-memory database
            // This ensures complete isolation between tests
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            _options = new DbContextOptionsBuilder<ConduitDbContext>()
                .UseSqlite(_connection)
                .EnableSensitiveDataLogging()
                .Options;

            // Create the schema once for this test instance
            using var context = new TestConduitDbContext(_options);
            context.Database.EnsureCreated();
        }

        /// <summary>
        /// Creates a fresh DbContext instance
        /// </summary>
        protected ConduitDbContext CreateContext()
        {
            return new TestConduitDbContext(_options);
        }

        /// <summary>
        /// Creates a DbContextFactory for repositories
        /// </summary>
        protected IDbContextFactory<ConduitDbContext> CreateDbContextFactory()
        {
            return new TestDbContextFactory(_options);
        }

        /// <summary>
        /// Seeds test data using foreign key IDs to avoid tracking issues
        /// </summary>
        protected void SeedData(Action<ConduitDbContext> seedAction)
        {
            using var context = CreateContext();
            seedAction(context);
            context.ChangeTracker.Clear();
        }

        /// <summary>
        /// Adds additional test data
        /// </summary>
        protected void AddTestData(Action<ConduitDbContext> seedAction)
        {
            using var context = CreateContext();
            seedAction(context);
            context.ChangeTracker.Clear();
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
                    _connection?.Dispose();
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
        private readonly DbContextOptions<ConduitDbContext> _options;

        public TestDbContextFactory(DbContextOptions<ConduitDbContext> options)
        {
            _options = options;
        }

        public ConduitDbContext CreateDbContext()
        {
            return new TestConduitDbContext(_options);
        }

        public Task<ConduitDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CreateDbContext());
        }
    }
}