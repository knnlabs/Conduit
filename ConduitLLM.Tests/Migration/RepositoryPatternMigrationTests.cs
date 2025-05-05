using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ConduitLLM.Configuration.DTOs.VirtualKey;
using ConduitLLM.Configuration.Options;
using ConduitLLM.WebUI.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Xunit.Sdk;

namespace ConduitLLM.Tests.Migration
{
    /// <summary>
    /// Tests for verifying that the repository pattern migration works correctly.
    /// </summary>
    /// <remarks>
    /// These tests are designed to verify that the repository pattern implementation
    /// works correctly and can be safely deployed to production environments.
    /// 
    /// The tests focus on basic CRUD operations with the repository-based services
    /// and verify that the performance is acceptable.
    /// </remarks>
    public class RepositoryPatternMigrationTests
    {
        /// <summary>
        /// Tests that the repository pattern configuration service correctly determines
        /// when the repository pattern should be enabled based on the environment.
        /// </summary>
        /// <param name="enabled">Whether the pattern is enabled.</param>
        /// <param name="environments">The environments in which the pattern is enabled.</param>
        /// <param name="currentEnvironment">The current environment.</param>
        /// <param name="expected">The expected result.</param>
        [Theory]
        [InlineData(true, "", "Production", true)]  // Empty string = all environments
        [InlineData(true, "Staging,Canary", "Staging", true)]
        [InlineData(true, "Staging,Canary", "Production", false)]
        [InlineData(true, "Staging,Canary", "canary", true)]  // Case insensitive
        [InlineData(false, "Staging,Canary", "Staging", false)]  // Master toggle off
        public void IsEnabledForEnvironment_ReturnsCorrectValue(bool enabled, string environments, string currentEnvironment, bool expected)
        {
            // Arrange
            var options = new RepositoryPatternOptions
            {
                Enabled = enabled,
                EnabledEnvironments = environments
            };
            
            // Act
            var result = options.IsEnabledForEnvironment(currentEnvironment);
            
            // Assert
            Assert.Equal(expected, result);
        }
        
        /// <summary>
        /// Tests that the repository pattern configuration service correctly reports 
        /// whether the repository pattern is enabled.
        /// </summary>
        [Fact]
        public void ConfigurationService_IsEnabled_ReturnsCorrectValue()
        {
            // Arrange
            var options = new RepositoryPatternOptions
            {
                Enabled = true,
                EnabledEnvironments = "Staging,Canary"
            };
            
            var mockOptions = new Mock<IOptions<RepositoryPatternOptions>>();
            mockOptions.Setup(o => o.Value).Returns(options);
            
            var mockLogger = new Mock<ILogger<RepositoryPatternConfigurationService>>();
            
            // Act
            var service = new RepositoryPatternConfigurationService(mockOptions.Object, mockLogger.Object, "Canary");
            
            // Assert
            Assert.True(service.IsEnabled);
            
            // Act again with a different environment
            service = new RepositoryPatternConfigurationService(mockOptions.Object, mockLogger.Object, "Production");
            
            // Assert
            Assert.False(service.IsEnabled);
        }
        
        /// <summary>
        /// Tests that the repository pattern configuration service correctly tracks performance metrics.
        /// </summary>
        [Fact]
        public async Task ExecuteWithTracking_TracksPerformanceMetrics()
        {
            // Arrange
            var options = new RepositoryPatternOptions
            {
                Enabled = true,
                TrackPerformanceMetrics = true
            };
            
            var mockOptions = new Mock<IOptions<RepositoryPatternOptions>>();
            mockOptions.Setup(o => o.Value).Returns(options);
            
            var mockLogger = new Mock<ILogger<RepositoryPatternConfigurationService>>();
            
            var service = new RepositoryPatternConfigurationService(mockOptions.Object, mockLogger.Object, "Production");
            
            // Act
            await service.ExecuteWithTracking("TestOperation", async () => 
            {
                await Task.Delay(10);  // Simulate some work
                return 42;
            });
            
            // Assert
            var metrics = service.GetPerformanceMetrics();
            Assert.Contains("TestOperation", metrics.Keys);
            Assert.Equal(1, metrics["TestOperation"].Count);
            Assert.True(metrics["TestOperation"].AverageMs >= 10);
        }
        
        /// <summary>
        /// Tests that the repository pattern configuration service doesn't track metrics when tracking is disabled.
        /// </summary>
        [Fact]
        public async Task ExecuteWithTracking_DoesNotTrackWhenDisabled()
        {
            // Arrange
            var options = new RepositoryPatternOptions
            {
                Enabled = true,
                TrackPerformanceMetrics = false
            };
            
            var mockOptions = new Mock<IOptions<RepositoryPatternOptions>>();
            mockOptions.Setup(o => o.Value).Returns(options);
            
            var mockLogger = new Mock<ILogger<RepositoryPatternConfigurationService>>();
            
            var service = new RepositoryPatternConfigurationService(mockOptions.Object, mockLogger.Object, "Production");
            
            // Act
            await service.ExecuteWithTracking("TestOperation", async () => 
            {
                await Task.Delay(10);  // Simulate some work
                return 42;
            });
            
            // Assert
            var metrics = service.GetPerformanceMetrics();
            Assert.Empty(metrics);
        }
        
        /// <summary>
        /// Integration test for verifying that the virtual key API works correctly with repository pattern.
        /// </summary>
        /// <remarks>
        /// This test requires the API to be running with repository pattern enabled.
        /// It can be run against a test environment to verify the deployment.
        /// </remarks>
        [Fact(Skip = "Integration test that requires a running API with repository pattern enabled")]
        [Trait("Category", "Integration")]
        [Trait("Category", "Repository")]
        public async Task VirtualKeysApi_WithRepositoryPattern_WorksCorrectly()
        {
            // This test is skipped with the Skip attribute on the Fact attribute
            
            // Arrange
            var client = new HttpClient();
            client.BaseAddress = new Uri("http://localhost:5001/");
            
            // Use a test master key - in a real test this would be configured properly
            var masterKey = "test_master_key";
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", masterKey);
            
            // Act - Create a new virtual key
            var createRequest = new CreateVirtualKeyRequestDto
            {
                KeyName = $"TestKey_{Guid.NewGuid()}",
                AllowedModels = "gpt-3.5-turbo",
                MaxBudget = 10,
                BudgetDuration = "monthly"
            };
            
            var createResponse = await client.PostAsJsonAsync("api/virtualkeys", createRequest);
            createResponse.EnsureSuccessStatusCode();
            
            var createResult = await createResponse.Content.ReadFromJsonAsync<CreateVirtualKeyResponseDto>();
            Assert.NotNull(createResult);
            
            // Act - Get the virtual key
            var getResponse = await client.GetAsync($"api/virtualkeys/{createResult.KeyInfo.Id}");
            getResponse.EnsureSuccessStatusCode();
            
            var getResult = await getResponse.Content.ReadFromJsonAsync<VirtualKeyDto>();
            Assert.NotNull(getResult);
            Assert.Equal(createResult.KeyInfo.Id, getResult.Id);
            Assert.Equal(createRequest.KeyName, getResult.KeyName);
            
            // Act - Update the virtual key
            var updateRequest = new UpdateVirtualKeyRequestDto
            {
                KeyName = $"UpdatedKey_{Guid.NewGuid()}",
                AllowedModels = "gpt-4",
                MaxBudget = 20,
                IsEnabled = true
            };
            
            var updateResponse = await client.PutAsJsonAsync($"api/virtualkeys/{getResult.Id}", updateRequest);
            updateResponse.EnsureSuccessStatusCode();
            
            // Act - Get the updated key
            var getUpdatedResponse = await client.GetAsync($"api/virtualkeys/{getResult.Id}");
            getUpdatedResponse.EnsureSuccessStatusCode();
            
            var getUpdatedResult = await getUpdatedResponse.Content.ReadFromJsonAsync<VirtualKeyDto>();
            Assert.NotNull(getUpdatedResult);
            Assert.Equal(updateRequest.KeyName, getUpdatedResult.KeyName);
            Assert.Equal(updateRequest.AllowedModels, getUpdatedResult.AllowedModels);
            Assert.Equal(updateRequest.MaxBudget, getUpdatedResult.MaxBudget);
            Assert.Equal(updateRequest.IsEnabled, getUpdatedResult.IsEnabled);
            
            // Act - Delete the virtual key
            var deleteResponse = await client.DeleteAsync($"api/virtualkeys/{getResult.Id}");
            deleteResponse.EnsureSuccessStatusCode();
            
            // Verify it's gone
            var getDeletedResponse = await client.GetAsync($"api/virtualkeys/{getResult.Id}");
            Assert.Equal(System.Net.HttpStatusCode.NotFound, getDeletedResponse.StatusCode);
        }
    }
}