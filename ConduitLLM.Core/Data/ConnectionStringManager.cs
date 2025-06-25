using System;
using System.Text.RegularExpressions;

using ConduitLLM.Core.Data.Constants;
using ConduitLLM.Core.Data.Interfaces;

using Microsoft.Extensions.Logging;

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
            if (!string.IsNullOrEmpty(databaseUrl) &&
                (databaseUrl.StartsWith(DatabaseConstants.POSTGRES_URL_PREFIX, StringComparison.OrdinalIgnoreCase) ||
                 databaseUrl.StartsWith(DatabaseConstants.POSTGRESQL_URL_PREFIX, StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    var connStr = ParsePostgresUrl(databaseUrl, serviceType);
                    ValidateConnectionString(DatabaseConstants.POSTGRES_PROVIDER, connStr);
                    logAction($"[DB] Using provider: {DatabaseConstants.POSTGRES_PROVIDER}, connection: {SanitizeConnectionString(connStr)}");
                    return (DatabaseConstants.POSTGRES_PROVIDER, connStr);
                }
                catch (Exception ex)
                {
                    logAction($"[DB] Error parsing PostgreSQL URL: {ex.Message}. Falling back to SQLite.");
                }
            }

            // If DATABASE_URL exists but doesn't match expected prefix, log and continue to SQLite fallback
            if (!string.IsNullOrEmpty(databaseUrl))
            {
                logAction("[DB] Invalid DATABASE_URL prefix. Falling back to SQLite.");
            }

            // Check for custom SQLite path
            var sqlitePath = Environment.GetEnvironmentVariable(DatabaseConstants.SQLITE_PATH_ENV);
            if (!string.IsNullOrEmpty(sqlitePath))
            {
                try
                {
                    var sqliteConnStr = $"Data Source={sqlitePath}";
                    ValidateConnectionString(DatabaseConstants.SQLITE_PROVIDER, sqliteConnStr);
                    logAction($"[DB] Using provider: {DatabaseConstants.SQLITE_PROVIDER}, connection: {SanitizeConnectionString(sqliteConnStr)}");
                    return (DatabaseConstants.SQLITE_PROVIDER, sqliteConnStr);
                }
                catch (Exception ex)
                {
                    logAction($"[DB] Error with custom SQLite path: {ex.Message}. Using default SQLite database.");
                }
            }

            // Default fallback to local SQLite database
            var defaultConnStr = $"Data Source={DatabaseConstants.DEFAULT_SQLITE_DATABASE}";
            logAction($"[DB] Using provider: {DatabaseConstants.SQLITE_PROVIDER}, connection: {SanitizeConnectionString(defaultConnStr)} (default)");
            return (DatabaseConstants.SQLITE_PROVIDER, defaultConnStr);
        }

        /// <inheritdoc/>
        public string ParsePostgresUrl(string postgresUrl)
        {
            return ParsePostgresUrl(postgresUrl, null);
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

            switch (providerName.ToLower())
            {
                case var _ when providerName.Equals(DatabaseConstants.POSTGRES_PROVIDER, StringComparison.OrdinalIgnoreCase):
                    ValidatePostgresConnectionString(connectionStringValue);
                    break;

                case var _ when providerName.Equals(DatabaseConstants.SQLITE_PROVIDER, StringComparison.OrdinalIgnoreCase):
                    ValidateSqliteConnectionString(connectionStringValue);
                    break;

                default:
                    throw new ArgumentException(
                        string.Format(DatabaseConstants.ERR_INVALID_PROVIDER, providerName),
                        nameof(providerName));
            }
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

        private void ValidateSqliteConnectionString(string connectionString)
        {
            // Check for Data Source parameter in SQLite connection string
            if (string.IsNullOrEmpty(connectionString) || !connectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase))
            {
                // lgtm [cs/cleartext-storage-of-sensitive-information]
                _logger?.LogError("SQLite connection string missing Data Source: {SanitizedConnStr}",
                    SanitizeConnectionString(connectionString));
                throw new InvalidOperationException(
                    string.Format(DatabaseConstants.ERR_MISSING_REQUIRED_FIELDS, "SQLite"));
            }

            // Extract the database path
            var match = Regex.Match(connectionString, @"Data Source=([^;]+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var path = match.Groups[1].Value;
                if (string.IsNullOrWhiteSpace(path))
                {
                    throw new InvalidOperationException("SQLite database path is empty or whitespace");
                }
            }
            else
            {
                // If we didn't match the regex, it's an invalid connection string
                throw new InvalidOperationException("SQLite connection string format is invalid");
            }
        }
    }
}
