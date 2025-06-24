using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Data.Sqlite;
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
        private static SqliteConnection? _connection;
        private static bool _databaseSeeded = false;

        static TestWebApplicationFactory()
        {
            // Set environment variables as early as possible
            // DO NOT skip database init - we need the tables to be created
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Test");
            Environment.SetEnvironmentVariable("DOTNET_hostBuilder:reloadConfigOnChange", "false");
            
            // Create a persistent SQLite connection to maintain the database schema across tests
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();
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
            AdditionalConfiguration = new Dictionary<string, string?>
            {
                // Use persistent SQLite connection for tests
                { "ConnectionStrings:DefaultConnection", _connection?.ConnectionString ?? "Data Source=:memory:" },
                { "ConnectionStrings:ConfigurationDb", _connection?.ConnectionString ?? "Data Source=:memory:" }
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
            if (_databaseSeeded) return;
            
            using var scope = host.Services.CreateScope();
            var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ConfigurationDbContext>>();
            
            await using var context = await dbContextFactory.CreateDbContextAsync();
            
            // Check if test virtual key already exists
            var testKeyHash = ComputeHash("test-api-key");
            var existingKey = await context.VirtualKeys
                .FirstOrDefaultAsync(vk => vk.KeyHash == testKeyHash);
                
            if (existingKey == null)
            {
                // Create test virtual key for integration tests
                var testVirtualKey = new VirtualKey
                {
                    Id = 1,
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
            
            _databaseSeeded = true;
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
            
            // Override database configuration to use persistent SQLite connection
            // Only do this if the connection is available (defensive programming)
            if (_connection != null)
            {
                builder.ConfigureServices(services =>
                {
                    // Remove the default database configuration
                    var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IDbContextFactory<ConfigurationDbContext>));
                    if (descriptor != null)
                    {
                        services.Remove(descriptor);
                    }
                    
                    // Add our persistent SQLite database configuration
                    services.AddDbContextFactory<ConfigurationDbContext>(options =>
                        options.UseSqlite(_connection));
                });
            }
            
            base.ConfigureWebHost(builder);
        }
        
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Keep connection alive for other tests - only close when explicitly needed
                // _connection?.Close();
            }
            base.Dispose(disposing);
        }
    }
}