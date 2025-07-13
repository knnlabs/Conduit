using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ConduitLLM.Core.Attributes;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;

namespace ConduitLLM.Tests.Core.Services
{
    public class CacheRegistryTests
    {
        private readonly Mock<ILogger<CacheRegistry>> _loggerMock;
        private readonly CacheRegistry _registry;

        public CacheRegistryTests()
        {
            _loggerMock = new Mock<ILogger<CacheRegistry>>();
            _registry = new CacheRegistry(_loggerMock.Object);
        }

        [Fact]
        public void RegisterRegion_AddsRegionSuccessfully()
        {
            // Arrange
            var config = new CacheRegionConfig
            {
                Region = CacheRegion.VirtualKeys,
                DefaultTTL = TimeSpan.FromMinutes(10),
                Priority = 100
            };

            // Act
            _registry.RegisterRegion(CacheRegion.VirtualKeys, config);

            // Assert
            Assert.True(_registry.IsRegionRegistered(CacheRegion.VirtualKeys));
            var retrievedConfig = _registry.GetRegionConfig(CacheRegion.VirtualKeys);
            Assert.NotNull(retrievedConfig);
            Assert.Equal(TimeSpan.FromMinutes(10), retrievedConfig.DefaultTTL);
        }

        [Fact]
        public void RegisterCustomRegion_AddsCustomRegionSuccessfully()
        {
            // Arrange
            const string regionName = "TestCustomRegion";
            var config = new CacheRegionConfig
            {
                DefaultTTL = TimeSpan.FromHours(1),
                UseDistributedCache = false
            };

            // Act
            _registry.RegisterCustomRegion(regionName, config);

            // Assert
            Assert.True(_registry.IsCustomRegionRegistered(regionName));
            var retrievedConfig = _registry.GetCustomRegionConfig(regionName);
            Assert.NotNull(retrievedConfig);
            Assert.Equal(TimeSpan.FromHours(1), retrievedConfig.DefaultTTL);
        }

        [Fact]
        public void GetAllRegions_ReturnsAllRegisteredRegions()
        {
            // Act
            var regions = _registry.GetAllRegions();

            // Assert
            Assert.NotEmpty(regions);
            // Should have default regions registered
            Assert.Contains(CacheRegion.VirtualKeys, regions.Keys);
            Assert.Contains(CacheRegion.RateLimits, regions.Keys);
            Assert.Contains(CacheRegion.ModelMetadata, regions.Keys);
        }

        [Fact]
        public void UpdateRegionConfig_UpdatesExistingRegion()
        {
            // Arrange
            var initialConfig = new CacheRegionConfig
            {
                Region = CacheRegion.AuthTokens,
                DefaultTTL = TimeSpan.FromMinutes(30)
            };
            _registry.RegisterRegion(CacheRegion.AuthTokens, initialConfig);

            var updatedConfig = new CacheRegionConfig
            {
                Region = CacheRegion.AuthTokens,
                DefaultTTL = TimeSpan.FromMinutes(60)
            };

            // Act
            var result = _registry.UpdateRegionConfig(CacheRegion.AuthTokens, updatedConfig);

            // Assert
            Assert.True(result);
            var retrievedConfig = _registry.GetRegionConfig(CacheRegion.AuthTokens);
            Assert.Equal(TimeSpan.FromMinutes(60), retrievedConfig?.DefaultTTL);
        }

        [Fact]
        public void UnregisterRegion_RemovesRegion()
        {
            // Arrange
            var config = new CacheRegionConfig { Region = CacheRegion.AsyncTasks };
            _registry.RegisterRegion(CacheRegion.AsyncTasks, config);

            // Act
            var result = _registry.UnregisterRegion(CacheRegion.AsyncTasks);

            // Assert
            Assert.True(result);
            Assert.False(_registry.IsRegionRegistered(CacheRegion.AsyncTasks));
        }

        [Fact]
        public async Task GetRegionMetadataAsync_ReturnsMetadata()
        {
            // Arrange
            var config = new CacheRegionConfig { Region = CacheRegion.ProviderHealth };
            _registry.RegisterRegion(CacheRegion.ProviderHealth, config);

            // Act
            var metadata = await _registry.GetRegionMetadataAsync(CacheRegion.ProviderHealth);

            // Assert
            Assert.NotNull(metadata);
            Assert.Equal(CacheRegion.ProviderHealth, metadata.Region);
            Assert.True(metadata.IsActive);
            Assert.NotEqual(default(DateTime), metadata.RegisteredAt);
        }

        [Fact]
        public void RegionRegistered_EventIsRaised()
        {
            // Arrange
            CacheRegionEventArgs? eventArgs = null;
            _registry.RegionRegistered += (sender, args) => eventArgs = args;

            var config = new CacheRegionConfig { Region = CacheRegion.Embeddings };

            // Act
            _registry.RegisterRegion(CacheRegion.Embeddings, config);

            // Assert
            Assert.NotNull(eventArgs);
            Assert.Equal(CacheRegion.Embeddings, eventArgs.Region);
            Assert.False(eventArgs.IsCustomRegion);
        }

        [Fact]
        public async Task DiscoverRegionsAsync_DiscoversAttributedClasses()
        {
            // Arrange
            var testAssembly = Assembly.GetExecutingAssembly();

            // Act
            var count = await _registry.DiscoverRegionsAsync(testAssembly);

            // Assert
            Assert.True(count > 0);
            // TestCacheService should be discovered
            var metadata = await _registry.GetRegionMetadataAsync(CacheRegion.ModelMetadata);
            Assert.NotNull(metadata);
            Assert.Contains("TestCacheService", metadata.ConsumerServices);
        }

        [Fact]
        public async Task DiscoverRegionsAsync_DiscoversCustomRegions()
        {
            // Arrange
            var testAssembly = Assembly.GetExecutingAssembly();

            // Act
            await _registry.DiscoverRegionsAsync(testAssembly);

            // Assert
            Assert.True(_registry.IsCustomRegionRegistered("TestCustomCache"));
            var config = _registry.GetCustomRegionConfig("TestCustomCache");
            Assert.NotNull(config);
            Assert.Equal(TimeSpan.FromSeconds(1800), config.DefaultTTL);
        }

        [Fact]
        public async Task DiscoverRegionsAsync_DiscoversMethodAttributes()
        {
            // Arrange
            var testAssembly = Assembly.GetExecutingAssembly();

            // Act
            await _registry.DiscoverRegionsAsync(testAssembly);

            // Assert
            var metadata = await _registry.GetRegionMetadataAsync(CacheRegion.RateLimits);
            Assert.NotNull(metadata);
            Assert.Contains("TestCacheService.GetRateLimitedData", metadata.ConsumerServices);
        }

        [Fact]
        public async Task DiscoverRegionsAsync_DiscoversDependencies()
        {
            // Arrange
            var testAssembly = Assembly.GetExecutingAssembly();

            // Act
            await _registry.DiscoverRegionsAsync(testAssembly);

            // Assert
            var metadata = await _registry.GetRegionMetadataAsync(CacheRegion.VirtualKeys);
            Assert.NotNull(metadata);
            Assert.Contains(CacheRegion.AuthTokens, metadata.Dependencies);
        }
    }

    // Test classes for discovery
    [CacheRegion(CacheRegion.ModelMetadata, Description = "Test cache usage")]
    [CustomCacheRegion("TestCustomCache", DefaultTtlSeconds = 1800)]
    public class TestCacheService
    {
        [CacheRegion(CacheRegion.RateLimits, SuggestedTtlSeconds = 300)]
        public string GetRateLimitedData()
        {
            return "data";
        }

        [CacheRegion(CacheRegion.VirtualKeys)]
        [CacheDependency(CacheRegion.AuthTokens)]
        public void ProcessWithDependency()
        {
        }
    }

    [CacheConfigurationProvider(ConfigurationPropertyName = nameof(CacheConfigurations))]
    public static class TestConfigProvider
    {
        public static CacheRegionConfig[] CacheConfigurations => new[]
        {
            new CacheRegionConfig
            {
                Region = CacheRegion.ProviderResponses,
                DefaultTTL = TimeSpan.FromHours(2),
                Priority = 75
            }
        };
    }
}