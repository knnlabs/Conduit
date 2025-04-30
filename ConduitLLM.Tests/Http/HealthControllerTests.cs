using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using ConduitLLM.Configuration;
using ConduitLLM.Http.Controllers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Http
{
    public class HealthControllerTests
    {
        private readonly DbContextOptions<ConfigurationDbContext> _dbContextOptions;
        
        public HealthControllerTests()
        {
            // Setup in-memory database for testing
            _dbContextOptions = new DbContextOptionsBuilder<ConfigurationDbContext>()
                .UseInMemoryDatabase(databaseName: $"DbHealthCheck_{Guid.NewGuid()}")
                .Options;
        }
        
        [Fact]
        public async Task GetDatabaseHealth_ReturnsOk_WhenDatabaseIsHealthy()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<HealthController>>();
            var dbContextFactoryMock = new Mock<IDbContextFactory<ConfigurationDbContext>>();
            
            // Setup the factory to return our context
            using var dbContext = new ConfigurationDbContext(_dbContextOptions);
            dbContext.Database.EnsureCreated();
            
            dbContextFactoryMock
                .Setup(f => f.CreateDbContextAsync(default))
                .ReturnsAsync(dbContext);
            
            // Use our test controller to have full control over the test
            var controller = new TestHealthController(
                dbContextFactoryMock.Object,
                loggerMock.Object,
                simulateConnected: true,
                hasPendingMigrations: false);
            
            // Act
            var result = await controller.GetDatabaseHealthAsync();
            
            // Assert
            var okResult = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result);
            var response = Assert.IsType<DatabaseHealthResponse>(okResult.Value);
            Assert.Equal("healthy", response.Status);
            Assert.Null(response.Details);
        }
        
        [Fact]
        public async Task GetDatabaseHealth_ReturnsServiceUnavailable_WhenCannotConnect()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<HealthController>>();
            var dbContextFactoryMock = new Mock<IDbContextFactory<ConfigurationDbContext>>();
            
            // Setup the factory to return our context
            using var dbContext = new ConfigurationDbContext(_dbContextOptions);
            
            dbContextFactoryMock
                .Setup(f => f.CreateDbContextAsync(default))
                .ReturnsAsync(dbContext);
            
            // Use our test controller with connection issues
            var controller = new TestHealthController(
                dbContextFactoryMock.Object,
                loggerMock.Object,
                simulateConnected: false);
            
            // Act
            var result = await controller.GetDatabaseHealthAsync();
            
            // Assert
            var statusCodeResult = Assert.IsType<Microsoft.AspNetCore.Mvc.ObjectResult>(result);
            Assert.Equal((int)HttpStatusCode.ServiceUnavailable, statusCodeResult.StatusCode);
            
            var response = Assert.IsType<DatabaseHealthResponse>(statusCodeResult.Value);
            Assert.Equal("unhealthy", response.Status);
            Assert.Equal("Cannot connect to database", response.Details);
        }
        
        [Fact]
        public async Task GetDatabaseHealth_ReturnsServiceUnavailable_WhenPendingMigrations()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<HealthController>>();
            var dbContextFactoryMock = new Mock<IDbContextFactory<ConfigurationDbContext>>();
            
            // Setup the factory to return our context
            using var dbContext = new ConfigurationDbContext(_dbContextOptions);
            
            dbContextFactoryMock
                .Setup(f => f.CreateDbContextAsync(default))
                .ReturnsAsync(dbContext);
            
            // Use our test controller with pending migrations
            var controller = new TestHealthController(
                dbContextFactoryMock.Object,
                loggerMock.Object,
                simulateConnected: true,
                hasPendingMigrations: true);
            
            // Act
            var result = await controller.GetDatabaseHealthAsync();
            
            // Assert
            var statusCodeResult = Assert.IsType<Microsoft.AspNetCore.Mvc.ObjectResult>(result);
            Assert.Equal((int)HttpStatusCode.ServiceUnavailable, statusCodeResult.StatusCode);
            
            var response = Assert.IsType<DatabaseHealthResponse>(statusCodeResult.Value);
            Assert.Equal("unhealthy", response.Status);
            Assert.Equal("Database has pending migrations", response.Details);
        }
        
        [Fact]
        public async Task GetDatabaseHealth_ReturnsInternalServerError_WhenExceptionOccurs()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<HealthController>>();
            var dbContextFactoryMock = new Mock<IDbContextFactory<ConfigurationDbContext>>();
            
            dbContextFactoryMock
                .Setup(f => f.CreateDbContextAsync(default))
                .ThrowsAsync(new Exception("Test exception"));
            
            var controller = new HealthController(dbContextFactoryMock.Object, loggerMock.Object);
            
            // Act
            var result = await controller.GetDatabaseHealthAsync();
            
            // Assert
            var statusCodeResult = Assert.IsType<Microsoft.AspNetCore.Mvc.ObjectResult>(result);
            Assert.Equal((int)HttpStatusCode.InternalServerError, statusCodeResult.StatusCode);
            
            var response = Assert.IsType<DatabaseHealthResponse>(statusCodeResult.Value);
            Assert.Equal("unhealthy", response.Status);
            Assert.Equal("Error: Test exception", response.Details);
        }
    }
    
    // Test-specific controller that overrides the database interaction
    public class TestHealthController : HealthController
    {
        private readonly bool _hasPendingMigrations;
        private readonly IEnumerable<string> _pendingMigrations;
        private readonly bool _simulateConnected;
        
        public TestHealthController(
            IDbContextFactory<ConfigurationDbContext> dbContextFactory,
            ILogger<HealthController> logger,
            bool simulateConnected = true,
            bool hasPendingMigrations = false,
            IEnumerable<string>? pendingMigrations = null)
            : base(dbContextFactory, logger)
        {
            _simulateConnected = simulateConnected;
            _hasPendingMigrations = hasPendingMigrations;
            _pendingMigrations = pendingMigrations ?? (hasPendingMigrations ? new[] { "TestMigration" } : Array.Empty<string>());
        }
        
        // Override for testing connection status
        protected override async Task<bool> CanConnectAsync(ConfigurationDbContext dbContext)
        {
            return _simulateConnected;
        }
        
        // Override for testing migrations
        protected override Task<IEnumerable<string>> GetPendingMigrationsAsync(ConfigurationDbContext dbContext)
        {
            // Override the method to return our test data
            return Task.FromResult(_pendingMigrations);
        }
    }
}
