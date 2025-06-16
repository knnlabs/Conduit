using System;
using System.IO;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Configuration.Data
{
    /// <summary>
    /// Factory to create ConfigurationDbContext instances at design time for EF Core tools
    /// </summary>
    public class ConfigurationDbContextFactory : IDesignTimeDbContextFactory<ConfigurationDbContext>
    {
        /// <summary>
        /// Creates a new ConfigurationDbContext for design-time operations
        /// </summary>
        public ConfigurationDbContext CreateDbContext(string[] args)
        {
            // Read connection string from environment variable first
            var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL");

            // If not set, use a default SQLite database for development
            if (string.IsNullOrEmpty(connectionString))
            {
                // Get the directory where the application is running
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;

                // Navigate to a common project directory if necessary
                // Find solution root
                var projectDir = baseDir;
                while (!File.Exists(Path.Combine(projectDir, "Conduit.sln")) && Directory.GetParent(projectDir) != null && projectDir.Length > 3)
                {
                    var parent = Directory.GetParent(projectDir);
                    if (parent == null) break;
                    projectDir = parent.FullName;
                }

                // Use SQLite with file in project directory
                var dbPath = Path.Combine(projectDir, "conduit-dev.db");
                connectionString = $"Data Source={dbPath}";

                Console.WriteLine($"Using development SQLite database at: {dbPath}");

                var optionsBuilder = new DbContextOptionsBuilder<ConfigurationDbContext>();
                optionsBuilder.UseSqlite(connectionString);

                return new ConfigurationDbContext(optionsBuilder.Options);
            }
            else
            {
                Console.WriteLine("Using database connection from environment: DATABASE_URL");

                // Parse the connection string to determine the provider
                if (connectionString.StartsWith("postgresql://"))
                {
                    // Format: postgresql://username:password@host:port/database
                    // Convert to Npgsql format
                    var uri = new Uri(connectionString);
                    var userInfo = uri.UserInfo.Split(':');
                    var host = uri.Host;
                    var port = uri.Port;
                    var database = uri.AbsolutePath.TrimStart('/');
                    var username = userInfo[0];
                    var password = userInfo.Length > 1 ? userInfo[1] : string.Empty;

                    var npgsqlConnectionString = $"Host={host};Port={port};Database={database};Username={username};Password={password}";

                    var optionsBuilder = new DbContextOptionsBuilder<ConfigurationDbContext>();
                    optionsBuilder.UseNpgsql(npgsqlConnectionString);

                    return new ConfigurationDbContext(optionsBuilder.Options);
                }
                else if (connectionString.StartsWith("Data Source=") ||
                         connectionString.Contains(".db") ||
                         connectionString.Contains("sqlite"))
                {
                    // Use SQLite
                    var optionsBuilder = new DbContextOptionsBuilder<ConfigurationDbContext>();
                    optionsBuilder.UseSqlite(connectionString);

                    return new ConfigurationDbContext(optionsBuilder.Options);
                }
                else
                {
                    // Assume PostgreSQL for other formats
                    var optionsBuilder = new DbContextOptionsBuilder<ConfigurationDbContext>();
                    optionsBuilder.UseNpgsql(connectionString);

                    return new ConfigurationDbContext(optionsBuilder.Options);
                }
            }
        }
    }
}
