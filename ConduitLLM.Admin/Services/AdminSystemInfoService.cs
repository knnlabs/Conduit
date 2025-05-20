using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            Database = await GetDatabaseInfoAsync()
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
                if (info.Provider.Contains("SqlServer", StringComparison.OrdinalIgnoreCase))
                {
                    var version = await _dbContext.GetDatabase().ExecuteSqlRawAsync("SELECT @@VERSION");
                    info.Version = version.ToString();
                }
                else if (info.Provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
                {
                    info.Version = "SQLite";
                }
                else if (info.Provider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
                {
                    var version = await _dbContext.GetDatabase().ExecuteSqlRawAsync("SHOW server_version");
                    info.Version = version.ToString();
                }
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
}