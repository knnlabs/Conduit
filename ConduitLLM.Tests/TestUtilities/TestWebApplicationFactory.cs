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
    /// Custom WebApplicationFactory that ensures test environment variables are set
    /// before the application starts.
    /// </summary>
    /// <typeparam name="TProgram">The entry point of the application to test</typeparam>
    public class TestWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram>
        where TProgram : class
    {
        protected Dictionary<string, string?> AdditionalConfiguration { get; set; }
        private static readonly string _testDbName = $"conduit_test_{Guid.NewGuid():N}";
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
            // Get test PostgreSQL connection from environment or use local instance
            var testDbUrl = Environment.GetEnvironmentVariable("TEST_DATABASE_URL") 
                ?? $"postgresql://conduit:conduitpass@localhost:5432/{_testDbName}";
            
            Environment.SetEnvironmentVariable("DATABASE_URL", testDbUrl);
            
            AdditionalConfiguration = new Dictionary<string, string?>
            {
                // Use PostgreSQL for tests
                { "DATABASE_URL", testDbUrl }
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
            
            // Add test model mapping for video-01 if it doesn't exist
            var existingMapping = await context.ModelProviderMappings
                .FirstOrDefaultAsync(mm => mm.ModelAlias == "video-01");
                
            if (existingMapping == null)
            {
                // First, we need to create or get a provider credential for minimax
                var existingCredential = await context.ProviderCredentials
                    .FirstOrDefaultAsync(pc => pc.ProviderName == "minimax");
                    
                if (existingCredential == null)
                {
                    try
                    {
                        existingCredential = new ConduitLLM.Configuration.Entities.ProviderCredential
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
                            ProviderModelName = "video-01",  // Entity uses ProviderModelName
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
            
            // Override database configuration to use shared SQLite file
            builder.ConfigureServices(services =>
            {
                // Remove the default database configuration
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IDbContextFactory<ConfigurationDbContext>));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }
                
                // Add our test PostgreSQL database configuration
                services.AddDbContextFactory<ConfigurationDbContext>(options =>
                    options.UseNpgsql(AdditionalConfiguration["DATABASE_URL"]));
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