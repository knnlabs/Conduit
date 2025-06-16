using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Data;
using ConduitLLM.Configuration.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

using Moq;

namespace ConduitLLM.Tests.TestHelpers
{
    /// <summary>
    /// Helper class for mocking database contexts and their related entities for testing
    /// </summary>
    public static class DbContextMockHelper
    {
        /// <summary>
        /// Creates a mock DbSet with the provided data that can be queried with LINQ
        /// </summary>
        /// <typeparam name="T">The entity type for the DbSet</typeparam>
        /// <param name="data">The data to include in the DbSet</param>
        /// <returns>A mock DbSet that can be used in place of a real DbSet</returns>
        public static Mock<DbSet<T>> CreateMockDbSet<T>(List<T>? data = null) where T : class
        {
            data ??= new List<T>();
            var queryableData = data.AsQueryable();
            var mockDbSet = new Mock<DbSet<T>>();

            // Setup the DbSet to work with LINQ queries
            mockDbSet.As<IQueryable<T>>().Setup(m => m.Provider).Returns(queryableData.Provider);
            mockDbSet.As<IQueryable<T>>().Setup(m => m.Expression).Returns(queryableData.Expression);
            mockDbSet.As<IQueryable<T>>().Setup(m => m.ElementType).Returns(queryableData.ElementType);
            mockDbSet.As<IQueryable<T>>().Setup(m => m.GetEnumerator()).Returns(() => queryableData.GetEnumerator());

            // Setup Find by ID
            mockDbSet.Setup(m => m.Find(It.IsAny<object[]>()))
                .Returns<object[]>(ids => data.FirstOrDefault(d => d.GetType().GetProperty("Id")?.GetValue(d)?.Equals(ids[0]) ?? false));

            // Setup DbSet.Add to add items to the data list
            mockDbSet.Setup(d => d.Add(It.IsAny<T>()))
                .Callback<T>(data.Add);

            // Setup DbSet.AddAsync
            mockDbSet.Setup(d => d.AddAsync(It.IsAny<T>(), It.IsAny<CancellationToken>()))
                .Callback<T, CancellationToken>((item, _) => data.Add(item))
                .Returns<T, CancellationToken>((entity, _) =>
                {
                    var mockEntry = new Mock<EntityEntry<T>>();
                    mockEntry.Setup(e => e.Entity).Returns(entity);
                    return new ValueTask<EntityEntry<T>>(mockEntry.Object);
                });

            // For any Include, Where, FirstOrDefault, etc. operations, we'll set up needed methods

            // We can't mock FirstOrDefaultAsync directly because it's an extension method
            // Instead, we provide a mock of the where clause and query that can be used by FirstOrDefaultAsync

            // Setup FindAsync
            mockDbSet.Setup(m => m.FindAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
                .Returns<object[], CancellationToken>((ids, _) =>
                    ValueTask.FromResult(data.FirstOrDefault(d => d.GetType().GetProperty("Id")?.GetValue(d)?.Equals(ids[0]) ?? false)));

            // We can't mock extension methods like Include directly
            // The tests will need to be refactored to avoid using these methods

            // Setup AsNoTracking - just return the same DbSet to allow chaining
            mockDbSet.Setup(m => m.AsNoTracking())
                .Returns(mockDbSet.Object);

            // Setup ToListAsync to return the data
            mockDbSet.Setup(m => m.ToListAsync(It.IsAny<CancellationToken>()))
                .Returns((CancellationToken _) => Task.FromResult(data.ToList()));

            return mockDbSet;
        }

        /// <summary>
        /// Creates a mock DbContext factory that returns the specified context
        /// </summary>
        /// <param name="context">The DbContext to return from the factory</param>
        /// <returns>A mock DbContextFactory</returns>
        public static Mock<IDbContextFactory<ConfigurationDbContext>> CreateMockDbContextFactory(ConfigurationDbContext context)
        {
            var mockFactory = new Mock<IDbContextFactory<ConfigurationDbContext>>();

            mockFactory.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(context);

            mockFactory.Setup(f => f.CreateDbContext())
                .Returns(context);

            return mockFactory;
        }

        /// <summary>
        /// Creates a real in-memory ConfigurationDbContext that can be used for testing
        /// </summary>
        /// <returns>A real ConfigurationDbContext using an in-memory database</returns>
        public static ConfigurationDbContext CreateInMemoryDbContext(string? dbName = null)
        {
            dbName ??= $"TestDb_{Guid.NewGuid()}";

            var options = new DbContextOptionsBuilder<ConfigurationDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .EnableSensitiveDataLogging()
                .Options;

            var context = new ConfigurationDbContext(options);

            // Ensure the database is created
            context.Database.EnsureCreated();

            return context;
        }

        /// <summary>
        /// Creates a simplified mock ConfigurationDbContext with optional entity data
        /// </summary>
        /// <param name="globalSettings">Optional list of GlobalSetting entities</param>
        /// <param name="requestLogs">Optional list of RequestLog entities</param>
        /// <param name="virtualKeys">Optional list of VirtualKey entities</param>
        /// <param name="modelCosts">Optional list of ModelCost entities</param>
        /// <param name="routerConfigs">Optional list of RouterConfigEntity entities</param>
        /// <param name="modelDeployments">Optional list of ModelDeploymentEntity entities</param>
        /// <param name="fallbackConfigs">Optional list of FallbackConfigurationEntity entities</param>
        /// <returns>A mock ConfigurationDbContext</returns>
        public static Mock<ConfigurationDbContext> CreateMockConfigurationDbContext(
            List<GlobalSetting>? globalSettings = null,
            List<RequestLog>? requestLogs = null,
            List<VirtualKey>? virtualKeys = null,
            List<ModelCost>? modelCosts = null,
            List<RouterConfigEntity>? routerConfigs = null,
            List<ModelDeploymentEntity>? modelDeployments = null,
            List<FallbackConfigurationEntity>? fallbackConfigs = null)
        {
            // Create a mock context
            var mockContext = new Mock<ConfigurationDbContext>(new DbContextOptionsBuilder<ConfigurationDbContext>()
                .UseInMemoryDatabase(databaseName: $"MockDb_{Guid.NewGuid()}")
                .Options);

            // Setup all the DbSets to return test data
            mockContext.Setup(c => c.GlobalSettings).Returns(CreateMockDbSet(globalSettings).Object);
            mockContext.Setup(c => c.RequestLogs).Returns(CreateMockDbSet(requestLogs).Object);
            mockContext.Setup(c => c.VirtualKeys).Returns(CreateMockDbSet(virtualKeys).Object);
            mockContext.Setup(c => c.ModelCosts).Returns(CreateMockDbSet(modelCosts).Object);
            mockContext.Setup(c => c.RouterConfigurations).Returns(CreateMockDbSet(routerConfigs).Object);
            mockContext.Setup(c => c.ModelDeployments).Returns(CreateMockDbSet(modelDeployments).Object);
            mockContext.Setup(c => c.FallbackConfigurations).Returns(CreateMockDbSet(fallbackConfigs).Object);

            // Setup SaveChangesAsync to return successful result
            mockContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            // Setup SaveChanges
            mockContext.Setup(c => c.SaveChanges())
                .Returns(1);

            // Setup Database property
            var mockDatabase = new Mock<DatabaseFacade>(mockContext.Object);
            mockDatabase.Setup(d => d.EnsureCreatedAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            mockDatabase.Setup(d => d.EnsureCreated())
                .Returns(true);

            mockContext.Setup(c => c.Database)
                .Returns(mockDatabase.Object);

            return mockContext;
        }
    }
}
