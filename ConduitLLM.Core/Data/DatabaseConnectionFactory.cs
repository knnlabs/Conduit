using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Data.Constants;
using ConduitLLM.Core.Data.Interfaces;

using Microsoft.Extensions.Logging;

using Npgsql;

namespace ConduitLLM.Core.Data
{
    /// <summary>
    /// Factory for creating database connections with proper pooling.
    /// </summary>
    public class DatabaseConnectionFactory : IDatabaseConnectionFactory
    {
        private readonly IConnectionStringManager _connectionStringManager;
        private readonly ILogger<DatabaseConnectionFactory> _logger;
        private readonly string _providerName;
        private readonly string _connectionString;

        /// <summary>
        /// Initializes a new instance of the <see cref="DatabaseConnectionFactory"/> class.
        /// </summary>
        /// <param name="connectionStringManager">The connection string manager.</param>
        /// <param name="logger">The logger.</param>
        public DatabaseConnectionFactory(
            IConnectionStringManager connectionStringManager,
            ILogger<DatabaseConnectionFactory> logger)
        {
            _connectionStringManager = connectionStringManager ?? throw new ArgumentNullException(nameof(connectionStringManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Get provider and connection string once at initialization
            (_providerName, _connectionString) = _connectionStringManager.GetProviderAndConnectionString(
                // lgtm [cs/cleartext-storage-of-sensitive-information]
                msg => _logger.LogDebug(msg));

            // Validate the connection string
            try
            {
                _connectionStringManager.ValidateConnectionString(_providerName, _connectionString);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Invalid connection string for provider: {Provider}", _providerName);
                throw;
            }
        }

        /// <inheritdoc/>
        public string ProviderName => _providerName;

        /// <inheritdoc/>
        public string GetSanitizedConnectionString()
        {
            return _connectionStringManager.SanitizeConnectionString(_connectionString);
        }

        /// <inheritdoc/>
        public DbConnection CreateConnection()
        {
            if (_providerName.ToLowerInvariant() != DatabaseConstants.POSTGRES_PROVIDER)
            {
                throw new InvalidOperationException($"Only PostgreSQL is supported. Invalid provider: {_providerName}");
            }

            var connection = new NpgsqlConnection(_connectionString);
            _logger.LogDebug("Created PostgreSQL connection");
            return connection;
        }

        /// <inheritdoc/>
        public async Task<DbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var connection = CreateConnection();
                await connection.OpenAsync(cancellationToken);
                return connection;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create connection for provider: {Provider}", _providerName);
                throw new InvalidOperationException($"Failed to create connection for provider: {_providerName}", ex);
            }
        }
    }
}
