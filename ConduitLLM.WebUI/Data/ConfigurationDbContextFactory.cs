using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;

namespace ConduitLLM.WebUI.Data;

// Factory for creating DbContext during design-time operations like migrations
public class ConfigurationDbContextFactory : IDesignTimeDbContextFactory<ConfigurationDbContext>
{
    public ConfigurationDbContext CreateDbContext(string[] args)
    {
        // Create configuration from appsettings.json
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        // Get the connection string from configuration or environment variables
        var connectionString = configuration.GetConnectionString("DefaultConnection") ?? 
                              Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");
        
        if (string.IsNullOrEmpty(connectionString))
        {
            // Use a default SQLite connection string if none is specified
            connectionString = "Data Source=config.db";
        }

        var optionsBuilder = new DbContextOptionsBuilder<ConfigurationDbContext>();
        
        // Get database provider from environment variables or use SQLite by default
        var dbProvider = Environment.GetEnvironmentVariable("DB_PROVIDER") ?? "sqlite";
        
        if (dbProvider.Equals("sqlite", StringComparison.OrdinalIgnoreCase)) 
        {
            optionsBuilder.UseSqlite(connectionString);
        }
        else if (dbProvider.Equals("postgres", StringComparison.OrdinalIgnoreCase))
        {
            optionsBuilder.UseNpgsql(connectionString);
        }
        else
        {
            throw new InvalidOperationException($"Unsupported database provider: {dbProvider}");
        }
        
        return new ConfigurationDbContext(optionsBuilder.Options);
    }
}
