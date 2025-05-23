using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace ConduitLLM.Configuration.Data
{
    /// <summary>
    /// Service for managing database initialization and migrations across different database providers
    /// </summary>
    public class DatabaseInitializer
    {
        private readonly IDbContextFactory<ConfigurationDbContext> _dbContextFactory;
        private readonly ILogger<DatabaseInitializer> _logger;
        private readonly string _dbProvider;
        
        // Dictionary of table definitions for direct creation when migrations fail
        private readonly Dictionary<string, Dictionary<string, string>> _tableDefinitions;

        /// <summary>
        /// Initializes a new instance of the <see cref="DatabaseInitializer"/> class
        /// </summary>
        /// <param name="dbContextFactory">The DbContext factory</param>
        /// <param name="logger">The logger</param>
        public DatabaseInitializer(
            IDbContextFactory<ConfigurationDbContext> dbContextFactory,
            ILogger<DatabaseInitializer> logger)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Determine database provider by creating a context and checking its database provider name
            using var context = _dbContextFactory.CreateDbContext();
            _dbProvider = context.Database.ProviderName?.ToLowerInvariant() switch
            {
                var p when p?.Contains("sqlite") == true => "sqlite",
                var p when p?.Contains("npgsql") == true => "postgres",
                _ => throw new InvalidOperationException($"Unsupported database provider: {context.Database.ProviderName}")
            };
            
            _logger.LogInformation("Database provider detected: {Provider}", _dbProvider);
            
            // Initialize table definitions for each supported provider
            _tableDefinitions = InitializeTableDefinitions();
        }

        /// <summary>
        /// Initializes the database by applying migrations and ensuring all required tables exist
        /// </summary>
        /// <param name="maxRetries">Maximum number of retries for database connection</param>
        /// <param name="retryDelayMs">Delay between retries in milliseconds</param>
        /// <returns>True if the database was initialized successfully, false otherwise</returns>
        public async Task<bool> InitializeDatabaseAsync(int maxRetries = 5, int retryDelayMs = 1000)
        {
            _logger.LogInformation("Starting database initialization with {Provider} provider", _dbProvider);
            
            for (int retry = 0; retry < maxRetries; retry++)
            {
                try
                {
                    using var context = _dbContextFactory.CreateDbContext();
                    
                    // Verify database connection
                    if (!await context.Database.CanConnectAsync())
                    {
                        _logger.LogWarning("Cannot connect to database. Attempt {Retry}/{MaxRetries}", 
                            retry + 1, maxRetries);
                        await Task.Delay(retryDelayMs);
                        continue;
                    }
                    
                    // Try both EnsureCreated and Migrate approaches
                    await ApplyMigrationsWithFallbackAsync(context);
                    
                    // Verify critical tables exist
                    var tablesToVerify = new[] 
                    { 
                        "GlobalSettings", 
                        "VirtualKeys", 
                        "ModelCosts", 
                        "RequestLogs",
                        "VirtualKeySpendHistory",
                        "ProviderHealthRecords",
                        "IpFilters"
                    };
                    
                    if (!await AreTablesCreatedAsync(context, tablesToVerify))
                    {
                        _logger.LogWarning("Not all required tables were created by migrations. Attempting direct table creation.");
                        await EnsureTablesExistAsync(tablesToVerify);
                    }
                    
                    _logger.LogInformation("Database initialization completed successfully");
                    return true;
                }
                catch (Exception ex) when (retry < maxRetries - 1)
                {
                    _logger.LogWarning(ex, "Error during database initialization. Attempt {Retry}/{MaxRetries}", 
                        retry + 1, maxRetries);
                    await Task.Delay(retryDelayMs);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to initialize database after {MaxRetries} attempts", maxRetries);
                    return false;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Ensures that specific tables exist by creating them directly if necessary
        /// </summary>
        /// <param name="tableNames">Array of table names to ensure exist</param>
        /// <returns>True if all tables exist or were created successfully, false otherwise</returns>
        public async Task<bool> EnsureTablesExistAsync(params string[] tableNames)
        {
            if (tableNames == null || tableNames.Length == 0)
            {
                return true;
            }
            
            using var context = _dbContextFactory.CreateDbContext();
            bool allTablesExist = true;
            
            foreach (var tableName in tableNames)
            {
                if (!await TableExistsAsync(context, tableName))
                {
                    _logger.LogInformation("Table {TableName} does not exist. Attempting to create it directly.", tableName);
                    
                    if (!_tableDefinitions.TryGetValue(tableName, out var tableDefinition))
                    {
                        _logger.LogWarning("No table definition found for {TableName}", tableName);
                        allTablesExist = false;
                        continue;
                    }
                    
                    try
                    {
                        await CreateTableDirectlyAsync(context, tableName, tableDefinition);
                        _logger.LogInformation("Successfully created table {TableName}", tableName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to create table {TableName}", tableName);
                        allTablesExist = false;
                    }
                }
            }
            
            return allTablesExist;
        }
        
        /// <summary>
        /// Gets the current database provider type
        /// </summary>
        /// <returns>The database provider type as a string ("sqlite" or "postgres")</returns>
        public string GetDatabaseProviderType() => _dbProvider;
        
        // Private helper methods
        
        private async Task ApplyMigrationsWithFallbackAsync(ConfigurationDbContext context)
        {
            try
            {
                // Try migrations first, which is the preferred approach
                var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
                if (pendingMigrations.Any())
                {
                    _logger.LogInformation("Applying {Count} pending migrations: {Migrations}", 
                        pendingMigrations.Count(), string.Join(", ", pendingMigrations));
                    
                    await context.Database.MigrateAsync();
                    _logger.LogInformation("Database migrations applied successfully");
                }
                else
                {
                    _logger.LogInformation("No pending migrations found");
                }
                
                // Also call EnsureCreated as a backup to catch any missing tables
                // Note: This shouldn't have any effect if migrations ran successfully
                _logger.LogInformation("Ensuring database schema is created...");
                await context.Database.EnsureCreatedAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error applying migrations, falling back to EnsureCreated");
                await context.Database.EnsureCreatedAsync();
            }
        }
        
        private async Task<bool> AreTablesCreatedAsync(ConfigurationDbContext context, string[] tableNames)
        {
            foreach (var tableName in tableNames)
            {
                if (!await TableExistsAsync(context, tableName))
                {
                    _logger.LogWarning("Table {TableName} does not exist after migrations", tableName);
                    return false;
                }
            }
            
            return true;
        }
        
        private async Task<bool> TableExistsAsync(ConfigurationDbContext context, string tableName)
        {
            try
            {
                var connection = context.Database.GetDbConnection();
                if (connection.State != ConnectionState.Open)
                {
                    await connection.OpenAsync();
                }
                
                using var command = connection.CreateCommand();
                
                if (_dbProvider == "postgres")
                {
                    command.CommandText = $@"
                        SELECT EXISTS (
                            SELECT FROM information_schema.tables 
                            WHERE table_schema = 'public' 
                            AND table_name = '{tableName}'
                        );";
                    
                    var result = await command.ExecuteScalarAsync();
                    return result != null && (result is bool boolResult ? boolResult : Convert.ToBoolean(result));
                }
                else // sqlite
                {
                    command.CommandText = $@"
                        SELECT COUNT(*) 
                        FROM sqlite_master 
                        WHERE type='table' AND name='{tableName}';";
                    
                    var result = await command.ExecuteScalarAsync();
                    return result != null && Convert.ToInt32(result) > 0;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if table {TableName} exists", tableName);
                return false;
            }
        }
        
        private async Task CreateTableDirectlyAsync(ConfigurationDbContext context, string tableName, Dictionary<string, string> columnDefinitions)
        {
            var connection = context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync();
            }
            
            using var command = connection.CreateCommand();
            
            // Build the CREATE TABLE statement based on the provider
            string createTableSql;
            
            if (_dbProvider == "postgres")
            {
                var columnDefinitionsSql = string.Join(", ", 
                    columnDefinitions.Select(kv => $"\"{kv.Key}\" {kv.Value}"));
                
                createTableSql = $@"
                    CREATE TABLE IF NOT EXISTS ""{tableName}"" (
                        {columnDefinitionsSql}
                    );";
            }
            else // sqlite
            {
                var columnDefinitionsSql = string.Join(", ", 
                    columnDefinitions.Select(kv => $"\"{kv.Key}\" {kv.Value}"));
                
                createTableSql = $@"
                    CREATE TABLE IF NOT EXISTS ""{tableName}"" (
                        {columnDefinitionsSql}
                    );";
            }
            
            command.CommandText = createTableSql;
            await command.ExecuteNonQueryAsync();
            
            // Create indexes for the table based on common patterns
            await CreateCommonIndexesAsync(context, tableName);
        }
        
        private async Task CreateCommonIndexesAsync(ConfigurationDbContext context, string tableName)
        {
            // We'll create some common indexes based on the table name
            var indexesToCreate = new List<(string IndexName, string[] Columns, bool IsUnique)>();
            
            // Common index patterns based on table name
            switch (tableName)
            {
                case "GlobalSettings":
                    indexesToCreate.Add(("IX_GlobalSettings_Key", new[] { "Key" }, true));
                    break;
                    
                case "VirtualKeys":
                    indexesToCreate.Add(("IX_VirtualKeys_KeyHash", new[] { "KeyHash" }, true));
                    break;
                    
                case "ModelCosts":
                    indexesToCreate.Add(("IX_ModelCosts_ModelIdPattern", new[] { "ModelIdPattern" }, false));
                    break;
                    
                case "RequestLogs":
                    indexesToCreate.Add(("IX_RequestLogs_VirtualKeyId", new[] { "VirtualKeyId" }, false));
                    indexesToCreate.Add(("IX_RequestLogs_Timestamp", new[] { "Timestamp" }, false));
                    break;
                    
                case "VirtualKeySpendHistory":
                    indexesToCreate.Add(("IX_VirtualKeySpendHistory_VirtualKeyId", new[] { "VirtualKeyId" }, false));
                    indexesToCreate.Add(("IX_VirtualKeySpendHistory_Timestamp", new[] { "Timestamp" }, false));
                    break;
                    
                case "ProviderHealthRecords":
                    indexesToCreate.Add(("IX_ProviderHealthRecords_ProviderName_TimestampUtc", new[] { "ProviderName", "TimestampUtc" }, false));
                    indexesToCreate.Add(("IX_ProviderHealthRecords_IsOnline", new[] { "IsOnline" }, false));
                    break;
                    
                case "IpFilters":
                    indexesToCreate.Add(("IX_IpFilters_FilterType_IpAddressOrCidr", new[] { "FilterType", "IpAddressOrCidr" }, false));
                    indexesToCreate.Add(("IX_IpFilters_IsEnabled", new[] { "IsEnabled" }, false));
                    break;
            }
            
            var connection = context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync();
            }
            
            foreach (var index in indexesToCreate)
            {
                try
                {
                    using var command = connection.CreateCommand();
                    string columnsList = string.Join(", ", index.Columns.Select(c => $"\"{c}\""));
                    string uniqueClause = index.IsUnique ? "UNIQUE" : "";
                    
                    if (_dbProvider == "postgres")
                    {
                        command.CommandText = $@"
                            CREATE {uniqueClause} INDEX IF NOT EXISTS ""{index.IndexName}"" 
                            ON ""{tableName}"" ({columnsList});";
                    }
                    else // sqlite
                    {
                        command.CommandText = $@"
                            CREATE {uniqueClause} INDEX IF NOT EXISTS ""{index.IndexName}"" 
                            ON ""{tableName}"" ({columnsList});";
                    }
                    
                    await command.ExecuteNonQueryAsync();
                    _logger.LogInformation("Created index {IndexName} on table {TableName}", index.IndexName, tableName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create index {IndexName} on table {TableName}", index.IndexName, tableName);
                }
            }
        }
        
        private Dictionary<string, Dictionary<string, string>> InitializeTableDefinitions()
        {
            var definitions = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            
            // Define IpFilters table for both providers
            if (_dbProvider == "postgres")
            {
                // PostgreSQL definitions
                definitions["IpFilters"] = new Dictionary<string, string>
                {
                    { "Id", "SERIAL PRIMARY KEY" },
                    { "FilterType", "VARCHAR(10) NOT NULL" },
                    { "IpAddressOrCidr", "VARCHAR(50) NOT NULL" },
                    { "Description", "VARCHAR(500) NULL" },
                    { "IsEnabled", "BOOLEAN NOT NULL DEFAULT TRUE" },
                    { "CreatedAt", "TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP" },
                    { "UpdatedAt", "TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP" },
                    { "RowVersion", "BYTEA NULL" }
                };
                
                definitions["GlobalSettings"] = new Dictionary<string, string>
                {
                    { "Id", "SERIAL PRIMARY KEY" },
                    { "Key", "VARCHAR(100) NOT NULL" },
                    { "Value", "VARCHAR(2000) NOT NULL" },
                    { "Description", "VARCHAR(500) NULL" },
                    { "CreatedAt", "TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP" },
                    { "UpdatedAt", "TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP" }
                };
                
                definitions["ProviderHealthRecords"] = new Dictionary<string, string>
                {
                    { "Id", "SERIAL PRIMARY KEY" },
                    { "ProviderName", "VARCHAR(50) NOT NULL" },
                    { "IsOnline", "BOOLEAN NOT NULL" },
                    { "Status", "INTEGER NOT NULL DEFAULT 0" },
                    { "StatusMessage", "VARCHAR(500) NULL" },
                    { "ErrorCategory", "VARCHAR(50) NULL" },
                    { "ErrorDetails", "VARCHAR(2000) NULL" },
                    { "ResponseTimeMs", "INTEGER NULL" },
                    { "EndpointUrl", "VARCHAR(1000) NULL" },
                    { "TimestampUtc", "TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP" }
                };
            }
            else // sqlite
            {
                // SQLite definitions
                definitions["IpFilters"] = new Dictionary<string, string>
                {
                    { "Id", "INTEGER PRIMARY KEY AUTOINCREMENT" },
                    { "FilterType", "TEXT NOT NULL" },
                    { "IpAddressOrCidr", "TEXT NOT NULL" },
                    { "Description", "TEXT NULL" },
                    { "IsEnabled", "INTEGER NOT NULL DEFAULT 1" },
                    { "CreatedAt", "TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP" },
                    { "UpdatedAt", "TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP" },
                    { "RowVersion", "BLOB NULL" }
                };
                
                definitions["GlobalSettings"] = new Dictionary<string, string>
                {
                    { "Id", "INTEGER PRIMARY KEY AUTOINCREMENT" },
                    { "Key", "TEXT NOT NULL" },
                    { "Value", "TEXT NOT NULL" },
                    { "Description", "TEXT NULL" },
                    { "CreatedAt", "TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP" },
                    { "UpdatedAt", "TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP" }
                };
                
                definitions["ProviderHealthRecords"] = new Dictionary<string, string>
                {
                    { "Id", "INTEGER PRIMARY KEY AUTOINCREMENT" },
                    { "ProviderName", "TEXT NOT NULL" },
                    { "IsOnline", "INTEGER NOT NULL" },
                    { "Status", "INTEGER NOT NULL DEFAULT 0" },
                    { "StatusMessage", "TEXT NULL" },
                    { "ErrorCategory", "TEXT NULL" },
                    { "ErrorDetails", "TEXT NULL" },
                    { "ResponseTimeMs", "INTEGER NULL" },
                    { "EndpointUrl", "TEXT NULL" },
                    { "TimestampUtc", "TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP" }
                };
            }
            
            // Add other table definitions as needed
            
            return definitions;
        }
    }
}