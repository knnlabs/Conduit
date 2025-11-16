using System.Text.RegularExpressions;

using ConduitLLM.Core.Data.Constants;
using ConduitLLM.Core.Data.Interfaces;

using Microsoft.Extensions.Logging;

using Npgsql;

namespace ConduitLLM.Core.Data
{
    /// <summary>
    /// Provides functionality for managing database connection strings including
    /// parsing, validation, and sanitization.
    /// </summary>
    public class ConnectionStringManager : IConnectionStringManager
    {
        private readonly ILogger<ConnectionStringManager>? _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConnectionStringManager"/> class.
        /// </summary>
        /// <param name="logger">Optional logger for database connection operations.</param>
        public ConnectionStringManager(ILogger<ConnectionStringManager>? logger = null)
        {
            _logger = logger;
        }

        /// <inheritdoc/>
        public (string ProviderName, string ConnectionStringValue) GetProviderAndConnectionString(Action<string>? logger = null)
        {
            return GetProviderAndConnectionString(null, logger);
        }

        /// <summary>
        /// Gets the database provider name and connection string based on environment configuration.
        /// </summary>
        /// <param name="serviceType">Optional service type to apply service-specific connection pool settings (e.g., "CoreAPI", "AdminAPI", "WebUI")</param>
        /// <param name="logger">Optional logger action for database connection operations.</param>
        /// <returns>A tuple containing the provider name and connection string.</returns>
        public (string ProviderName, string ConnectionStringValue) GetProviderAndConnectionString(string? serviceType, Action<string>? logger = null)
        {
            // Use either the provided logger action or our internal logger if available
            // lgtm [cs/cleartext-storage-of-sensitive-information]
            Action<string> logAction = logger ?? (message => _logger?.LogInformation(message));

            // Check for PostgreSQL DATABASE_URL
            var databaseUrl = Environment.GetEnvironmentVariable(DatabaseConstants.DATABASE_URL_ENV);
            if (string.IsNullOrEmpty(databaseUrl))
            {
                var errorMsg = "DATABASE_URL environment variable is not set. PostgreSQL is required for Conduit.";
                logAction($"[DB] Error: {errorMsg}");
                throw new InvalidOperationException(errorMsg);
            }

            if (!databaseUrl.StartsWith(DatabaseConstants.POSTGRES_URL_PREFIX, StringComparison.OrdinalIgnoreCase) &&
                !databaseUrl.StartsWith(DatabaseConstants.POSTGRESQL_URL_PREFIX, StringComparison.OrdinalIgnoreCase))
            {
                var errorMsg = $"Invalid DATABASE_URL prefix. Must start with '{DatabaseConstants.POSTGRES_URL_PREFIX}' or '{DatabaseConstants.POSTGRESQL_URL_PREFIX}'.";
                logAction($"[DB] Error: {errorMsg}");
                throw new InvalidOperationException(errorMsg);
            }

            try
            {
                var connStr = ParsePostgresUrl(databaseUrl, serviceType);
                ValidateConnectionString(DatabaseConstants.POSTGRES_PROVIDER, connStr);
                logAction($"[DB] Using provider: {DatabaseConstants.POSTGRES_PROVIDER}, connection: {SanitizeConnectionString(connStr)}");
                return (DatabaseConstants.POSTGRES_PROVIDER, connStr);
            }
            catch (Exception ex)
            {
                var errorMsg = $"Failed to parse PostgreSQL URL: {ex.Message}";
                logAction($"[DB] Error: {errorMsg}");
                throw new InvalidOperationException(errorMsg, ex);
            }
        }

        /// <inheritdoc/>
        public string ParsePostgresUrl(string postgresUrl)
        {
            return ParsePostgresUrl(postgresUrl, null);
        }

        /// <summary>
        /// Validates PostgreSQL connectivity with exponential backoff retry logic.
        /// </summary>
        /// <param name="connectionString">The PostgreSQL connection string to validate.</param>
        /// <param name="maxRetries">Maximum number of retry attempts (default: 5).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if connection is successful, false otherwise.</returns>
        public async Task<bool> ValidatePostgreSQLConnectivityAsync(
            string connectionString, 
            int maxRetries = 5, 
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                _logger?.LogError("Connection string is null or empty");
                return false;
            }

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    using var connection = new NpgsqlConnection(connectionString);
                    await connection.OpenAsync(cancellationToken);
                    
                    // Test the connection with a simple query
                    using var command = new NpgsqlCommand("SELECT 1", connection);
                    await command.ExecuteScalarAsync(cancellationToken);
                    
                    _logger?.LogInformation("PostgreSQL connection validated successfully");
                    return true;
                }
                catch (Exception ex) when (attempt < maxRetries - 1)
                {
                    // Calculate exponential backoff: 1s, 2s, 4s, 8s, 16s
                    var delayMs = (int)Math.Pow(2, attempt) * 1000;
                    
                    _logger?.LogWarning(
                        "PostgreSQL connection attempt {Attempt}/{MaxRetries} failed. Retrying in {DelayMs}ms. Error: {Message}",
                        attempt + 1, maxRetries, delayMs, ex.Message);
                    
                    await Task.Delay(delayMs, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, 
                        "PostgreSQL connection failed after {MaxRetries} attempts", 
                        maxRetries);
                    return false;
                }
            }

            return false;
        }

        /// <summary>
        /// Parses a PostgreSQL URL into a standard .NET connection string format.
        /// </summary>
        /// <param name="postgresUrl">The PostgreSQL URL to parse (e.g., postgres://user:pass@host:port/database).</param>
        /// <param name="serviceType">Optional service type to apply service-specific connection pool settings (e.g., "CoreAPI", "AdminAPI", "WebUI")</param>
        /// <returns>A properly formatted PostgreSQL connection string.</returns>
        /// <exception cref="ArgumentNullException">Thrown when postgresUrl is null or empty.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the URL format is invalid.</exception>
        public string ParsePostgresUrl(string postgresUrl, string? serviceType)
        {
            if (string.IsNullOrEmpty(postgresUrl))
            {
                throw new ArgumentNullException(nameof(postgresUrl), "PostgreSQL URL cannot be null or empty");
            }

            // Accepts both postgres:// and postgresql://
            var pattern = @"^(postgres(?:ql)?):\/\/(?<user>[^:]+):(?<password>[^@]+)@(?<host>[^:/]+)(?::(?<port>\d+))?\/(?<database>[^?]+)";
            var match = Regex.Match(postgresUrl, pattern, RegexOptions.IgnoreCase);

            if (!match.Success)
            {
                _logger?.LogError("Invalid PostgreSQL URL format: {SanitizedUrl}",
                    Regex.Replace(postgresUrl, ":[^@]+@", ":****@"));
                throw new InvalidOperationException(DatabaseConstants.ERR_INVALID_POSTGRES_URL);
            }

            var user = match.Groups["user"].Value;
            var password = match.Groups["password"].Value;
            var host = match.Groups["host"].Value;
            var port = match.Groups["port"].Success
                ? match.Groups["port"].Value
                : DatabaseConstants.DEFAULT_POSTGRES_PORT;
            var database = match.Groups["database"].Value;

            // Handle optional query parameters
            var uri = new Uri(postgresUrl);
            var query = uri.Query;
            var queryString = string.IsNullOrEmpty(query) ? string.Empty : query.TrimStart('?');

            // Add connection pooling parameters if not specified in the URL
            var poolingParams = string.Empty;
            if (DatabaseConstants.POOLING_ENABLED &&
                !queryString.Contains("Pooling=") &&
                !queryString.Contains("pooling="))
            {
                // Determine pool sizes based on service type
                int minPoolSize, maxPoolSize;
                switch (serviceType?.ToUpperInvariant())
                {
                    case "COREAPI":
                        minPoolSize = DatabaseConstants.CORE_API_MIN_POOL_SIZE;
                        maxPoolSize = DatabaseConstants.CORE_API_MAX_POOL_SIZE;
                        break;
                    case "ADMINAPI":
                        minPoolSize = DatabaseConstants.ADMIN_API_MIN_POOL_SIZE;
                        maxPoolSize = DatabaseConstants.ADMIN_API_MAX_POOL_SIZE;
                        break;
                    case "WEBUI":
                        minPoolSize = DatabaseConstants.WEBUI_MIN_POOL_SIZE;
                        maxPoolSize = DatabaseConstants.WEBUI_MAX_POOL_SIZE;
                        break;
                    default:
                        minPoolSize = DatabaseConstants.MIN_POOL_SIZE;
                        maxPoolSize = DatabaseConstants.MAX_POOL_SIZE;
                        break;
                }

                poolingParams = $";Pooling=true;MinPoolSize={minPoolSize};" +
                               $"MaxPoolSize={maxPoolSize};" +
                               $"ConnectionLifetime={DatabaseConstants.CONNECTION_LIFETIME_SECONDS};" +
                               $"ConnectionIdleLifetime={DatabaseConstants.CONNECTION_IDLE_LIFETIME_SECONDS};" +
                               $"IncludeErrorDetail=true";
            }

            // Build the connection string
            var connStr = $"Host={host};Port={port};Database={database};Username={user};Password={password}";

            // Add query parameters if present
            if (!string.IsNullOrEmpty(queryString))
            {
                connStr += ";" + queryString.Replace("&", ";");
            }

            // Add pooling parameters if needed
            connStr += poolingParams;

            return connStr;
        }

        /// <inheritdoc/>
        public void ValidateConnectionString(string providerName, string connectionStringValue)
        {
            if (string.IsNullOrEmpty(providerName))
            {
                throw new ArgumentNullException(nameof(providerName), "Provider name cannot be null or empty");
            }

            if (string.IsNullOrEmpty(connectionStringValue))
            {
                throw new ArgumentNullException(nameof(connectionStringValue), "Connection string cannot be null or empty");
            }

            if (!providerName.Equals(DatabaseConstants.POSTGRES_PROVIDER, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    $"Only PostgreSQL is supported. Invalid provider: {providerName}",
                    nameof(providerName));
            }

            ValidatePostgresConnectionString(connectionStringValue);
        }

        /// <inheritdoc/>
        public string SanitizeConnectionString(string connectionStringValue)
        {
            if (string.IsNullOrEmpty(connectionStringValue))
            {
                return connectionStringValue;
            }

            // Mask password in connection string (case-insensitive)
            return Regex.Replace(
                connectionStringValue,
                @"(Password|pwd)=([^;]+)",
                "$1=*****",
                RegexOptions.IgnoreCase);
        }

        private void ValidatePostgresConnectionString(string connectionString)
        {
            // Basic validation for required fields in Postgres connection string
            if (!connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase) ||
                !connectionString.Contains("Database=", StringComparison.OrdinalIgnoreCase) ||
                !connectionString.Contains("Username=", StringComparison.OrdinalIgnoreCase) ||
                !connectionString.Contains("Password=", StringComparison.OrdinalIgnoreCase))
            {
                // lgtm [cs/cleartext-storage-of-sensitive-information]
                _logger?.LogError("Postgres connection string missing required fields: {SanitizedConnStr}",
                    SanitizeConnectionString(connectionString));
                throw new InvalidOperationException(
                    string.Format(DatabaseConstants.ERR_MISSING_REQUIRED_FIELDS, "PostgreSQL"));
            }
        }

    }
}
