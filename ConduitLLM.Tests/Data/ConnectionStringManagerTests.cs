using System;
using ConduitLLM.Core.Data;
using ConduitLLM.Core.Data.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Data
{
    public class ConnectionStringManagerTests
    {
        private readonly Mock<ILogger<ConnectionStringManager>> _mockLogger;
        private readonly ConnectionStringManager _connectionStringManager;

        public ConnectionStringManagerTests()
        {
            _mockLogger = new Mock<ILogger<ConnectionStringManager>>();
            _connectionStringManager = new ConnectionStringManager(_mockLogger.Object);
        }

        [Fact]
        public void GetProviderAndConnectionString_WithValidPostgresUrl_ReturnsPostgresProvider()
        {
            // Arrange
            Environment.SetEnvironmentVariable(DatabaseConstants.DATABASE_URL_ENV, "postgresql://user:pass@localhost:5432/testdb");

            try
            {
                // Act
                var (providerName, connectionStringValue) = _connectionStringManager.GetProviderAndConnectionString();

                // Assert
                Assert.Equal(DatabaseConstants.POSTGRES_PROVIDER, providerName);
                Assert.Contains("Host=localhost", connectionStringValue);
                Assert.Contains("Username=user", connectionStringValue);
                Assert.Contains("Password=pass", connectionStringValue);
            }
            finally
            {
                // Cleanup
                Environment.SetEnvironmentVariable(DatabaseConstants.DATABASE_URL_ENV, null);
            }
        }

        [Fact]
        public void GetProviderAndConnectionString_WithInvalidPrefix_ReturnsSqliteProvider()
        {
            // Arrange
            Environment.SetEnvironmentVariable(DatabaseConstants.DATABASE_URL_ENV, "mysql://user:pass@localhost:3306/testdb");

            try
            {
                // Act
                var (providerName, connectionStringValue) = _connectionStringManager.GetProviderAndConnectionString();

                // Assert
                Assert.Equal(DatabaseConstants.SQLITE_PROVIDER, providerName);
                // Accept either default database name or test database name
                Assert.True(
                    connectionStringValue.Contains("Data Source=ConduitConfig.db") || 
                    connectionStringValue.Contains("Data Source=test_sqlite.db"),
                    $"Connection string '{connectionStringValue}' does not contain expected database name");
            }
            finally
            {
                // Cleanup
                Environment.SetEnvironmentVariable(DatabaseConstants.DATABASE_URL_ENV, null);
            }
        }

        [Fact]
        public void GetProviderAndConnectionString_WithCustomSqlitePath_ReturnsSqliteWithCustomPath()
        {
            // Arrange
            // Clear DATABASE_URL to ensure SQLite is used
            var originalDatabaseUrl = Environment.GetEnvironmentVariable(DatabaseConstants.DATABASE_URL_ENV);
            Environment.SetEnvironmentVariable(DatabaseConstants.DATABASE_URL_ENV, null);
            Environment.SetEnvironmentVariable(DatabaseConstants.SQLITE_PATH_ENV, "/custom/path/database.db");

            try
            {
                // Act
                var (providerName, connectionStringValue) = _connectionStringManager.GetProviderAndConnectionString();

                // Assert
                Assert.Equal(DatabaseConstants.SQLITE_PROVIDER, providerName);
                Assert.Contains("Data Source=/custom/path/database.db", connectionStringValue);
            }
            finally
            {
                // Cleanup
                Environment.SetEnvironmentVariable(DatabaseConstants.SQLITE_PATH_ENV, null);
                Environment.SetEnvironmentVariable(DatabaseConstants.DATABASE_URL_ENV, originalDatabaseUrl);
            }
        }

        [Fact]
        public void GetProviderAndConnectionString_WithNoEnvironmentVars_ReturnsDefaultSqlite()
        {
            // Arrange - clear any env vars
            Environment.SetEnvironmentVariable(DatabaseConstants.DATABASE_URL_ENV, null);
            Environment.SetEnvironmentVariable(DatabaseConstants.SQLITE_PATH_ENV, null);

            // Act
            var (providerName, connectionStringValue) = _connectionStringManager.GetProviderAndConnectionString();

            // Assert
            Assert.Equal(DatabaseConstants.SQLITE_PROVIDER, providerName);
            Assert.Equal($"Data Source={DatabaseConstants.DEFAULT_SQLITE_DATABASE}", connectionStringValue);
        }

        [Fact]
        public void ParsePostgresUrl_WithValidUrl_ReturnsParsedConnectionString()
        {
            // Arrange
            var url = "postgresql://testuser:testpass@localhost:5432/testdb?sslmode=require";

            // Act
            var result = _connectionStringManager.ParsePostgresUrl(url);

            // Assert
            Assert.Contains("Host=localhost", result);
            Assert.Contains("Port=5432", result);
            Assert.Contains("Database=testdb", result);
            Assert.Contains("Username=testuser", result);
            Assert.Contains("Password=testpass", result);
            Assert.Contains("sslmode=require", result);
        }

        [Fact]
        public void ParsePostgresUrl_WithInvalidUrl_ThrowsInvalidOperationException()
        {
            // Arrange
            var url = "invalid-url";

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => _connectionStringManager.ParsePostgresUrl(url));
        }

        [Fact]
        public void ParsePostgresUrl_WithNullUrl_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => _connectionStringManager.ParsePostgresUrl(null!));
        }

        [Fact]
        public void SanitizeConnectionString_WithPasswordInConnectionString_MasksPassword()
        {
            // Arrange
            var connectionString = "Host=localhost;Port=5432;Database=testdb;Username=testuser;Password=secret123";

            // Act
            var result = _connectionStringManager.SanitizeConnectionString(connectionString);

            // Assert
            Assert.Contains("Password=*****", result);
            Assert.DoesNotContain("Password=secret123", result);
        }

        [Fact]
        public void ValidateConnectionString_WithValidPostgresConnectionString_DoesNotThrow()
        {
            // Arrange
            var connStr = "Host=localhost;Port=5432;Database=testdb;Username=testuser;Password=testpass";

            // Act & Assert
            _connectionStringManager.ValidateConnectionString(DatabaseConstants.POSTGRES_PROVIDER, connStr);
        }

        [Fact]
        public void ValidateConnectionString_WithInvalidPostgresConnectionString_ThrowsInvalidOperationException()
        {
            // Arrange
            var connStr = "Host=localhost;Port=5432"; // Missing database, username, and password

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => 
                _connectionStringManager.ValidateConnectionString(DatabaseConstants.POSTGRES_PROVIDER, connStr));
        }

        [Fact]
        public void ValidateConnectionString_WithValidSqliteConnectionString_DoesNotThrow()
        {
            // Arrange
            var connStr = "Data Source=/path/to/database.db";

            // Act & Assert
            _connectionStringManager.ValidateConnectionString(DatabaseConstants.SQLITE_PROVIDER, connStr);
        }

        [Fact]
        public void ValidateConnectionString_WithInvalidSqliteConnectionString_ThrowsInvalidOperationException()
        {
            // Arrange
            var connStr = "Invalid=Connection;String=Value";

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => 
                _connectionStringManager.ValidateConnectionString(DatabaseConstants.SQLITE_PROVIDER, connStr));
        }

        [Fact]
        public void ValidateConnectionString_WithUnsupportedProvider_ThrowsArgumentException()
        {
            // Arrange
            var connStr = "Data Source=test.db";

            // Act & Assert
            Assert.Throws<ArgumentException>(() => 
                _connectionStringManager.ValidateConnectionString("unsupported", connStr));
        }
    }
}