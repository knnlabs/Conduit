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

using Microsoft.EntityFrameworkCore;

namespace ConduitLLM.Admin.Services;

/// <summary>
/// Service for retrieving system information through the Admin API
/// </summary>
public class AdminSystemInfoService : IAdminSystemInfoService
{
    private readonly IConfigurationDbContext _dbContext;
    private readonly ILogger<AdminSystemInfoService> _logger;
    private readonly DateTime _startTime;

    /// <summary>
    /// Initializes a new instance of the AdminSystemInfoService class
    /// </summary>
    /// <param name="dbContext">The configuration database context</param>
    /// <param name="logger">The logger</param>
    public AdminSystemInfoService(
        IConfigurationDbContext dbContext,
        ILogger<AdminSystemInfoService> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

        var components = new Dictionary<string, ComponentHealth>();

        // Database health
        var dbHealth = await CheckDatabaseHealthAsync();
        components.Add("Database", dbHealth);

        // Overall health is determined by component statuses
        string overallStatus = components.All(c => c.Value.Status == "Healthy")
            ? "Healthy"
            : "Unhealthy";

        return new HealthStatusDto
        {
            Status = overallStatus,
            Components = components
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

                if (info.Provider.Contains("SqlServer", StringComparison.OrdinalIgnoreCase))
                {
                    var version = await _dbContext.GetDatabase().ExecuteSqlRawAsync("SELECT @@VERSION");
                    info.Version = version.ToString();
                }
                else if (info.Provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
                {
                    info.Version = "SQLite";
                    info.Location = ExtractDatabasePathFromConnectionString(connectionString);

                    // Get database file size if it's SQLite
                    if (!string.IsNullOrEmpty(info.Location) && File.Exists(info.Location))
                    {
                        var fileInfo = new FileInfo(info.Location);
                        info.Size = FormatFileSize(fileInfo.Length);
                    }
                }
                else if (info.Provider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
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
                            command.CommandText = $"SELECT pg_database_size('{dbName}')";
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
            Description = "Database connection and migrations",
            Data = new Dictionary<string, string>()
        };

        try
        {
            // Check connection
            bool canConnect = await _dbContext.GetDatabase().CanConnectAsync();
            health.Data["Connection"] = canConnect ? "Success" : "Failed";

            if (canConnect)
            {
                // Check migrations
                bool pendingMigrations = (await _dbContext.GetDatabase().GetPendingMigrationsAsync()).Any();
                health.Data["Pending Migrations"] = pendingMigrations ? "Yes" : "No";

                // Get migration history
                var migrations = await _dbContext.GetDatabase().GetAppliedMigrationsAsync();
                health.Data["Applied Migrations"] = migrations.Count().ToString();

                health.Status = pendingMigrations ? "Degraded" : "Healthy";
            }
            else
            {
                health.Status = "Unhealthy";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking database health");
            health.Status = "Unhealthy";
            health.Data["Error"] = ex.Message;
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
            counts.Providers = await _dbContext.ProviderCredentials.CountAsync();
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

    private string ExtractDatabasePathFromConnectionString(string? connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
            return "Unknown";

        var parts = connectionString.Split(';');
        foreach (var part in parts)
        {
            var trimmedPart = part.Trim();
            if (trimmedPart.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
            {
                return trimmedPart.Split('=')[1].Trim();
            }
        }

        return "Unknown";
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
