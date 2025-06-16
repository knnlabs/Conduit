namespace ConduitLLM.Core.Data.Constants
{
    /// <summary>
    /// Contains constants related to database connections and configuration.
    /// </summary>
    /// <remarks>
    /// This class centralizes all database-related constants to avoid duplication
    /// and ensure consistency across the application.
    /// </remarks>
    public static class DatabaseConstants
    {
        // Environment variables
        public const string DATABASE_URL_ENV = "DATABASE_URL";
        public const string SQLITE_PATH_ENV = "CONDUIT_SQLITE_PATH";
        public const string ENSURE_CREATED_ENV = "CONDUIT_DATABASE_ENSURE_CREATED";

        // Provider names
        public const string POSTGRES_PROVIDER = "postgres";
        public const string SQLITE_PROVIDER = "sqlite";

        // URL prefixes
        public const string POSTGRES_URL_PREFIX = "postgres://";
        public const string POSTGRESQL_URL_PREFIX = "postgresql://";

        // Default values
        public const string DEFAULT_POSTGRES_PORT = "5432";
        public const string DEFAULT_SQLITE_DATABASE = "ConduitConfig.db";

        // Connection timeouts
        public const int DEFAULT_CONNECTION_TIMEOUT_SECONDS = 30;
        public const int RETRY_CONNECTION_TIMEOUT_SECONDS = 5;
        public const int MAX_RETRY_COUNT = 3;

        // Connection pooling settings
        public const int MIN_POOL_SIZE = 1;
        public const int MAX_POOL_SIZE = 100;
        public const int CONNECTION_LIFETIME_SECONDS = 300;
        public const bool POOLING_ENABLED = true;

        // Error messages
        public const string ERR_INVALID_PROVIDER = "Unsupported database provider: {0}";
        public const string ERR_MISSING_REQUIRED_FIELDS = "Missing required fields in {0} connection string";
        public const string ERR_INVALID_POSTGRES_URL = "Invalid PostgreSQL URL format";
    }
}
