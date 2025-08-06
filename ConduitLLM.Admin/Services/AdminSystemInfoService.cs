using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

using ConduitLLM.Admin.Extensions;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.Data;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Configuration.DTOs.Monitoring;

using Microsoft.EntityFrameworkCore;

namespace ConduitLLM.Admin.Services;

/// <summary>
/// Service for retrieving system information through the Admin API
/// </summary>
public class AdminSystemInfoService : IAdminSystemInfoService
{
    private readonly IConfigurationDbContext _dbContext;
    private readonly ILogger<AdminSystemInfoService> _logger;
    private readonly IProviderRepository _providerRepository;
    private readonly DateTime _startTime;

    /// <summary>
    /// Initializes a new instance of the AdminSystemInfoService class
    /// </summary>
    /// <param name="dbContext">The configuration database context</param>
    /// <param name="logger">The logger</param>
    /// <param name="providerRepository">The provider repository</param>
    public AdminSystemInfoService(
        IConfigurationDbContext dbContext,
        ILogger<AdminSystemInfoService> logger,
        IProviderRepository providerRepository)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _providerRepository = providerRepository ?? throw new ArgumentNullException(nameof(providerRepository));
        _startTime = Process.GetCurrentProcess().StartTime;
    }

    /// <inheritdoc />
    public async Task<SystemInfoDto> GetSystemInfoAsync()
    {
        _logger.LogInformation("Getting system information");

        var systemInfo = new SystemInfoDto
        {
            Version = GetVersionInfo(),
            OperatingSystem = GetOsInfo(),
            Runtime = GetRuntimeInfo(),
            Database = await GetDatabaseInfoAsync(),
            RecordCounts = await GetRecordCountsAsync()
        };

        return systemInfo;
    }

    /// <inheritdoc />
    public async Task<HealthStatusDto> GetHealthStatusAsync()
    {
        _logger.LogInformation("Getting health status");

        var sw = Stopwatch.StartNew();
        var checks = new Dictionary<string, ComponentHealth>();

        // Database health
        var dbSw = Stopwatch.StartNew();
        var dbHealth = await CheckDatabaseHealthAsync();
        dbHealth.Duration = dbSw.ElapsedMilliseconds;
        checks.Add("database", dbHealth);

        // Provider health check removed - Epic #680

        sw.Stop();

        // Overall health is determined by component statuses
        string overallStatus = checks.All(c => c.Value.Status == "healthy")
            ? "healthy"
            : checks.Any(c => c.Value.Status == "unhealthy")
                ? "unhealthy"
                : "degraded";

        return new HealthStatusDto
        {
            Status = overallStatus,
            Timestamp = DateTime.UtcNow,
            Checks = checks,
            TotalDuration = sw.ElapsedMilliseconds
        };
    }

    private VersionInfo GetVersionInfo()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        var versionString = version?.ToString() ?? "Unknown";

        // Try to get build date from assembly metadata if available
        DateTime? buildDate = null;
        var buildDateAttribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        if (buildDateAttribute != null)
        {
            string? buildInfo = buildDateAttribute.InformationalVersion;
            if (!string.IsNullOrEmpty(buildInfo) && buildInfo.Contains("+"))
            {
                string dateString = buildInfo.Split('+')[1];
                if (DateTime.TryParse(dateString, out var parsedDate))
                {
                    buildDate = parsedDate;
                }
            }
        }

        return new VersionInfo
        {
            AppVersion = versionString,
            BuildDate = buildDate
        };
    }

    private OsInfo GetOsInfo()
    {
        return new OsInfo
        {
            Description = RuntimeInformation.OSDescription,
            Architecture = RuntimeInformation.ProcessArchitecture.ToString()
        };
    }

    private RuntimeInfo GetRuntimeInfo()
    {
        return new RuntimeInfo
        {
            RuntimeVersion = RuntimeInformation.FrameworkDescription,
            StartTime = _startTime,
            Uptime = DateTime.Now - _startTime
        };
    }

    private async Task<DatabaseInfo> GetDatabaseInfoAsync()
    {
        var info = new DatabaseInfo
        {
            Provider = _dbContext.GetDatabase().ProviderName ?? "Unknown",
            Connected = false,
            Version = "Unknown"
        };

        try
        {
            // Check connection
            info.Connected = await _dbContext.GetDatabase().CanConnectAsync();

            // Get database version if possible
            if (info.Connected)
            {
                // Get connection string (masked)
                var connectionString = _dbContext.GetDatabase().GetConnectionString();
                info.ConnectionString = MaskConnectionString(connectionString);

                if (info.Provider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
                {
                    info.Version = "-1"; // We'll get this with raw SQL below
                    info.Location = ExtractHostFromConnectionString(connectionString);

                    // Get PostgreSQL version and database size using raw SQL
                    try
                    {
                        var dbConnection = _dbContext.GetDatabase().GetDbConnection();
                        await dbConnection.OpenAsync();

                        using (var command = dbConnection.CreateCommand())
                        {
                            // Get PostgreSQL version
                            command.CommandText = "SELECT version()";
                            var versionResult = await command.ExecuteScalarAsync();
                            if (versionResult != null)
                            {
                                var versionString = versionResult.ToString();
                                if (!string.IsNullOrEmpty(versionString))
                                {
                                    // Extract just the version number from the full version string
                                    var match = System.Text.RegularExpressions.Regex.Match(versionString, @"PostgreSQL (\d+\.\d+)");
                                    info.Version = match.Success ? match.Groups[1].Value : versionString;
                                }
                            }
                        }

                        using (var command = dbConnection.CreateCommand())
                        {
                            // Get database size
                            var dbName = ExtractDatabaseNameFromConnectionString(connectionString);
                            // Validate database name to prevent SQL injection
                            if (!IsValidDatabaseName(dbName))
                            {
                                info.Size = "Invalid database name";
                            }
                            else
                            {
                                // Use quote_ident to safely escape the database name
                                command.CommandText = "SELECT pg_database_size(quote_ident(@dbName))";
                                var parameter = command.CreateParameter();
                                parameter.ParameterName = "@dbName";
                                parameter.Value = dbName;
                                command.Parameters.Add(parameter);
                                var sizeResult = await command.ExecuteScalarAsync();
                                if (sizeResult != null && long.TryParse(sizeResult.ToString(), out long sizeInBytes))
                                {
                                    info.Size = FormatFileSize(sizeInBytes);
                                }
                                else
                                {
                                    info.Size = "Unknown";
                                }
                            }
                        }

                        await dbConnection.CloseAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not get PostgreSQL database size");
                        info.Size = "N/A";
                    }
                }

                // Get table count
                var tables = await GetTableCountAsync();
                info.TableCount = tables;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting database information");
            info.Connected = false;
        }

        return info;
    }

    private async Task<ComponentHealth> CheckDatabaseHealthAsync()
    {
        var health = new ComponentHealth
        {
            Description = "Database connection and migrations"
        };

        try
        {
            // Check connection
            bool canConnect = await _dbContext.GetDatabase().CanConnectAsync();

            if (canConnect)
            {
                // Check migrations
                bool pendingMigrations = (await _dbContext.GetDatabase().GetPendingMigrationsAsync()).Any();

                // Get migration history
                var migrations = await _dbContext.GetDatabase().GetAppliedMigrationsAsync();

                health.Status = pendingMigrations ? "degraded" : "healthy";
                if (pendingMigrations)
                {
                    health.Description = "Database connected but has pending migrations";
                }
            }
            else
            {
                health.Status = "unhealthy";
                health.Description = "Database connection failed";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking database health");
            health.Status = "unhealthy";
            health.Error = ex.Message;
        }

        return health;
    }


    private async Task<RecordCountsDto> GetRecordCountsAsync()
    {
        var counts = new RecordCountsDto();

        try
        {
            counts.VirtualKeys = await _dbContext.VirtualKeys.CountAsync();
            counts.Requests = await _dbContext.RequestLogs.CountAsync();
            counts.Settings = await _dbContext.GlobalSettings.CountAsync();
            counts.Providers = await _dbContext.Providers.CountAsync();
            counts.ModelMappings = await _dbContext.ModelProviderMappings.CountAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting record counts");
        }

        return counts;
    }

    private async Task<int> GetTableCountAsync()
    {
        try
        {
            var provider = _dbContext.GetDatabase().ProviderName ?? "";
            var dbConnection = _dbContext.GetDatabase().GetDbConnection();

            await dbConnection.OpenAsync();

            using (var command = dbConnection.CreateCommand())
            {
                if (provider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
                {
                    // PostgreSQL query to count tables
                    command.CommandText = @"
                        SELECT COUNT(*) 
                        FROM information_schema.tables 
                        WHERE table_schema = 'public' 
                        AND table_type = 'BASE TABLE'";
                }
                else if (provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
                {
                    // SQLite query to count tables
                    command.CommandText = @"
                        SELECT COUNT(*) 
                        FROM sqlite_master 
                        WHERE type = 'table' 
                        AND name NOT LIKE 'sqlite_%'";
                }
                else
                {
                    // Default to known table count for other providers
                    await dbConnection.CloseAsync();
                    return 13;
                }

                var result = await command.ExecuteScalarAsync();
                await dbConnection.CloseAsync();

                if (result != null && int.TryParse(result.ToString(), out int count))
                {
                    return count;
                }
            }

            // Default to known table count
            return 13;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting table count");
            return 13; // Default known table count
        }
    }

    private string MaskConnectionString(string? connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
            return "Not configured";

        var parts = connectionString.Split(';');
        var maskedParts = new List<string>();

        foreach (var part in parts)
        {
            var trimmedPart = part.Trim();
            if (trimmedPart.StartsWith("Password=", StringComparison.OrdinalIgnoreCase) ||
                trimmedPart.StartsWith("Pwd=", StringComparison.OrdinalIgnoreCase))
            {
                maskedParts.Add(trimmedPart.Split('=')[0] + "=****");
            }
            else
            {
                maskedParts.Add(trimmedPart);
            }
        }

        return string.Join("; ", maskedParts);
    }


    private string ExtractHostFromConnectionString(string? connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
            return "Unknown";

        var parts = connectionString.Split(';');
        foreach (var part in parts)
        {
            var trimmedPart = part.Trim();
            if (trimmedPart.StartsWith("Host=", StringComparison.OrdinalIgnoreCase) ||
                trimmedPart.StartsWith("Server=", StringComparison.OrdinalIgnoreCase))
            {
                return trimmedPart.Split('=')[1].Trim();
            }
        }

        return "Unknown";
    }

    private string ExtractDatabaseNameFromConnectionString(string? connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
            return "";

        var parts = connectionString.Split(';');
        foreach (var part in parts)
        {
            var trimmedPart = part.Trim();
            if (trimmedPart.StartsWith("Database=", StringComparison.OrdinalIgnoreCase))
            {
                return trimmedPart.Split('=')[1].Trim();
            }
        }

        return "";
    }

    private bool IsValidDatabaseName(string dbName)
    {
        if (string.IsNullOrWhiteSpace(dbName))
            return false;

        // Database names in PostgreSQL can contain letters, numbers, underscores, and hyphens
        // They cannot contain quotes, semicolons, or other special characters that could be used for SQL injection
        return System.Text.RegularExpressions.Regex.IsMatch(dbName, @"^[a-zA-Z0-9_\-]+$");
    }

    private string FormatFileSize(long bytes)
    {
        if (bytes < 0)
            return "N/A";

        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;

        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }
}
