using System;
using System.Text.RegularExpressions;

namespace ConduitLLM.Core
{
    public static class DbConnectionHelper
    {
        public static (string Provider, string ConnectionString) GetProviderAndConnectionString(Action<string>? logger = null)
        {
            // Check for DATABASE_URL (Postgres)
            var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
            if (!string.IsNullOrEmpty(databaseUrl) &&
                (databaseUrl.StartsWith("postgres://") || databaseUrl.StartsWith("postgresql://")))
            {
                var connStr = ParsePostgresUrl(databaseUrl);
                ValidatePostgres(connStr);
                logger?.Invoke($"[DB] Using provider: postgres, connection: {SanitizeConnectionString(connStr)}");
                return ("postgres", connStr);
            }

            // Fallback to SQLite
            var sqlitePath = Environment.GetEnvironmentVariable("CONDUIT_SQLITE_PATH");
            if (!string.IsNullOrEmpty(sqlitePath))
            {
                ValidateSqlite(sqlitePath);
                logger?.Invoke($"[DB] Using provider: sqlite, connection: Data Source={SanitizeConnectionString(sqlitePath)}");
                return ("sqlite", $"Data Source={sqlitePath}");
            }

            // Last fallback: in-memory SQLite (dev/test)
            logger?.Invoke("[DB] Using provider: sqlite, connection: Data Source=ConduitConfig.db (default)");
            return ("sqlite", "Data Source=ConduitConfig.db");
        }

        private static string ParsePostgresUrl(string url)
        {
            // Accepts both postgres:// and postgresql://
            var pattern = @"^(postgres(?:ql)?):\/\/(?<user>[^:]+):(?<password>[^@]+)@(?<host>[^:/]+)(?::(?<port>\d+))?\/(?<database>[^?]+)";
            var match = Regex.Match(url, pattern);
            if (!match.Success)
                throw new InvalidOperationException($"Invalid DATABASE_URL format: {url}");

            var user = match.Groups["user"].Value;
            var password = match.Groups["password"].Value;
            var host = match.Groups["host"].Value;
            var port = match.Groups["port"].Success ? match.Groups["port"].Value : "5432";
            var database = match.Groups["database"].Value;

            // Optionally handle query params (e.g., sslmode)
            var uri = new Uri(url);
            var query = uri.Query;
            var queryString = string.IsNullOrEmpty(query) ? string.Empty : query.TrimStart('?');

            var connStr = $"Host={host};Port={port};Database={database};Username={user};Password={password}";
            if (!string.IsNullOrEmpty(queryString))
                connStr += ";" + queryString.Replace("&", ";");
            return connStr;
        }

        private static void ValidatePostgres(string connStr)
        {
            // Basic validation for required fields
            if (!connStr.Contains("Host=") || !connStr.Contains("Database=") ||
                !connStr.Contains("Username=") || !connStr.Contains("Password="))
                throw new InvalidOperationException($"Postgres connection string missing required fields: {SanitizeConnectionString(connStr)}");
        }

        private static void ValidateSqlite(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new InvalidOperationException("SQLite path is empty or whitespace.");
            // Optionally check for invalid chars, etc.
        }

        private static string SanitizeConnectionString(string connStr)
        {
            // Mask password in connection string
            return Regex.Replace(connStr, @"Password=([^;]+)", "Password=*****", RegexOptions.IgnoreCase);
        }
    }
}
