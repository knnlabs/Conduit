using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Data.Health;
using ConduitLLM.Core.Data.Interfaces;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

using Moq;
using Moq.Protected;

using Xunit;

using ItExpr = Moq.Protected.ItExpr;

namespace ConduitLLM.Tests.Data.Health
{
    public class DatabaseHealthCheckTests
    {
        private readonly Mock<IDatabaseConnectionFactory> _mockConnectionFactory;
        private readonly Mock<ILogger<DatabaseHealthCheck>> _mockLogger;

        public DatabaseHealthCheckTests()
        {
            _mockConnectionFactory = new Mock<IDatabaseConnectionFactory>();
            _mockLogger = new Mock<ILogger<DatabaseHealthCheck>>();
        }

        [Fact]
        public async Task CheckHealthAsync_WhenDatabaseIsHealthy_ReturnsHealthy()
        {
            // Arrange
            var mockConnection = new Mock<DbConnection>();
            var mockCommand = new Mock<DbCommand>();
            
            // Setup connection to return the mock command
            mockConnection.Protected()
                .Setup<DbCommand>("CreateDbCommand")
                .Returns(mockCommand.Object);
            
            mockConnection.Setup(c => c.State).Returns(ConnectionState.Closed);
            mockConnection.Setup(c => c.OpenAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            
            // Setup command to return success
            mockCommand.Setup(c => c.ExecuteScalarAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(1); // SELECT 1 returns 1
            
            mockCommand.SetupProperty(c => c.CommandText);
            mockCommand.SetupProperty(c => c.CommandTimeout);
            
            _mockConnectionFactory.Setup(f => f.CreateConnectionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockConnection.Object);
            
            _mockConnectionFactory.Setup(f => f.ProviderName)
                .Returns("postgres");
            
            var healthCheck = new DatabaseHealthCheck(_mockConnectionFactory.Object, _mockLogger.Object);
            var context = new HealthCheckContext();
            
            // Act
            var result = await healthCheck.CheckHealthAsync(context);
            
            // Assert
            Assert.Equal(HealthStatus.Healthy, result.Status);
            Assert.Contains("postgres", result.Description);
        }

        [Fact]
        public async Task CheckHealthAsync_WhenConnectionThrowsException_ReturnsUnhealthy()
        {
            // Arrange
            _mockConnectionFactory.Setup(f => f.CreateConnectionAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Cannot connect to database"));
            
            _mockConnectionFactory.Setup(f => f.ProviderName)
                .Returns("postgres");
            
            var healthCheck = new DatabaseHealthCheck(_mockConnectionFactory.Object, _mockLogger.Object);
            var context = new HealthCheckContext();
            
            // Act
            var result = await healthCheck.CheckHealthAsync(context);
            
            // Assert
            Assert.Equal(HealthStatus.Unhealthy, result.Status);
            Assert.Contains("Database connection failed", result.Description);
            Assert.NotNull(result.Exception);
        }

        [Fact]
        public async Task CheckHealthAsync_WhenQueryThrowsException_ReturnsUnhealthy()
        {
            // Arrange
            var mockConnection = new Mock<DbConnection>();
            var mockCommand = new Mock<DbCommand>();
            
            // Setup connection to return the mock command
            mockConnection.Protected()
                .Setup<DbCommand>("CreateDbCommand")
                .Returns(mockCommand.Object);
            
            mockConnection.Setup(c => c.State).Returns(ConnectionState.Closed);
            mockConnection.Setup(c => c.OpenAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            
            // Setup command to throw exception
            mockCommand.Setup(c => c.ExecuteScalarAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Query failed"));
            
            mockCommand.SetupProperty(c => c.CommandText);
            mockCommand.SetupProperty(c => c.CommandTimeout);
            
            _mockConnectionFactory.Setup(f => f.CreateConnectionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockConnection.Object);
            
            _mockConnectionFactory.Setup(f => f.ProviderName)
                .Returns("sqlite");
            
            var healthCheck = new DatabaseHealthCheck(_mockConnectionFactory.Object, _mockLogger.Object);
            var context = new HealthCheckContext();
            
            // Act
            var result = await healthCheck.CheckHealthAsync(context);
            
            // Assert
            Assert.Equal(HealthStatus.Unhealthy, result.Status);
            Assert.Contains("Database connection failed", result.Description);
        }

        [Fact]
        public async Task CheckHealthAsync_WithUnsupportedProvider_ReturnsUnhealthy()
        {
            // Arrange
            var mockConnection = new Mock<DbConnection>();
            var mockCommand = new Mock<DbCommand>();
            
            // Setup connection to return the mock command
            mockConnection.Protected()
                .Setup<DbCommand>("CreateDbCommand")
                .Returns(mockCommand.Object);
            
            mockConnection.Setup(c => c.State).Returns(ConnectionState.Closed);
            mockConnection.Setup(c => c.OpenAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            
            _mockConnectionFactory.Setup(f => f.CreateConnectionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockConnection.Object);
            
            _mockConnectionFactory.Setup(f => f.ProviderName)
                .Returns("unknown");
            
            var healthCheck = new DatabaseHealthCheck(_mockConnectionFactory.Object, _mockLogger.Object);
            var context = new HealthCheckContext();
            
            // Act
            var result = await healthCheck.CheckHealthAsync(context);
            
            // Assert
            Assert.Equal(HealthStatus.Unhealthy, result.Status);
            Assert.Contains("Unsupported provider for health check", result.Exception?.Message);
        }

        [Theory]
        [InlineData("postgres")]
        [InlineData("sqlite")]
        public async Task CheckHealthAsync_WithSupportedProviders_ExecutesCorrectQuery(string providerName)
        {
            // Arrange
            var mockConnection = new Mock<DbConnection>();
            var mockCommand = new Mock<DbCommand>();
            string? executedCommand = null;
            
            // Setup connection to return the mock command
            mockConnection.Protected()
                .Setup<DbCommand>("CreateDbCommand")
                .Returns(mockCommand.Object);
            
            mockConnection.Setup(c => c.State).Returns(ConnectionState.Closed);
            mockConnection.Setup(c => c.OpenAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            
            // Setup command
            mockCommand.Setup(c => c.ExecuteScalarAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);
            
            mockCommand.SetupProperty(c => c.CommandText);
            mockCommand.SetupProperty(c => c.CommandTimeout);
            
            // Capture the command text when it's set
            mockCommand.SetupSet(c => c.CommandText = It.IsAny<string>())
                .Callback<string>(cmd => executedCommand = cmd);
            
            _mockConnectionFactory.Setup(f => f.CreateConnectionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockConnection.Object);
            
            _mockConnectionFactory.Setup(f => f.ProviderName)
                .Returns(providerName);
            
            var healthCheck = new DatabaseHealthCheck(_mockConnectionFactory.Object, _mockLogger.Object);
            var context = new HealthCheckContext();
            
            // Act
            var result = await healthCheck.CheckHealthAsync(context);
            
            // Assert
            Assert.Equal(HealthStatus.Healthy, result.Status);
            Assert.Equal("SELECT 1", executedCommand);
        }

        [Fact]
        public async Task CheckHealthAsync_LogsDebugInformation()
        {
            // Arrange
            var mockConnection = new Mock<DbConnection>();
            var mockCommand = new Mock<DbCommand>();
            
            // Setup connection to return the mock command
            mockConnection.Protected()
                .Setup<DbCommand>("CreateDbCommand")
                .Returns(mockCommand.Object);
            
            mockConnection.Setup(c => c.State).Returns(ConnectionState.Closed);
            mockConnection.Setup(c => c.OpenAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            
            // Setup command
            mockCommand.Setup(c => c.ExecuteScalarAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);
            
            mockCommand.SetupProperty(c => c.CommandText);
            
            _mockConnectionFactory.Setup(f => f.CreateConnectionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockConnection.Object);
            
            _mockConnectionFactory.Setup(f => f.ProviderName)
                .Returns("postgres");
            
            var healthCheck = new DatabaseHealthCheck(_mockConnectionFactory.Object, _mockLogger.Object);
            var context = new HealthCheckContext();
            
            // Act
            await healthCheck.CheckHealthAsync(context);
            
            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Checking database health")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task CheckHealthAsync_OnError_LogsError()
        {
            // Arrange
            var exception = new InvalidOperationException("Connection failed");
            
            _mockConnectionFactory.Setup(f => f.CreateConnectionAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(exception);
            
            _mockConnectionFactory.Setup(f => f.ProviderName)
                .Returns("postgres");
            
            var healthCheck = new DatabaseHealthCheck(_mockConnectionFactory.Object, _mockLogger.Object);
            var context = new HealthCheckContext();
            
            // Act
            await healthCheck.CheckHealthAsync(context);
            
            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Database health check failed")),
                    exception,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
    }
}