using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;

using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Options;
using ConduitLLM.Configuration;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models.Routing;
using ConduitLLM.WebUI.Services;
using ConduitLLM.Tests.TestHelpers;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;

using Moq;
using Xunit;

namespace ConduitLLM.Tests.RepositoryServices
{
    public class RouterServiceTests
    {
        private readonly Mock<ILLMClientFactory> _mockClientFactory;
        private readonly Mock<IOptionsMonitor<RouterOptions>> _mockRouterOptions;
        private readonly ILogger<RouterService> _logger;
        private readonly Mock<IServiceProvider> _mockServiceProvider;
        private readonly RouterOptions _routerOptions;

        public RouterServiceTests()
        {
            _mockClientFactory = new Mock<ILLMClientFactory>();
            _mockRouterOptions = new Mock<IOptionsMonitor<RouterOptions>>();
            _logger = NullLogger<RouterService>.Instance;
            _mockServiceProvider = new Mock<IServiceProvider>();
            
            // Setup router options
            _routerOptions = new RouterOptions
            {
                Enabled = true,
                DefaultRoutingStrategy = "ordered",
                MaxRetries = 3,
                RetryBaseDelayMs = 1000,
                RetryMaxDelayMs = 5000,
                ModelDeployments = new List<ConduitLLM.Configuration.Options.RouterModelDeployment>
                {
                    new ConduitLLM.Configuration.Options.RouterModelDeployment
                    {
                        DeploymentName = "gpt-4",
                        ModelAlias = "gpt-4",
                        RPM = 100,
                        TPM = 100000
                    }
                },
                FallbackRules = new List<string>
                {
                    "gpt-4:claude-2,gpt-3.5-turbo"
                }
            };
            
            _mockRouterOptions.Setup(m => m.CurrentValue).Returns(_routerOptions);
        }

        private RouterService CreateRouterService(IDbContextFactory<ConfigurationDbContext> dbContextFactory)
        {
            return new RouterService(
                dbContextFactory,
                _mockClientFactory.Object,
                _logger,
                _mockRouterOptions.Object,
                _mockServiceProvider.Object);
        }

        [Fact]
        public async Task GetRouterConfigAsync_ShouldDeserializeConfig_WhenExists()
        {
            // Arrange
            var routerConfig = new RouterConfig
            {
                DefaultRoutingStrategy = "ordered",
                MaxRetries = 3,
                ModelDeployments = new List<ModelDeployment>
                {
                    new ModelDeployment
                    {
                        DeploymentName = "gpt-4",
                        ModelAlias = "gpt-4",
                        IsHealthy = true
                    }
                }
            };
            
            var serializedConfig = JsonSerializer.Serialize(routerConfig);
            
            // Create in-memory database context factory
            var factory = DbContextTestHelper.CreateInMemoryDbContextFactory();
            
            // Seed the database with test data
            using (var context = await factory.CreateDbContextAsync())
            {
                await DbContextTestHelper.SeedDatabaseAsync(context, 
                    globalSettings: new List<GlobalSetting> 
                    { 
                        new GlobalSetting { Key = "RouterConfig", Value = serializedConfig } 
                    });
            }
            
            // Create service with real factory
            var service = CreateRouterService(factory);

            // Act
            var result = await service.GetRouterConfigAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal("ordered", result.DefaultRoutingStrategy);
            Assert.Equal(3, result.MaxRetries);
            Assert.Single(result.ModelDeployments);
            Assert.Equal("gpt-4", result.ModelDeployments[0].DeploymentName);
        }

        [Fact]
        public async Task GetRouterConfigAsync_ShouldCreateFromOptions_WhenNoConfigExists()
        {
            // Arrange
            // Create in-memory database context factory with no initial data
            var factory = DbContextTestHelper.CreateInMemoryDbContextFactory();
            
            // Create service with real factory
            var service = CreateRouterService(factory);

            // Act
            var result = await service.GetRouterConfigAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal("ordered", result.DefaultRoutingStrategy);
            Assert.Equal(3, result.MaxRetries);
            Assert.Single(result.ModelDeployments);
            Assert.Equal("gpt-4", result.ModelDeployments[0].DeploymentName);
        }

        [Fact]
        public async Task SaveModelDeploymentAsync_ShouldAddToDeployments()
        {
            // Arrange
            var existingConfig = new RouterConfig
            {
                ModelDeployments = new List<ModelDeployment>
                {
                    new ModelDeployment
                    {
                        DeploymentName = "existing-model",
                        ModelAlias = "existing-model"
                    }
                }
            };
            
            var serializedConfig = JsonSerializer.Serialize(existingConfig);
            
            // Create in-memory database context factory
            var factory = DbContextTestHelper.CreateInMemoryDbContextFactory();
            
            // Seed the database with test data
            using (var context = await factory.CreateDbContextAsync())
            {
                await DbContextTestHelper.SeedDatabaseAsync(context, 
                    globalSettings: new List<GlobalSetting> 
                    { 
                        new GlobalSetting { Key = "RouterConfig", Value = serializedConfig } 
                    });
            }
            
            // Create service with real factory
            var service = CreateRouterService(factory);
            
            var newDeployment = new ModelDeployment
            {
                DeploymentName = "new-model",
                ModelAlias = "new-model-alias",
                IsHealthy = true
            };

            // Act
            var result = await service.SaveModelDeploymentAsync(newDeployment);

            // Assert
            Assert.True(result);
            
            // Verify that the deployment was actually added by retrieving the updated config
            using (var context = await factory.CreateDbContextAsync())
            {
                var savedSetting = await context.GlobalSettings
                    .FirstOrDefaultAsync(g => g.Key == "RouterConfig");
                
                Assert.NotNull(savedSetting);
                
                var updatedConfig = JsonSerializer.Deserialize<RouterConfig>(savedSetting.Value);
                Assert.NotNull(updatedConfig);
                Assert.Equal(2, updatedConfig.ModelDeployments.Count);
                Assert.Contains(updatedConfig.ModelDeployments, d => d.DeploymentName == "new-model");
            }
        }

        [Fact]
        public async Task DeleteModelDeploymentAsync_ShouldRemoveFromDeployments()
        {
            // Arrange
            var existingConfig = new RouterConfig
            {
                ModelDeployments = new List<ModelDeployment>
                {
                    new ModelDeployment
                    {
                        DeploymentName = "model-to-delete",
                        ModelAlias = "model-alias"
                    },
                    new ModelDeployment
                    {
                        DeploymentName = "other-model",
                        ModelAlias = "other-alias"
                    }
                },
                Fallbacks = new Dictionary<string, List<string>>
                {
                    { "primary-model", new List<string> { "model-to-delete", "fallback-model" } }
                }
            };
            
            var serializedConfig = JsonSerializer.Serialize(existingConfig);
            
            // Create in-memory database context factory
            var factory = DbContextTestHelper.CreateInMemoryDbContextFactory();
            
            // Seed the database with test data
            using (var context = await factory.CreateDbContextAsync())
            {
                await DbContextTestHelper.SeedDatabaseAsync(context, 
                    globalSettings: new List<GlobalSetting> 
                    { 
                        new GlobalSetting { Key = "RouterConfig", Value = serializedConfig } 
                    });
            }
            
            // Create service with real factory
            var service = CreateRouterService(factory);

            // Act
            var result = await service.DeleteModelDeploymentAsync("model-to-delete");

            // Assert
            Assert.True(result);
            
            // Verify that the deployment was actually removed by retrieving the updated config
            using (var context = await factory.CreateDbContextAsync())
            {
                var savedSetting = await context.GlobalSettings
                    .FirstOrDefaultAsync(g => g.Key == "RouterConfig");
                
                Assert.NotNull(savedSetting);
                
                var updatedConfig = JsonSerializer.Deserialize<RouterConfig>(savedSetting.Value);
                Assert.NotNull(updatedConfig);
                Assert.Single(updatedConfig.ModelDeployments);
                Assert.DoesNotContain(updatedConfig.ModelDeployments, d => d.DeploymentName == "model-to-delete");
                
                // Verify fallbacks were also updated
                Assert.True(updatedConfig.Fallbacks.ContainsKey("primary-model"));
                Assert.DoesNotContain("model-to-delete", updatedConfig.Fallbacks["primary-model"]);
            }
        }

        [Fact]
        public async Task SetFallbackConfigurationAsync_ShouldUpdateFallbacks()
        {
            // Arrange
            var existingConfig = new RouterConfig
            {
                Fallbacks = new Dictionary<string, List<string>>
                {
                    { "existing-model", new List<string> { "existing-fallback" } }
                }
            };
            
            var serializedConfig = JsonSerializer.Serialize(existingConfig);
            
            // Create in-memory database context factory
            var factory = DbContextTestHelper.CreateInMemoryDbContextFactory();
            
            // Seed the database with test data
            using (var context = await factory.CreateDbContextAsync())
            {
                await DbContextTestHelper.SeedDatabaseAsync(context, 
                    globalSettings: new List<GlobalSetting> 
                    { 
                        new GlobalSetting { Key = "RouterConfig", Value = serializedConfig } 
                    });
            }
            
            // Create service with real factory
            var service = CreateRouterService(factory);
            
            var primaryModel = "new-primary";
            var fallbackModels = new List<string> { "fallback1", "fallback2" };

            // Act
            var result = await service.SetFallbackConfigurationAsync(primaryModel, fallbackModels);

            // Assert
            Assert.True(result);
            
            // Verify that the fallback config was actually updated by retrieving the updated config
            using (var context = await factory.CreateDbContextAsync())
            {
                var savedSetting = await context.GlobalSettings
                    .FirstOrDefaultAsync(g => g.Key == "RouterConfig");
                
                Assert.NotNull(savedSetting);
                
                var updatedConfig = JsonSerializer.Deserialize<RouterConfig>(savedSetting.Value);
                Assert.NotNull(updatedConfig);
                Assert.True(updatedConfig.Fallbacks.ContainsKey(primaryModel));
                Assert.Equal(fallbackModels, updatedConfig.Fallbacks[primaryModel]);
                Assert.True(updatedConfig.Fallbacks.ContainsKey("existing-model")); // Original entry should still exist
            }
        }

        [Fact]
        public async Task RemoveFallbackConfigurationAsync_ShouldRemoveFallbacks()
        {
            // Arrange
            var existingConfig = new RouterConfig
            {
                Fallbacks = new Dictionary<string, List<string>>
                {
                    { "model-to-remove", new List<string> { "fallback1", "fallback2" } },
                    { "other-model", new List<string> { "other-fallback" } }
                }
            };
            
            var serializedConfig = JsonSerializer.Serialize(existingConfig);
            
            // Create in-memory database context factory
            var factory = DbContextTestHelper.CreateInMemoryDbContextFactory();
            
            // Seed the database with test data
            using (var context = await factory.CreateDbContextAsync())
            {
                await DbContextTestHelper.SeedDatabaseAsync(context, 
                    globalSettings: new List<GlobalSetting> 
                    { 
                        new GlobalSetting { Key = "RouterConfig", Value = serializedConfig } 
                    });
            }
            
            // Create service with real factory
            var service = CreateRouterService(factory);

            // Act
            var result = await service.RemoveFallbackConfigurationAsync("model-to-remove");

            // Assert
            Assert.True(result);
            
            // Verify that the fallback config was actually removed by retrieving the updated config
            using (var context = await factory.CreateDbContextAsync())
            {
                var savedSetting = await context.GlobalSettings
                    .FirstOrDefaultAsync(g => g.Key == "RouterConfig");
                
                Assert.NotNull(savedSetting);
                
                var updatedConfig = JsonSerializer.Deserialize<RouterConfig>(savedSetting.Value);
                Assert.NotNull(updatedConfig);
                Assert.False(updatedConfig.Fallbacks.ContainsKey("model-to-remove"));
                Assert.True(updatedConfig.Fallbacks.ContainsKey("other-model")); // Original entry should still exist
            }
        }

        [Fact]
        public async Task GetRouterStatusAsync_ShouldReturnStatus()
        {
            // Arrange
            var routerConfig = new RouterConfig
            {
                DefaultRoutingStrategy = "ordered",
                MaxRetries = 3
            };
            
            var serializedConfig = JsonSerializer.Serialize(routerConfig);
            
            // Create in-memory database context factory
            var factory = DbContextTestHelper.CreateInMemoryDbContextFactory();
            
            // Seed the database with test data
            using (var context = await factory.CreateDbContextAsync())
            {
                await DbContextTestHelper.SeedDatabaseAsync(context, 
                    globalSettings: new List<GlobalSetting> 
                    { 
                        new GlobalSetting { Key = "RouterConfig", Value = serializedConfig } 
                    });
            }
            
            // Create service with real factory
            var service = CreateRouterService(factory);

            // Act
            var result = await service.GetRouterStatusAsync();

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.Config);
            Assert.Equal("ordered", result.Config.DefaultRoutingStrategy);
            Assert.Equal(3, result.Config.MaxRetries);
        }

        [Fact]
        public async Task UpdateRouterConfigAsync_ShouldSaveConfig()
        {
            // Arrange
            var newConfig = new RouterConfig
            {
                DefaultRoutingStrategy = "fallback",
                MaxRetries = 5,
                ModelDeployments = new List<ModelDeployment>
                {
                    new ModelDeployment
                    {
                        DeploymentName = "updated-model",
                        ModelAlias = "updated-alias"
                    }
                }
            };
            
            // Create in-memory database context factory
            var factory = DbContextTestHelper.CreateInMemoryDbContextFactory();
            
            // Create service with real factory
            var service = CreateRouterService(factory);

            // Act
            var result = await service.UpdateRouterConfigAsync(newConfig);

            // Assert
            Assert.True(result);
            
            // Verify that the config was actually saved by retrieving it
            using (var context = await factory.CreateDbContextAsync())
            {
                var savedSetting = await context.GlobalSettings
                    .FirstOrDefaultAsync(g => g.Key == "RouterConfig");
                
                Assert.NotNull(savedSetting);
                
                var updatedConfig = JsonSerializer.Deserialize<RouterConfig>(savedSetting.Value);
                Assert.NotNull(updatedConfig);
                Assert.Equal("fallback", updatedConfig.DefaultRoutingStrategy);
                Assert.Equal(5, updatedConfig.MaxRetries);
                Assert.Single(updatedConfig.ModelDeployments);
                Assert.Equal("updated-model", updatedConfig.ModelDeployments[0].DeploymentName);
            }
        }
    }
}