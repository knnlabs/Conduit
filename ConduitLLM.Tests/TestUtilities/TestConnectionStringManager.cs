using System;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Data.Interfaces;

namespace ConduitLLM.Tests.TestUtilities
{
    /// <summary>
    /// Test implementation of IConnectionStringManager that always returns valid PostgreSQL connection info
    /// to satisfy the application's requirements while actually using in-memory database.
    /// </summary>
    internal class TestConnectionStringManager : IConnectionStringManager
    {
        public (string ProviderName, string ConnectionStringValue) GetProviderAndConnectionString(Action<string>? logger = null)
        {
            return GetProviderAndConnectionString(null, logger);
        }

        public (string ProviderName, string ConnectionStringValue) GetProviderAndConnectionString(string? serviceType, Action<string>? logger = null)
        {
            logger?.Invoke("[DB] Using test connection string manager for in-memory database");
            return ("postgres", "Host=localhost;Port=5432;Database=test;Username=test;Password=test");
        }

        public string ParsePostgresUrl(string postgresUrl)
        {
            return "Host=localhost;Port=5432;Database=test;Username=test;Password=test";
        }

        public string ParsePostgresUrl(string postgresUrl, string? serviceType)
        {
            return "Host=localhost;Port=5432;Database=test;Username=test;Password=test";
        }

        public void ValidateConnectionString(string providerName, string connectionString)
        {
            // No-op for tests
        }

        public string SanitizeConnectionString(string connectionString)
        {
            return connectionString;
        }

        public Task<bool> ValidatePostgreSQLConnectivityAsync(string connectionString, int maxRetries = 5, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }
    }
}