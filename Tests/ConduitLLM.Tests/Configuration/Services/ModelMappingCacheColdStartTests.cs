using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Services;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

using Moq;

namespace ConduitLLM.Tests.Configuration.Services
{
    /// <summary>
    /// Regression tests for issue #959: on a cold start the Gateway's memory cache is empty,
    /// but the distributed cache (Redis) survives the restart and serves model mapping entries
    /// whose [JsonIgnore] navigation properties (ModelProviderTypeAssociation.Model) were
    /// dropped during JSON serialization. Capability checks then read false until a
    /// ModelMappingChanged invalidation repopulates the entry.
    ///
    /// These tests use the real CacheManager and a real serializing IDistributedCache shared
    /// across two service instances to simulate a restart.
    /// </summary>
    public class ModelMappingCacheColdStartTests
    {
        private const string TestModelAlias = "smoke-image";

        private static ModelProviderMapping CreateFullyLoadedMapping()
        {
            return new ModelProviderMapping
            {
                Id = 5,
                ModelAlias = TestModelAlias,
                ProviderId = 1,
                ProviderModelId = "image-model-1",
                IsEnabled = true,
                Provider = new Provider
                {
                    Id = 1,
                    ProviderName = "Test Provider",
                    ProviderType = ProviderType.OpenAI
                },
                ModelProviderTypeAssociationId = 10,
                ModelProviderTypeAssociation = new ModelProviderTypeAssociation
                {
                    Id = 10,
                    ModelId = 20,
                    Identifier = "image-model-1",
                    Model = new Model
                    {
                        Id = 20,
                        Name = "Image Model",
                        SupportsImageGeneration = true
                    }
                }
            };
        }

        private static CachedModelProviderMappingService CreateService(
            IDistributedCache distributedCache,
            IModelProviderMappingService innerService)
        {
            var cacheManager = new CacheManager(
                new MemoryCache(Microsoft.Extensions.Options.Options.Create(new MemoryCacheOptions())),
                distributedCache,
                Mock.Of<ILogger<CacheManager>>());

            return new CachedModelProviderMappingService(
                innerService,
                cacheManager,
                Mock.Of<ILogger<CachedModelProviderMappingService>>());
        }

        [Fact]
        public async Task ColdStart_EntryServedFromDistributedCache_StillReportsCapabilities()
        {
            // A real serializing distributed cache, shared across both "instances" the way
            // Redis is shared across Gateway restarts.
            var sharedDistributedCache = new MemoryDistributedCache(
                Microsoft.Extensions.Options.Options.Create(new MemoryDistributedCacheOptions()));

            var innerService = new Mock<IModelProviderMappingService>();
            innerService
                .Setup(x => x.GetMappingByModelAliasAsync(TestModelAlias))
                .ReturnsAsync(CreateFullyLoadedMapping);

            // Instance 1 (before restart): populates memory + distributed cache from the DB.
            var serviceBeforeRestart = CreateService(sharedDistributedCache, innerService.Object);
            var warm = await serviceBeforeRestart.GetMappingByModelAliasAsync(TestModelAlias);
            Assert.True(warm?.ModelProviderTypeAssociation?.Model?.SupportsImageGeneration);

            // Instance 2 (after restart): empty memory cache, same distributed cache. The
            // distributed entry lost its Model graph during serialization, so without the
            // fallback the capability check reads false here.
            var serviceAfterRestart = CreateService(sharedDistributedCache, innerService.Object);
            var cold = await serviceAfterRestart.GetMappingByModelAliasAsync(TestModelAlias);

            Assert.NotNull(cold);
            Assert.NotNull(cold.ModelProviderTypeAssociation?.Model);
            Assert.True(cold.ModelProviderTypeAssociation.Model.SupportsImageGeneration);
        }

        [Fact]
        public async Task ColdStart_SubsequentRequests_ServeFullGraphFromMemoryWithoutExtraDbCalls()
        {
            var sharedDistributedCache = new MemoryDistributedCache(
                Microsoft.Extensions.Options.Options.Create(new MemoryDistributedCacheOptions()));

            var innerService = new Mock<IModelProviderMappingService>();
            innerService
                .Setup(x => x.GetMappingByModelAliasAsync(TestModelAlias))
                .ReturnsAsync(CreateFullyLoadedMapping);

            var serviceBeforeRestart = CreateService(sharedDistributedCache, innerService.Object);
            await serviceBeforeRestart.GetMappingByModelAliasAsync(TestModelAlias);

            var serviceAfterRestart = CreateService(sharedDistributedCache, innerService.Object);
            var first = await serviceAfterRestart.GetMappingByModelAliasAsync(TestModelAlias);
            var second = await serviceAfterRestart.GetMappingByModelAliasAsync(TestModelAlias);

            Assert.True(first?.ModelProviderTypeAssociation?.Model?.SupportsImageGeneration);
            Assert.True(second?.ModelProviderTypeAssociation?.Model?.SupportsImageGeneration);

            // One DB call to warm instance 1, one fallback reload on instance 2's cold start;
            // the second cold request is served from instance 2's refreshed memory tier.
            innerService.Verify(x => x.GetMappingByModelAliasAsync(TestModelAlias), Times.Exactly(2));
        }
    }
}
