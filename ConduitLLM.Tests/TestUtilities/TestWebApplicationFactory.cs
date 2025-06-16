using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;

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

        static TestWebApplicationFactory()
        {
            // Set environment variables as early as possible
            Environment.SetEnvironmentVariable("CONDUIT_SKIP_DATABASE_INIT", "true");
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Test");
        }

        public TestWebApplicationFactory()
        {
            AdditionalConfiguration = new Dictionary<string, string?>
            {
                // Default configuration for tests - use in-memory SQLite
                { "ConnectionStrings:DefaultConnection", "Data Source=:memory:" },
                { "ConnectionStrings:ConfigurationDb", "Data Source=:memory:" }
            };
        }

        protected override IHost CreateHost(IHostBuilder builder)
        {
            // Ensure environment variables are set before the host is created
            Environment.SetEnvironmentVariable("CONDUIT_SKIP_DATABASE_INIT", "true");
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Test");
            
            return base.CreateHost(builder);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Test");
            
            // Add any additional configuration
            if (AdditionalConfiguration.Count > 0)
            {
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    config.AddInMemoryCollection(AdditionalConfiguration);
                });
            }
            
            base.ConfigureWebHost(builder);
        }
    }
}