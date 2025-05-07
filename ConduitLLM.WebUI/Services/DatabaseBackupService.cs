using System;
using System.IO;
using System.Threading.Tasks;
using System.Text.Json;
using System.Reflection;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Data;
using ConduitLLM.WebUI.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Collections.Generic;

namespace ConduitLLM.WebUI.Services
{
    /// <summary>
    /// Service implementation for database backup and restore operations
    /// </summary>
    public class DatabaseBackupService : IDatabaseBackupService
    {
        private readonly IDbContextFactory<ConfigurationDbContext> _dbContextFactory;
        private readonly ILogger<DatabaseBackupService> _logger;
        private readonly string _databaseProvider;
        private readonly string? _databasePath;

        public DatabaseBackupService(
            IDbContextFactory<ConfigurationDbContext> dbContextFactory,
            ILogger<DatabaseBackupService> logger)
        {
            _dbContextFactory = dbContextFactory;
            _logger = logger;

            // Determine the database provider similar to DbStatus.razor
            var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
            if (!string.IsNullOrEmpty(databaseUrl) &&
                (databaseUrl.StartsWith("postgres://") || databaseUrl.StartsWith("postgresql://")))
            {
                _databaseProvider = "postgres";
            }
            else
            {
                _databaseProvider = "sqlite";
                
                // Get SQLite database path
                var sqlitePath = Environment.GetEnvironmentVariable("CONDUIT_SQLITE_PATH");
                if (!string.IsNullOrEmpty(sqlitePath))
                {
                    _databasePath = sqlitePath;
                }
                else
                {
                    // Last fallback: default SQLite file
                    _databasePath = "ConduitConfig.db";
                }
            }
        }

        /// <inheritdoc />
        public string GetDatabaseProvider() => _databaseProvider;

        /// <inheritdoc />
        public async Task<byte[]> CreateBackupAsync()
        {
            try
            {
                if (_databaseProvider == "sqlite")
                {
                    // For SQLite, we can directly read the file
                    if (_databasePath != null && File.Exists(_databasePath))
                    {
                        return await File.ReadAllBytesAsync(_databasePath);
                    }
                    throw new FileNotFoundException($"SQLite database file not found at: {_databasePath ?? "unknown"}");
                }
                else if (_databaseProvider == "postgres")
                {
                    // For PostgreSQL, we'll export as JSON
                    return await CreatePostgresBackupAsJsonAsync();
                }
                
                throw new NotSupportedException($"Database provider '{_databaseProvider}' not supported for backup");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating database backup");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<bool> RestoreFromBackupAsync(byte[] backupData)
        {
            try
            {
                if (!await ValidateBackupAsync(backupData))
                {
                    _logger.LogWarning("Invalid backup data provided for restore");
                    return false;
                }

                if (_databaseProvider == "sqlite")
                {
                    // For SQLite, we just overwrite the file
                    // First, let's make a backup of the current file
                    if (File.Exists(_databasePath))
                    {
                        string backupPath = $"{_databasePath}.bak.{DateTime.UtcNow:yyyyMMddHHmmss}";
                        File.Copy(_databasePath, backupPath, true);
                        _logger.LogInformation($"Created backup of existing database at: {backupPath}");
                    }

                    // Write the new database file
                    if (_databasePath != null)
                    {
                        try
                        {
                            // Create directory if it doesn't exist
                            var directory = Path.GetDirectoryName(_databasePath);
                            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                            {
                                Directory.CreateDirectory(directory);
                            }
                            
                            await File.WriteAllBytesAsync(_databasePath, backupData);
                            return true;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Error writing database file to path: {_databasePath}");
                            return false;
                        }
                    }
                    else
                    {
                        _logger.LogError("Cannot restore database: Database path is null");
                        return false;
                    }
                }
                else if (_databaseProvider == "postgres")
                {
                    // For PostgreSQL, we'll import from JSON
                    return await RestorePostgresFromJsonAsync(backupData);
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restoring database from backup");
                throw;
            }
        }

        /// <inheritdoc />
        public Task<bool> ValidateBackupAsync(byte[] backupData)
        {
            if (backupData == null || backupData.Length == 0)
            {
                return Task.FromResult(false);
            }

            try
            {
                if (_databaseProvider == "sqlite")
                {
                    // For SQLite, check header signature
                    // SQLite files start with "SQLite format 3\0"
                    if (backupData.Length < 16)
                    {
                        return Task.FromResult(false);
                    }

                    string header = System.Text.Encoding.ASCII.GetString(backupData, 0, 16);
                    return Task.FromResult(header.StartsWith("SQLite format 3"));
                }
                else if (_databaseProvider == "postgres")
                {
                    // For PostgreSQL, verify it's valid JSON
                    try
                    {
                        string jsonContent = System.Text.Encoding.UTF8.GetString(backupData);
                        using JsonDocument doc = JsonDocument.Parse(jsonContent);
                        return Task.FromResult(doc.RootElement.ValueKind == JsonValueKind.Object);
                    }
                    catch
                    {
                        return Task.FromResult(false);
                    }
                }

                return Task.FromResult(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating backup data");
                return Task.FromResult(false);
            }
        }

        private async Task<byte[]> CreatePostgresBackupAsJsonAsync()
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();
            var backup = new Dictionary<string, object>();

            // Get all entity sets (tables)
            var entityTypes = context.Model.GetEntityTypes().ToList();
            foreach (var entityType in entityTypes)
            {
                var tableName = entityType.GetTableName();
                var clrType = entityType.ClrType;
                
                // Get the DbSet dynamically using reflection
                var entityMethod = typeof(DbContext).GetMethod(nameof(DbContext.Set), 
                    BindingFlags.Public | BindingFlags.Instance, 
                    null, 
                    Type.EmptyTypes, 
                    null);
                    
                if (entityMethod != null && tableName != null && clrType != null)
                {
                    var genericMethod = entityMethod.MakeGenericMethod(clrType);
                    var dbSet = genericMethod.Invoke(context, null);
                    
                    if (dbSet != null)
                    {
                        // Call ToList dynamically using reflection
                        var toListMethod = dbSet.GetType().GetMethod("ToList");
                        if (toListMethod != null)
                        {
                            var entities = toListMethod.Invoke(dbSet, null);
                            
                            // Add to our backup dictionary
                            if (entities != null)
                            {
                                backup[tableName] = entities;
                            }
                        }
                    }
                }
            }

            // Serialize to JSON
            var options = new JsonSerializerOptions 
            { 
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            
            string json = JsonSerializer.Serialize(backup, options);
            return System.Text.Encoding.UTF8.GetBytes(json);
        }

        private async Task<bool> RestorePostgresFromJsonAsync(byte[] backupData)
        {
            try
            {
                using var context = await _dbContextFactory.CreateDbContextAsync();
                string jsonContent = System.Text.Encoding.UTF8.GetString(backupData);
                
                // Parse JSON backup
                using JsonDocument doc = JsonDocument.Parse(jsonContent);
                
                // Begin transaction
                await using var transaction = await context.Database.BeginTransactionAsync();
                
                try
                {
                    // Get all entity types
                    var entityTypes = context.Model.GetEntityTypes().ToList();
                    
                    // Clear existing data first (in reverse dependency order)
                    foreach (var entityType in entityTypes.Reverse<Microsoft.EntityFrameworkCore.Metadata.IEntityType>())
                    {
                        var tableName = entityType.GetTableName();
                        if (tableName != null)
                        {
                            // Use escaped SQL identifiers which prevents SQL injection
                            #pragma warning disable EF1002 // SQL injection risk is mitigated through the EscapeSqlIdentifier method
                            await context.Database.ExecuteSqlRawAsync($"DELETE FROM \"{EscapeSqlIdentifier(tableName)}\"");
                            #pragma warning restore EF1002
                        }
                    }
                    
                    // Remember to reset identity columns if needed
                    foreach (var entityType in entityTypes)
                    {
                        var tableName = entityType.GetTableName();
                        if (tableName != null) 
                        {
                            // Only try to reset sequences for tables that have them
                            if (entityType.FindPrimaryKey()?.Properties.Any(p => p.ValueGenerated == Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.OnAdd) == true)
                            {
                                try
                                {
                                    // Use escaped SQL identifier to prevent SQL injection
                                    #pragma warning disable EF1002 // SQL injection risk is mitigated through the EscapeSqlIdentifier method
                                    await context.Database.ExecuteSqlRawAsync($"ALTER SEQUENCE \"{EscapeSqlIdentifier(tableName)}_id_seq\" RESTART WITH 1");
                                    #pragma warning restore EF1002
                                }
                                catch
                                {
                                    // Sequence might not exist or have a different name, just continue
                                }
                            }
                        }
                    }

                    // Process each table in the backup
                    foreach (JsonProperty tableProperty in doc.RootElement.EnumerateObject())
                    {
                        var tableName = tableProperty.Name;
                        var entityType = entityTypes.FirstOrDefault(e => e.GetTableName() == tableName);
                        
                        if (entityType != null)
                        {
                            var clrType = entityType.ClrType;
                            
                            // Deserialize entities
                            string entitiesJson = tableProperty.Value.GetRawText();
                            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                            var entitiesArray = JsonSerializer.Deserialize(entitiesJson, clrType.MakeArrayType(), options);
                            
                            // Get the DbSet and AddRange methods dynamically
                            var entityMethod = typeof(DbContext).GetMethod(nameof(DbContext.Set),
                                BindingFlags.Public | BindingFlags.Instance,
                                null,
                                Type.EmptyTypes,
                                null);
                                
                            if (entityMethod != null && clrType != null)
                            {
                                var genericMethod = entityMethod.MakeGenericMethod(clrType);
                                var dbSet = genericMethod.Invoke(context, null);
                                
                                if (dbSet != null)
                                {
                                    // Find AddRange method that takes one parameter (could be IEnumerable<T> or params T[])
                                    var addRangeMethods = dbSet.GetType().GetMethods()
                                        .Where(m => m.Name == "AddRange" && m.GetParameters().Length == 1)
                                        .ToList();
                                        
                                    if (addRangeMethods.Count > 0 && entitiesArray != null)
                                    {
                                        var addRangeMethod = addRangeMethods.First();
                                        addRangeMethod.Invoke(dbSet, new[] { entitiesArray });
                                    }
                                }
                            }
                        }
                    }
                    
                    // Save changes
                    await context.SaveChangesAsync();
                    
                    // Commit transaction
                    await transaction.CommitAsync();
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during PostgreSQL restore, rolling back");
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to restore PostgreSQL database from JSON");
                return false;
            }
        }
        
        /// <summary>
        /// Helper method to escape SQL identifiers to prevent SQL injection
        /// </summary>
        /// <param name="identifier">The SQL identifier to escape</param>
        /// <returns>The escaped SQL identifier</returns>
        private string EscapeSqlIdentifier(string? identifier)
        {
            if (string.IsNullOrEmpty(identifier))
                return string.Empty;
                
            // Replace any quotes with double quotes to prevent SQL injection
            return identifier.Replace("\"", "\"\"");
        }
    }
}