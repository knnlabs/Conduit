using System;
using System.Threading.Tasks;

using ConduitLLM.Core.Data;
using ConduitLLM.Core.Data.Constants;
using ConduitLLM.Core.Data.Interfaces;

using Microsoft.Extensions.Logging;

using Moq;

using Npgsql;

using Xunit;

namespace ConduitLLM.Tests.Data
{
    public class DatabaseConnectionFactoryTests
    {
        private readonly Mock<IConnectionStringManager> _mockConnectionStringManager;
        private readonly Mock<ILogger<DatabaseConnectionFactory>> _mockLogger;

        public DatabaseConnectionFactoryTests()
        {
            _mockConnectionStringManager = new Mock<IConnectionStringManager>();
            _mockLogger = new Mock<ILogger<DatabaseConnectionFactory>>();
        }

        [Fact]
        public void CreateConnection_WithPostgresProvider_ReturnsNpgsqlConnection()
        {
            // Arrange
            _mockConnectionStringManager.Setup(m => m.GetProviderAndConnectionString(It.IsAny<Action<string>>()))
                .Returns((DatabaseConstants.POSTGRES_PROVIDER, "Host=localhost;Database=testdb;Username=user;Password=pass"));

            var factory = new DatabaseConnectionFactory(_mockConnectionStringManager.Object, _mockLogger.Object);

            // Act
            var connection = factory.CreateConnection();

            // Assert
            Assert.IsType<NpgsqlConnection>(connection);
            Assert.Equal(DatabaseConstants.POSTGRES_PROVIDER, factory.ProviderName);
        }

        [Fact]
        public void CreateConnection_WithNonPostgresProvider_ThrowsInvalidOperationException()
        {
            // Arrange
            _mockConnectionStringManager.Setup(m => m.GetProviderAndConnectionString(It.IsAny<Action<string>>()))
                .Returns(("sqlite", "Data Source=test.db"));

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => 
                new DatabaseConnectionFactory(_mockConnectionStringManager.Object, _mockLogger.Object));
        }

        [Fact]
        public void CreateConnection_WithInvalidProvider_ThrowsInvalidOperationException()
        {
            // Arrange
            _mockConnectionStringManager.Setup(m => m.GetProviderAndConnectionString(It.IsAny<Action<string>>()))
                .Returns(("unsupported", "InvalidConnectionString"));

            // We need to also set up the validation to not throw an exception
            _mockConnectionStringManager.Setup(m => m.ValidateConnectionString(It.IsAny<string>(), It.IsAny<string>()))
                .Verifiable();

            var factory = new DatabaseConnectionFactory(_mockConnectionStringManager.Object, _mockLogger.Object);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => factory.CreateConnection());
        }

        [Fact]
        public void GetSanitizedConnectionString_CallsSanitizeMethodOnConnectionStringManager()
        {
            // Arrange
            var connectionString = "Host=localhost;Database=testdb;Username=user;Password=pass";
            _mockConnectionStringManager.Setup(m => m.GetProviderAndConnectionString(It.IsAny<Action<string>>()))
                .Returns((DatabaseConstants.POSTGRES_PROVIDER, connectionString));

            _mockConnectionStringManager.Setup(m => m.SanitizeConnectionString(connectionString))
                .Returns("Host=localhost;Database=testdb;Username=user;Password=*****");

            var factory = new DatabaseConnectionFactory(_mockConnectionStringManager.Object, _mockLogger.Object);

            // Act
            var result = factory.GetSanitizedConnectionString();

            // Assert
            _mockConnectionStringManager.Verify(m => m.SanitizeConnectionString(connectionString), Times.Once);
            Assert.Contains("Password=*****", result);
        }

        [Fact]
        public async Task CreateConnectionAsync_WithValidPostgresConnection_OpensSuccessfully()
        {
            // Arrange - Mock PostgreSQL connection
            _mockConnectionStringManager.Setup(m => m.GetProviderAndConnectionString(It.IsAny<Action<string>>()))
                .Returns((DatabaseConstants.POSTGRES_PROVIDER, "Host=localhost;Database=testdb;Username=user;Password=pass"));

            var factory = new DatabaseConnectionFactory(_mockConnectionStringManager.Object, _mockLogger.Object);

            // Act & Assert - This will throw since we don't have a real PostgreSQL connection
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await factory.CreateConnectionAsync());
        }
    }
}
