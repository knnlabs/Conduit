using System;
using ConduitLLM.Core.Data;
using ConduitLLM.Core.Data.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace ConduitLLM.Core
{
    /// <summary>
    /// Helper class for database connection string operations.
    /// </summary>
    /// <remarks>
    /// This class is maintained for backward compatibility and delegates to the new
    /// <see cref="IConnectionStringManager"/> implementation.
    /// Consider using the new interface directly for new code.
    /// </remarks>
    [Obsolete("Use IConnectionStringManager instead. This class will be removed in a future version.")]
    public static class DbConnectionHelper
    {
        // Lazy-initialized instance for static methods
        private static readonly Lazy<IConnectionStringManager> _connectionManager = 
            new Lazy<IConnectionStringManager>(() => new ConnectionStringManager());

        /// <summary>
        /// Gets the database provider and connection string based on environment configuration.
        /// </summary>
        /// <param name="logger">Optional logger action for connection details.</param>
        /// <returns>A tuple containing the provider name and connection string.</returns>
        public static (string Provider, string ConnectionString) GetProviderAndConnectionString(Action<string>? logger = null)
        {
            var result = _connectionManager.Value.GetProviderAndConnectionString(logger);
            return (result.ProviderName, result.ConnectionStringValue);
        }

        /// <summary>
        /// Parses a PostgreSQL URL into a connection string.
        /// </summary>
        /// <param name="url">The PostgreSQL URL to parse.</param>
        /// <returns>A formatted connection string.</returns>
        /// <exception cref="InvalidOperationException">Thrown when URL format is invalid.</exception>
        [Obsolete("Use IConnectionStringManager.ParsePostgresUrl instead. This method will be removed in a future version.")]
        public static string ParsePostgresUrl(string url)
        {
            return _connectionManager.Value.ParsePostgresUrl(url);
        }

        /// <summary>
        /// Validates a connection string for the specified provider.
        /// </summary>
        /// <param name="provider">The database provider.</param>
        /// <param name="connectionString">The connection string to validate.</param>
        /// <exception cref="InvalidOperationException">Thrown when validation fails.</exception>
        [Obsolete("Use IConnectionStringManager.ValidateConnectionString instead. This method will be removed in a future version.")]
        public static void ValidateConnectionString(string provider, string connectionString)
        {
            _connectionManager.Value.ValidateConnectionString(provider, connectionString);
        }

        /// <summary>
        /// Sanitizes a connection string by masking sensitive information.
        /// </summary>
        /// <param name="connectionString">The connection string to sanitize.</param>
        /// <returns>A sanitized connection string safe for logging.</returns>
        [Obsolete("Use IConnectionStringManager.SanitizeConnectionString instead. This method will be removed in a future version.")]
        public static string SanitizeConnectionString(string connectionString)
        {
            return _connectionManager.Value.SanitizeConnectionString(connectionString);
        }

        // For backward compatibility, exposing the previous private methods as public

        /// <summary>
        /// Validates a PostgreSQL connection string.
        /// </summary>
        /// <param name="connStr">The connection string to validate.</param>
        /// <exception cref="InvalidOperationException">Thrown when validation fails.</exception>
        [Obsolete("Use IConnectionStringManager.ValidateConnectionString instead. This method will be removed in a future version.")]
        public static void ValidatePostgres(string connStr)
        {
            _connectionManager.Value.ValidateConnectionString("postgres", connStr);
        }

        /// <summary>
        /// Validates a SQLite database path.
        /// </summary>
        /// <param name="path">The SQLite database path to validate.</param>
        /// <exception cref="InvalidOperationException">Thrown when validation fails.</exception>
        [Obsolete("Use IConnectionStringManager.ValidateConnectionString instead. This method will be removed in a future version.")]
        public static void ValidateSqlite(string path)
        {
            _connectionManager.Value.ValidateConnectionString("sqlite", $"Data Source={path}");
        }
    }
}
