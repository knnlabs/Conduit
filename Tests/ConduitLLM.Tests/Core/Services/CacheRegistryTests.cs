using Microsoft.Extensions.Logging;
using Moq;
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
        public async Task RegisterDescriptor_RegistersConfigurationAndMetadata()
        {
            _registry.RegisterDescriptor(new CacheRegionDescriptor(
                CacheRegion.ModelMetadata,
                new CacheRegionConfig { DefaultTTL = TimeSpan.FromMinutes(20) },
                ["TestCacheService", "TestCacheService.GetRateLimitedData"],
                [CacheRegion.AuthTokens]));

            var metadata = await _registry.GetRegionMetadataAsync(CacheRegion.ModelMetadata);
            Assert.NotNull(metadata);
            Assert.Contains("TestCacheService", metadata.ConsumerServices);
            Assert.Contains("TestCacheService.GetRateLimitedData", metadata.ConsumerServices);
            Assert.Contains(CacheRegion.AuthTokens, metadata.Dependencies);
            Assert.Equal(TimeSpan.FromMinutes(20),
                _registry.GetRegionConfig(CacheRegion.ModelMetadata)!.DefaultTTL);
        }
    }
}
