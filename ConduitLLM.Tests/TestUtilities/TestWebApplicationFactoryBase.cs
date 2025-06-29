using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace ConduitLLM.Tests.TestUtilities
{
    /// <summary>
    /// Base class for test factories that provides flexible database configuration.
    /// Supports both in-memory and PostgreSQL databases for testing.
    /// </summary>
    public abstract class TestWebApplicationFactoryBase<TProgram> : WebApplicationFactory<TProgram>
        where TProgram : class
    {
        protected Dictionary<string, string?> AdditionalConfiguration { get; set; }
        private readonly string _testDbName = $"conduit_test_{Guid.NewGuid():N}";
        private bool _useInMemoryDatabase = true;
        private static readonly object _seedLock = new object();
        private static readonly Dictionary<string, bool> _databaseSeeded = new();

        static TestWebApplicationFactoryBase()
        {
            // Set environment variables as early as possible
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Test");
            Environment.SetEnvironmentVariable("DOTNET_hostBuilder:reloadConfigOnChange", "false");
            // Skip the main application's database initialization
            Environment.SetEnvironmentVariable("CONDUIT_SKIP_DATABASE_INIT", "true");
        }

        protected TestWebApplicationFactoryBase(bool useInMemoryDatabase = true)
        {
            _useInMemoryDatabase = useInMemoryDatabase;
            
            if (_useInMemoryDatabase)
            {
                // Use in-memory database for fast unit tests
                AdditionalConfiguration = new Dictionary<string, string?>
                {
                    { "ConnectionStrings:Default", "" },
                    { "ConduitLLM:Database:Provider", "InMemory" }
                };
            }
            else
            {
                // Use PostgreSQL for integration tests that need real database features
                var testDbUrl = Environment.GetEnvironmentVariable("TEST_DATABASE_URL") 
                    ?? Environment.GetEnvironmentVariable("DATABASE_URL")
                    ?? $"postgresql://conduit:conduitpass@localhost:5432/{_testDbName}";
                
                AdditionalConfiguration = new Dictionary<string, string?>
                {
                    { "DATABASE_URL", testDbUrl },
                    { "ConnectionStrings:Default", ConvertPostgresUrlToConnectionString(testDbUrl) }
                };
            }
        }

        /// <summary>
        /// Converts PostgreSQL URL format to standard connection string format
        /// </summary>
        private string ConvertPostgresUrlToConnectionString(string postgresUrl)
        {
            try
            {
                var uri = new Uri(postgresUrl);
                var userInfo = uri.UserInfo.Split(':');
                var username = userInfo[0];
                var password = userInfo.Length > 1 ? userInfo[1] : "";
                var host = uri.Host;
                var port = uri.Port > 0 ? uri.Port : 5432;
                var database = uri.AbsolutePath.TrimStart('/');

                return $"Host={host};Port={port};Database={database};Username={username};Password={password}";
            }
            catch
            {
                // If parsing fails, return empty string to trigger in-memory database
                return "";
            }
        }

        /// <summary>
        /// Computes SHA256 hash of a string (same method used by the application)
        /// </summary>
        protected static string ComputeHash(string input)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(input);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        protected override IHost CreateHost(IHostBuilder builder)
        {
            // Ensure environment variables are set before the host is created
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Test");
            Environment.SetEnvironmentVariable("DOTNET_hostBuilder:reloadConfigOnChange", "false");
            
            var host = base.CreateHost(builder);
            
            // Seed test data after host is created and database is initialized
            SeedTestDataAsync(host).GetAwaiter().GetResult();
            
            return host;
        }

        /// <summary>
        /// Seeds the test database with required data for integration tests
        /// </summary>
        protected virtual async Task SeedTestDataAsync(IHost host)
        {
            var dbKey = _useInMemoryDatabase ? _testDbName : "shared_postgres";
            
            // Use a static lock to prevent parallel test initialization race conditions
            bool shouldSeed;
            lock (_seedLock)
            {
                if (!_databaseSeeded.ContainsKey(dbKey))
                {
                    _databaseSeeded[dbKey] = false;
                }
                
                shouldSeed = !_databaseSeeded[dbKey];
                if (shouldSeed)
                {
                    _databaseSeeded[dbKey] = true; // Mark as seeded immediately to prevent other threads
                }
            }
            
            if (!shouldSeed) return;
            
            using var scope = host.Services.CreateScope();
            var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ConfigurationDbContext>>();
            
            await using var context = await dbContextFactory.CreateDbContextAsync();
            
            // Ensure database schema is created
            if (_useInMemoryDatabase)
            {
                await context.Database.EnsureCreatedAsync();
            }
            else
            {
                // For PostgreSQL, might want to run migrations instead
                await context.Database.EnsureCreatedAsync();
            }
            
            // Seed test data
            await SeedTestVirtualKeyAsync(context);
            await SeedTestModelMappingAsync(context);
        }

        protected virtual async Task SeedTestVirtualKeyAsync(ConfigurationDbContext context)
        {
            // Check if test virtual key already exists
            var testKeyHash = ComputeHash("test-api-key");
            var existingKey = await context.VirtualKeys
                .FirstOrDefaultAsync(vk => vk.KeyHash == testKeyHash);
                
            if (existingKey == null)
            {
                try
                {
                    // Create test virtual key for integration tests
                    var testVirtualKey = new VirtualKey
                    {
                        KeyName = "Integration Test Key",
                        KeyHash = testKeyHash, // SHA256 of "test-api-key"
                        IsEnabled = true,
                        AllowedModels = null, // Allow all models
                        MaxBudget = null,     // No budget limit
                        CurrentSpend = 0,
                        ExpiresAt = null,     // No expiration
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    
                    context.VirtualKeys.Add(testVirtualKey);
                    await context.SaveChangesAsync();
                }
                catch (DbUpdateException)
                {
                    // Ignore if another test instance already created the key
                }
            }
        }

        protected virtual async Task SeedTestModelMappingAsync(ConfigurationDbContext context)
        {
            // Add test model mapping for video-01 if it doesn't exist
            var existingMapping = await context.ModelProviderMappings
                .FirstOrDefaultAsync(mm => mm.ModelAlias == "video-01");
                
            if (existingMapping == null)
            {
                // First, create or get a provider credential for minimax
                var existingCredential = await context.ProviderCredentials
                    .FirstOrDefaultAsync(pc => pc.ProviderName == "minimax");
                    
                if (existingCredential == null)
                {
                    try
                    {
                        existingCredential = new ProviderCredential
                        {
                            ProviderName = "minimax",
                            ApiKey = "test-minimax-key",
                            IsEnabled = true,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };
                        context.ProviderCredentials.Add(existingCredential);
                        await context.SaveChangesAsync();
                    }
                    catch (DbUpdateException)
                    {
                        // Reload if another test instance created it
                        existingCredential = await context.ProviderCredentials
                            .FirstOrDefaultAsync(pc => pc.ProviderName == "minimax");
                    }
                }
                
                if (existingCredential != null)
                {
                    try
                    {
                        var testModelMapping = new ConduitLLM.Configuration.Entities.ModelProviderMapping
                        {
                            ModelAlias = "video-01",
                            ProviderModelName = "video-01",
                            ProviderCredentialId = existingCredential.Id,
                            IsEnabled = true,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };
                        
                        context.ModelProviderMappings.Add(testModelMapping);
                        await context.SaveChangesAsync();
                    }
                    catch (DbUpdateException)
                    {
                        // Ignore if mapping already exists
                    }
                }
            }
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Test");
            
            // Disable file watching in tests to avoid inotify limits
            builder.UseSetting("hostBuilder:reloadConfigOnChange", "false");
            
            // Configure app configuration without file watching
            builder.ConfigureAppConfiguration((context, config) =>
            {
                // Clear any file-based configuration sources that might use file watching
                config.Sources.Clear();
                
                // Add only in-memory configuration to avoid file watchers
                config.AddInMemoryCollection(AdditionalConfiguration);
                
                // Add essential configuration without file watching
                config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
                config.AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: false);
                config.AddEnvironmentVariables();
            });
            
            // Override database configuration
            builder.ConfigureServices(services =>
            {
                // Remove the default database configuration
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IDbContextFactory<ConfigurationDbContext>));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }
                
                // Add our test database configuration
                if (_useInMemoryDatabase)
                {
                    services.AddDbContextFactory<ConfigurationDbContext>(options =>
                        options.UseInMemoryDatabase(databaseName: _testDbName));
                }
                else
                {
                    var connectionString = AdditionalConfiguration.GetValueOrDefault("ConnectionStrings:Default");
                    if (!string.IsNullOrEmpty(connectionString))
                    {
                        services.AddDbContextFactory<ConfigurationDbContext>(options =>
                            options.UseNpgsql(connectionString));
                    }
                    else
                    {
                        // Fallback to in-memory if no valid connection string
                        services.AddDbContextFactory<ConfigurationDbContext>(options =>
                            options.UseInMemoryDatabase(databaseName: _testDbName));
                    }
                }
            });
            
            base.ConfigureWebHost(builder);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_useInMemoryDatabase)
            {
                // Clean up test database if needed
                try
                {
                    // TODO: Drop test PostgreSQL database if created
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Default implementation using in-memory database for fast unit tests
    /// </summary>
    public class InMemoryTestWebApplicationFactory<TProgram> : TestWebApplicationFactoryBase<TProgram>
        where TProgram : class
    {
        public InMemoryTestWebApplicationFactory() : base(useInMemoryDatabase: true)
        {
        }
    }

    /// <summary>
    /// PostgreSQL implementation for integration tests requiring real database features
    /// </summary>
    public class PostgreSqlTestWebApplicationFactory<TProgram> : TestWebApplicationFactoryBase<TProgram>
        where TProgram : class
    {
        public PostgreSqlTestWebApplicationFactory() : base(useInMemoryDatabase: false)
        {
        }
    }
}