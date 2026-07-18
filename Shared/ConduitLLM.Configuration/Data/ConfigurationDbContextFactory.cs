using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ConduitLLM.Configuration.Data
{
    /// <summary>
    /// Factory to create ConfigurationDbContext instances at design time for EF Core tools
    /// </summary>
    public class ConfigurationDbContextFactory : IDesignTimeDbContextFactory<ConduitDbContext>
    {
        /// <summary>
        /// Creates a new ConfigurationDbContext for design-time operations
        /// </summary>
        public ConduitDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<ConduitDbContext>();
            optionsBuilder.UseNpgsql(ResolveNpgsqlConnectionString());

            return new ConduitDbContext(optionsBuilder.Options);
        }

        /// <summary>
        /// Resolves the Npgsql connection string from the DATABASE_URL environment
        /// variable, accepting either postgresql:// URI form or key=value form.
        /// Shared by design-time tooling and the "migrate" CLI verb.
        /// </summary>
        public static string ResolveNpgsqlConnectionString()
        {
            // Read connection string from environment variable
            var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL");

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException(
                    "DATABASE_URL environment variable is not set. PostgreSQL is required for Conduit.\n" +
                    "Set DATABASE_URL to a valid PostgreSQL connection string:\n" +
                    "Example: postgresql://user:password@localhost:5432/conduitdb");
            }

            // Parse the connection string
            if (connectionString.StartsWith("postgresql://") || connectionString.StartsWith("postgres://"))
            {
                // Format: postgresql://username:password@host:port/database
                // Convert to Npgsql format
                var uri = new Uri(connectionString);
                var userInfo = uri.UserInfo.Split(':');
                var host = uri.Host;
                var port = uri.Port > 0 ? uri.Port : 5432;
                var database = uri.AbsolutePath.TrimStart('/');
                var username = userInfo[0];
                var password = userInfo.Length > 1 ? userInfo[1] : string.Empty;

                return $"Host={host};Port={port};Database={database};Username={username};Password={password}";
            }

            if (connectionString.Contains("Host=") || connectionString.Contains("Server="))
            {
                // Already in Npgsql format
                return connectionString;
            }

            throw new InvalidOperationException(
                $"Invalid DATABASE_URL format. Must be a PostgreSQL connection string.\n" +
                $"Examples:\n" +
                $"  postgresql://user:password@localhost:5432/conduitdb\n" +
                $"  Host=localhost;Port=5432;Database=conduitdb;Username=user;Password=password");
        }
    }
}
