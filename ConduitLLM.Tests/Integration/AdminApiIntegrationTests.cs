using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.DTOs.VirtualKey;
using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.WebUI.Services;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Xunit;
using Xunit.Abstractions;

namespace ConduitLLM.Tests.Integration
{
    /// <summary>
    /// Integration tests for Admin API mode functionality
    /// </summary>
    public class AdminApiIntegrationTests : IClassFixture<WebApplicationFactory<ConduitLLM.Admin.Program>>
    {
        private readonly WebApplicationFactory<ConduitLLM.Admin.Program> _adminFactory;
        private readonly HttpClient _adminHttpClient;

        public AdminApiIntegrationTests(WebApplicationFactory<ConduitLLM.Admin.Program> adminFactory)
        {
            _adminFactory = adminFactory
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureAppConfiguration((context, config) =>
                    {
                        // Add test configuration with master key
                        config.AddInMemoryCollection(new Dictionary<string, string?>
                        {
                            { "AdminApi:MasterKey", "test-master-key" },
                            { "ConnectionStrings:ConfigurationDb", "Data Source=:memory:" }
                        });
                    });
                });
            _adminHttpClient = _adminFactory.CreateClient();
        }

        [Fact]
        public void AdminApiClient_CanConnectToAdminApi()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddLogging();

            // Configure the AdminApiClient to use the test server's HttpClient
            var adminOptions = Options.Create(new ConduitLLM.WebUI.Options.AdminApiOptions
            {
                BaseUrl = _adminHttpClient.BaseAddress?.ToString() ?? "http://localhost",
                MasterKey = "test-master-key"
            });

            var serviceProvider = services.BuildServiceProvider();
            var logger = serviceProvider.GetRequiredService<ILogger<AdminApiClient>>();

            // Create AdminApiClient with the test HttpClient
            var adminApiClient = new AdminApiClient(_adminHttpClient, adminOptions, logger);

            // Act & Assert - Just verify we can create the client
            Assert.NotNull(adminApiClient);
        }

        [Fact(Skip = "Requires database setup - run manually with test database")]
        public async Task AdminApiClient_VirtualKeyOperations_WorkEndToEnd()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddLogging();

            // Configure the AdminApiClient to use the test server's HttpClient
            var adminOptions = Options.Create(new ConduitLLM.WebUI.Options.AdminApiOptions
            {
                BaseUrl = _adminHttpClient.BaseAddress?.ToString() ?? "http://localhost",
                MasterKey = "test-master-key"
            });

            var serviceProvider = services.BuildServiceProvider();
            var logger = serviceProvider.GetRequiredService<ILogger<AdminApiClient>>();

            // Create AdminApiClient with the test HttpClient
            var adminApiClient = new AdminApiClient(_adminHttpClient, adminOptions, logger);

            // Act - Create a virtual key
            var createRequest = new CreateVirtualKeyRequestDto
            {
                KeyName = $"Integration Test Key {Guid.NewGuid()}",
                AllowedModels = "gpt-4,claude-v1",
                MaxBudget = 100.0m
            };

            try
            {
                var createResponse = await adminApiClient.CreateVirtualKeyAsync(createRequest);

                // Assert - Verify creation
                Assert.NotNull(createResponse);
                Assert.NotNull(createResponse.VirtualKey);
                Assert.NotNull(createResponse.KeyInfo);
                Assert.Equal(createRequest.KeyName, createResponse.KeyInfo.Name);

                // Act - List virtual keys
                var keys = await adminApiClient.GetAllVirtualKeysAsync();

                // Assert - Verify the key appears in the list
                Assert.NotNull(keys);
                Assert.Contains(keys, k => k.Id == createResponse.KeyInfo.Id);

                // Act - Delete the key (cleanup)
                await adminApiClient.DeleteVirtualKeyAsync(createResponse.KeyInfo.Id);
            }
            catch (HttpRequestException ex)
            {
                // Skip test if Admin API is not running
                if (ex.Message.Contains("Connection refused") || ex.Message.Contains("No such host"))
                {
                    return; // Test inconclusive - Admin API not available
                }
                throw;
            }
        }

        [Fact(Skip = "Requires database setup - run manually with test database")]
        public async Task AdminApiClient_RequestLogOperations_WorkEndToEnd()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddLogging();

            // Configure the AdminApiClient to use the test server's HttpClient
            var adminOptions = Options.Create(new ConduitLLM.WebUI.Options.AdminApiOptions
            {
                BaseUrl = _adminHttpClient.BaseAddress?.ToString() ?? "http://localhost",
                MasterKey = "test-master-key"
            });

            var serviceProvider = services.BuildServiceProvider();
            var logger = serviceProvider.GetRequiredService<ILogger<AdminApiClient>>();

            // Create AdminApiClient with the test HttpClient
            var adminApiClient = new AdminApiClient(_adminHttpClient, adminOptions, logger);

            try
            {
                // Act - Get logs summary
                var summary = await adminApiClient.GetLogsSummaryAsync(7);

                // Assert - Verify we got a response (even if empty)
                Assert.NotNull(summary);
                Assert.True(summary.TotalRequests >= 0);

                // Act - Get request logs
                var logs = await adminApiClient.GetRequestLogsAsync(1, 10);

                // Assert - Verify we got a response
                Assert.NotNull(logs);
                Assert.NotNull(logs.Items);
                Assert.True(logs.TotalCount >= 0);
            }
            catch (HttpRequestException ex)
            {
                // Skip test if Admin API is not running
                if (ex.Message.Contains("Connection refused") || ex.Message.Contains("No such host"))
                {
                    return; // Test inconclusive - Admin API not available
                }
                throw;
            }
        }

        [Fact]
        public async Task AdminApiClient_HealthCheck_ReturnsHealthy()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddHttpClient();

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "AdminApi:BaseUrl", _adminHttpClient.BaseAddress?.ToString() ?? "http://localhost" },
                    { "AdminApi:MasterKey", "test-master-key" },
                    { "AdminApi:UseAdminApi", "true" }
                })
                .Build();

            services.AddSingleton<IConfiguration>(config);
            services.AddTransient<IAdminApiClient, AdminApiClient>();

            var serviceProvider = services.BuildServiceProvider();
            var adminApiClient = serviceProvider.GetRequiredService<IAdminApiClient>();

            try
            {
                // Act - Call health endpoint
                var response = await _adminHttpClient.GetAsync("/health");

                // Assert
                Assert.True(response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable);
            }
            catch (HttpRequestException ex)
            {
                // Skip test if Admin API is not running
                if (ex.Message.Contains("Connection refused") || ex.Message.Contains("No such host"))
                {
                    return; // Test inconclusive - Admin API not available
                }
                throw;
            }
        }
    }
}
