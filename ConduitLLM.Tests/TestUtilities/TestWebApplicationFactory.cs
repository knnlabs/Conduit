using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
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

namespace ConduitLLM.Tests.TestUtilities
{
    /// <summary>
    /// Custom WebApplicationFactory that uses in-memory database by default
    /// to avoid PostgreSQL connection issues in test environments.
    /// </summary>
    /// <typeparam name="TProgram">The entry point of the application to test</typeparam>
    public class TestWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram>
        where TProgram : class
    {
        protected Dictionary<string, string?> AdditionalConfiguration { get; set; }
        private readonly string _testDbName = $"conduit_test_{Guid.NewGuid():N}";
        private static bool _databaseSeeded = false;
        private static readonly object _seedLock = new object();

        static TestWebApplicationFactory()
        {
            // Set environment variables as early as possible
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Test");
            Environment.SetEnvironmentVariable("DOTNET_hostBuilder:reloadConfigOnChange", "false");
            // Skip the main application's database initialization to prevent migration lock conflicts
            // TestWebApplicationFactory handles its own database setup in SeedTestDataAsync
            Environment.SetEnvironmentVariable("CONDUIT_SKIP_DATABASE_INIT", "true");
        }
        
        /// <summary>
        /// Computes SHA256 hash of a string (same method used by the application)
        /// </summary>
        private static string ComputeHash(string input)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(input);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        public TestWebApplicationFactory()
        {
            // Set a dummy DATABASE_URL to satisfy the application's requirement
            // The actual database configuration will be overridden to use in-memory
            Environment.SetEnvironmentVariable("DATABASE_URL", "postgresql://test:test@localhost:5432/test");
            
            // Use in-memory database by default to avoid connection string issues
            AdditionalConfiguration = new Dictionary<string, string?>
            {
                { "ConnectionStrings:Default", "" },
                { "ConduitLLM:Database:Provider", "InMemory" }
            };
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
        private async Task SeedTestDataAsync(IHost host)
        {
            // Use a static lock to prevent parallel test initialization race conditions
            bool shouldSeed;
            lock (_seedLock)
            {
                shouldSeed = !_databaseSeeded;
                if (shouldSeed)
                {
                    _databaseSeeded = true; // Mark as seeded immediately to prevent other threads
                }
            }
            
            if (!shouldSeed) return;
            
            using var scope = host.Services.CreateScope();
            var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ConfigurationDbContext>>();
            
            await using var context = await dbContextFactory.CreateDbContextAsync();
            context.IsTestEnvironment = true;
            
            // Ensure database schema is created (since we skipped the main app's database initialization)
            await context.Database.EnsureCreatedAsync();
            
            // Check if test virtual key already exists (use upsert pattern to handle race conditions)
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
                    // This handles race conditions in parallel test execution
                }
            }
            
            // Skip seeding model mappings and provider credentials in test environment
            // These entities are ignored in test configuration to simplify testing
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Test");
            
            // Disable file watching in tests to avoid inotify limits
            builder.UseSetting("hostBuilder:reloadConfigOnChange", "false");
            
            // Configure services BEFORE the application's Startup/Program runs
            // This ensures our test services are registered first
            builder.ConfigureServices(services =>
            {
                // Pre-register the test ConnectionStringManager to avoid PostgreSQL connection attempts
                services.AddSingleton<ConduitLLM.Core.Data.Interfaces.IConnectionStringManager, TestConnectionStringManager>();
            });
            
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
            
            // Override database configuration to use in-memory database
            builder.ConfigureServices(services =>
            {
                // First, replace the ConnectionStringManager to avoid PostgreSQL requirement
                var connectionStringManagerDescriptor = services.FirstOrDefault(d => 
                    d.ServiceType == typeof(ConduitLLM.Core.Data.Interfaces.IConnectionStringManager));
                if (connectionStringManagerDescriptor != null)
                {
                    services.Remove(connectionStringManagerDescriptor);
                }
                services.AddSingleton<ConduitLLM.Core.Data.Interfaces.IConnectionStringManager, TestConnectionStringManager>();
                
                // Remove all EF Core related services to avoid conflicts
                var descriptorsToRemove = services.Where(d => 
                    d.ServiceType == typeof(IDbContextFactory<ConfigurationDbContext>) ||
                    d.ServiceType == typeof(DbContextOptions<ConfigurationDbContext>) ||
                    d.ServiceType == typeof(DbContextOptions) ||
                    d.ServiceType.FullName?.Contains("EntityFrameworkCore") == true).ToList();
                
                foreach (var descriptor in descriptorsToRemove)
                {
                    services.Remove(descriptor);
                }
                
                // Add our test in-memory database configuration
                services.AddDbContextFactory<ConfigurationDbContext>(options =>
                {
                    options.UseInMemoryDatabase(databaseName: _testDbName);
                }, ServiceLifetime.Singleton);
                
                // Override the DbContext service to set IsTestEnvironment
                services.AddScoped<ConfigurationDbContext>(provider =>
                {
                    var factory = provider.GetRequiredService<IDbContextFactory<ConfigurationDbContext>>();
                    var context = factory.CreateDbContext();
                    context.IsTestEnvironment = true;
                    return context;
                });
            });
            
            base.ConfigureWebHost(builder);
        }
        
        protected override void Dispose(bool disposing)
        {
            if (disposing)
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
}