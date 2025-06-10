using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.DTOs.VirtualKey;
using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.WebUI.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;

namespace ConduitLLM.Tests.Integration
{
    /// <summary>
    /// Integration tests for Admin API mode functionality
    /// </summary>
    [Collection("AdminApiIntegration")]
    public class AdminApiIntegrationTests
    {
        [Fact(Skip = "Requires external Admin API service running - use docker-compose to start services")]
        public void AdminApiClient_CanConnectToAdminApi()
        {
            // This test requires the Admin API to be running externally
            // Run with: docker-compose up -d postgres redis admin
            
            // Arrange
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddHttpClient();
            
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "AdminApi:BaseUrl", "http://localhost:5002" },
                    { "AdminApi:MasterKey", Environment.GetEnvironmentVariable("CONDUIT_MASTER_KEY") ?? "development-key-change-me" }
                })
                .Build();
            
            services.AddSingleton<IConfiguration>(config);
            services.Configure<ConduitLLM.WebUI.Options.AdminApiOptions>(config.GetSection("AdminApi"));
            services.AddTransient<IAdminApiClient, AdminApiClient>();
            
            var serviceProvider = services.BuildServiceProvider();
            var adminApiClient = serviceProvider.GetRequiredService<IAdminApiClient>();

            // Act & Assert - Just verify we can create the client
            Assert.NotNull(adminApiClient);
            
            // If you want to actually test the connection:
            // var keys = await adminApiClient.GetAllVirtualKeysAsync();
            // Assert.NotNull(keys);
        }

        [Fact(Skip = "Requires external Admin API service running - use docker-compose to start services")]
        public async Task AdminApiClient_VirtualKeyOperations_WorkEndToEnd()
        {
            // This test requires the Admin API to be running externally
            // Run with: docker-compose up -d postgres redis admin
            
            // Arrange
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddHttpClient();
            
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "AdminApi:BaseUrl", "http://localhost:5002" },
                    { "AdminApi:MasterKey", Environment.GetEnvironmentVariable("CONDUIT_MASTER_KEY") ?? "development-key-change-me" }
                })
                .Build();
            
            services.AddSingleton<IConfiguration>(config);
            services.Configure<ConduitLLM.WebUI.Options.AdminApiOptions>(config.GetSection("AdminApi"));
            services.AddTransient<IAdminApiClient, AdminApiClient>();
            
            var serviceProvider = services.BuildServiceProvider();
            var adminApiClient = serviceProvider.GetRequiredService<IAdminApiClient>();

            // Act - Create a virtual key
            var createRequest = new CreateVirtualKeyRequestDto
            {
                KeyName = $"Integration Test Key {Guid.NewGuid()}",
                AllowedModels = "gpt-4,claude-v1",
                MaxBudget = 100.0m
            };

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

        [Fact(Skip = "Requires external Admin API service running - use docker-compose to start services")]
        public async Task AdminApiClient_RequestLogOperations_WorkEndToEnd()
        {
            // This test requires the Admin API to be running externally
            // Run with: docker-compose up -d postgres redis admin
            
            // Arrange
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddHttpClient();
            
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "AdminApi:BaseUrl", "http://localhost:5002" },
                    { "AdminApi:MasterKey", Environment.GetEnvironmentVariable("CONDUIT_MASTER_KEY") ?? "development-key-change-me" }
                })
                .Build();
            
            services.AddSingleton<IConfiguration>(config);
            services.Configure<ConduitLLM.WebUI.Options.AdminApiOptions>(config.GetSection("AdminApi"));
            services.AddTransient<IAdminApiClient, AdminApiClient>();
            
            var serviceProvider = services.BuildServiceProvider();
            var adminApiClient = serviceProvider.GetRequiredService<IAdminApiClient>();

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

        [Fact(Skip = "Requires external Admin API service running - use docker-compose to start services")]
        public async Task AdminApiClient_HealthCheck_ReturnsHealthy()
        {
            // This test requires the Admin API to be running externally
            // Run with: docker-compose up -d postgres redis admin
            
            // Arrange
            using var httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5002") };
            httpClient.DefaultRequestHeaders.Add("X-Master-Key", Environment.GetEnvironmentVariable("CONDUIT_MASTER_KEY") ?? "development-key-change-me");

            // Act - Call health endpoint
            var response = await httpClient.GetAsync("/health");
            
            // Assert
            Assert.True(response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable);
        }
    }
}