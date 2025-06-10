using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ConduitLLM.Tests.Data
{
    public class DatabaseInitializerTests
    {
        private readonly Mock<ILogger<DatabaseInitializer>> _loggerMock;
        private readonly Mock<IDbContextFactory<ConfigurationDbContext>> _dbContextFactoryMock;
        private readonly Mock<ConfigurationDbContext> _dbContextMock;
        private readonly Mock<DatabaseFacade> _databaseFacadeMock;

        public DatabaseInitializerTests()
        {
            _loggerMock = new Mock<ILogger<DatabaseInitializer>>();
            _dbContextFactoryMock = new Mock<IDbContextFactory<ConfigurationDbContext>>();
            _dbContextMock = new Mock<ConfigurationDbContext>(new DbContextOptions<ConfigurationDbContext>());
            _databaseFacadeMock = new Mock<DatabaseFacade>(_dbContextMock.Object);

            // Set up the Database property
            _dbContextMock.Setup(c => c.Database).Returns(_databaseFacadeMock.Object);

            // Set up the provider name
            _databaseFacadeMock.Setup(d => d.ProviderName).Returns("Microsoft.EntityFrameworkCore.Sqlite");

            // Set up the CreateDbContext method
            _dbContextFactoryMock.Setup(f => f.CreateDbContext()).Returns(_dbContextMock.Object);
            _dbContextFactoryMock.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(_dbContextMock.Object);
        }

        [Fact]
        public void Constructor_InitializesWithCorrectProvider()
        {
            // Arrange & Act
            var initializer = new DatabaseInitializer(_dbContextFactoryMock.Object, _loggerMock.Object);

            // Assert
            Assert.Equal("sqlite", initializer.GetDatabaseProviderType());
        }

        [Fact(Skip = "Test requires refactoring due to EF Core migration extension methods that cannot be mocked")]
        public async Task InitializeDatabaseAsync_ReturnsTrue_WhenDatabaseConnects()
        {
            // Arrange
            // Mock the database connection to succeed
            _databaseFacadeMock.Setup(d => d.CanConnectAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
            
            // Mock the database connection for table existence checks
            var mockConnection = new Mock<System.Data.Common.DbConnection>();
            var mockCommand = new Mock<System.Data.Common.DbCommand>();
            
            mockConnection.SetupGet(c => c.State).Returns(System.Data.ConnectionState.Open);
            mockConnection.Setup(c => c.CreateCommand()).Returns(mockCommand.Object);
            mockCommand.SetupSet(c => c.CommandText = It.IsAny<string>());
            
            // For SQLite, return 0 to indicate no migration history table exists
            mockCommand.Setup(c => c.ExecuteScalarAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => 0);
            
            _databaseFacadeMock.Setup(d => d.GetDbConnection()).Returns(mockConnection.Object);
            _databaseFacadeMock.Setup(d => d.OpenConnectionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            
            // Mock EnsureCreatedAsync to succeed
            _databaseFacadeMock.Setup(d => d.EnsureCreatedAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            
            // Mock the migrations methods - these are extension methods that we'll simulate
            // by ensuring the database operations succeed
            mockCommand.Setup(c => c.ExecuteNonQueryAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            var initializer = new DatabaseInitializer(_dbContextFactoryMock.Object, _loggerMock.Object);

            // Act
            var result = await initializer.InitializeDatabaseAsync();

            // Assert
            Assert.True(result);
            _databaseFacadeMock.Verify(d => d.CanConnectAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task InitializeDatabaseAsync_ReturnsFalse_WhenDatabaseCannotConnect()
        {
            // Arrange
            _databaseFacadeMock.Setup(d => d.CanConnectAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);

            var initializer = new DatabaseInitializer(_dbContextFactoryMock.Object, _loggerMock.Object);

            // Act
            var result = await initializer.InitializeDatabaseAsync(maxRetries: 1, retryDelayMs: 10);

            // Assert
            Assert.False(result);
            _databaseFacadeMock.Verify(d => d.CanConnectAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact(Skip = "Test requires refactoring due to EF Core migration extension methods that cannot be mocked")]
        public async Task InitializeDatabaseAsync_Succeeds_WithEnsureCreated()
        {
            // Arrange
            // Mock the database connection to succeed
            _databaseFacadeMock.Setup(d => d.CanConnectAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
            
            // Mock the database connection for table existence checks
            var mockConnection = new Mock<System.Data.Common.DbConnection>();
            var mockCommand = new Mock<System.Data.Common.DbCommand>();
            
            mockConnection.SetupGet(c => c.State).Returns(System.Data.ConnectionState.Open);
            mockConnection.Setup(c => c.CreateCommand()).Returns(mockCommand.Object);
            mockCommand.SetupSet(c => c.CommandText = It.IsAny<string>());
            
            // Setup different responses based on the query
            var callCount = 0;
            mockCommand.Setup(c => c.ExecuteScalarAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    callCount++;
                    // First call checks for migration history table - return 0 (doesn't exist)
                    if (callCount == 1) return 0;
                    // Second call checks for GlobalSettings table - return 0 (doesn't exist)
                    if (callCount == 2) return 0;
                    // Subsequent calls for table existence checks - return 1 (exists)
                    return 1;
                });
            
            _databaseFacadeMock.Setup(d => d.GetDbConnection()).Returns(mockConnection.Object);
            _databaseFacadeMock.Setup(d => d.OpenConnectionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            
            // Mock EnsureCreatedAsync to succeed
            _databaseFacadeMock.Setup(d => d.EnsureCreatedAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            
            // Mock ExecuteNonQueryAsync for CREATE TABLE and INSERT operations
            mockCommand.Setup(c => c.ExecuteNonQueryAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            var initializer = new DatabaseInitializer(_dbContextFactoryMock.Object, _loggerMock.Object);

            // Act
            var result = await initializer.InitializeDatabaseAsync();

            // Assert
            Assert.True(result);
            _databaseFacadeMock.Verify(d => d.EnsureCreatedAsync(It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}