using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
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

        [Fact]
        public async Task InitializeDatabaseAsync_ReturnsTrue_WhenDatabaseConnects()
        {
            // Arrange
            _databaseFacadeMock.Setup(d => d.CanConnectAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _databaseFacadeMock.Setup(d => d.EnsureCreatedAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
            // We can't mock extension methods directly, so we'll skip this verification

            var initializer = new DatabaseInitializer(_dbContextFactoryMock.Object, _loggerMock.Object);

            // Act
            var result = await initializer.InitializeDatabaseAsync();

            // Assert
            Assert.True(result);
            _databaseFacadeMock.Verify(d => d.CanConnectAsync(It.IsAny<CancellationToken>()), Times.Once);
            _databaseFacadeMock.Verify(d => d.EnsureCreatedAsync(It.IsAny<CancellationToken>()), Times.Once);
            // We can't verify extension methods directly
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

        [Fact]
        public async Task InitializeDatabaseAsync_Succeeds_WithEnsureCreated()
        {
            // Arrange
            _databaseFacadeMock.Setup(d => d.CanConnectAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _databaseFacadeMock.Setup(d => d.EnsureCreatedAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
            // We can't mock extension methods directly, so we'll skip this verification

            var initializer = new DatabaseInitializer(_dbContextFactoryMock.Object, _loggerMock.Object);

            // Act
            var result = await initializer.InitializeDatabaseAsync();

            // Assert
            Assert.True(result);
            // We can't verify extension methods directly
        }
    }
}