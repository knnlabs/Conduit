using Microsoft.AspNetCore.Mvc;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Tests.Http.Builders;
using Xunit.Abstractions;

namespace ConduitLLM.Tests.Http.Controllers.Discovery.GetModels
{
    [Trait("Category", "Unit")]
    [Trait("Component", "Http")]
    [Trait("Phase", "2")]
    public class GetModelsDataRetrievalTests : DiscoveryControllerTestsBase
    {
        public GetModelsDataRetrievalTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task GetModels_WithValidKey_ReturnsAllEnabledModels()
        {
            // Arrange
            SetupValidVirtualKey("valid-key");

            var mappings = new List<ModelProviderMapping>
            {
                new ModelProviderMappingBuilder()
                    .WithModelAlias("gpt-4")
                    .WithVisionSupport(true)
                    .Build(),
                new ModelProviderMappingBuilder()
                    .WithModelAlias("claude-3")
                    .WithVisionSupport(false)
                    .Build()
            };

            SetupModelProviderMappings(mappings);

            // Act
            var result = await Controller.GetModels(capability: null);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            dynamic response = okResult.Value!;
            Assert.Equal(2, response.count);
            Assert.Equal(2, ((IEnumerable<object>)response.data).Count());
        }

        [Fact]
        public async Task GetModels_SkipsModelsWithNullModel_ReturnsOnlyValid()
        {
            // Arrange
            SetupValidVirtualKey("valid-key");

            var mappings = new List<ModelProviderMapping>
            {
                new ModelProviderMappingBuilder()
                    .WithModelAlias("gpt-4")
                    .WithModel(null) // Model is null
                    .Build(),
                new ModelProviderMappingBuilder()
                    .WithModelAlias("claude-3")
                    .Build()
            };

            SetupModelProviderMappings(mappings);

            // Act
            var result = await Controller.GetModels();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            dynamic response = okResult.Value!;
            Assert.Equal(1, response.count);
        }

        [Fact]
        public async Task GetModels_SkipsModelsWithNullCapabilities_ReturnsOnlyValid()
        {
            // Arrange
            SetupValidVirtualKey("valid-key");

            var mappings = new List<ModelProviderMapping>
            {
                new ModelProviderMappingBuilder()
                    .WithModelAlias("gpt-4")
                    .WithCapabilities(null) // Capabilities is null
                    .Build(),
                new ModelProviderMappingBuilder()
                    .WithModelAlias("claude-3")
                    .Build()
            };

            SetupModelProviderMappings(mappings);

            // Act
            var result = await Controller.GetModels();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            dynamic response = okResult.Value!;
            Assert.Equal(1, response.count);
        }

        [Fact]
        public async Task GetModels_RespectsProviderIsEnabledFlag_ReturnsOnlyEnabledProviders()
        {
            // Arrange
            SetupValidVirtualKey("valid-key");

            var mappings = new List<ModelProviderMapping>
            {
                new ModelProviderMappingBuilder()
                    .WithModelAlias("gpt-4")
                    .WithProviderEnabled(false) // Provider disabled
                    .Build(),
                new ModelProviderMappingBuilder()
                    .WithModelAlias("claude-3")
                    .WithProviderEnabled(true)
                    .Build()
            };

            SetupModelProviderMappings(mappings);

            // Act
            var result = await Controller.GetModels();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            dynamic response = okResult.Value!;
            Assert.Equal(1, response.count);
        }

        [Fact]
        public async Task GetModels_RespectsModelProviderMappingIsEnabledFlag_ReturnsOnlyEnabledMappings()
        {
            // Arrange
            SetupValidVirtualKey("valid-key");

            var mappings = new List<ModelProviderMapping>
            {
                new ModelProviderMappingBuilder()
                    .WithModelAlias("gpt-4")
                    .WithMappingEnabled(false) // Mapping disabled
                    .Build(),
                new ModelProviderMappingBuilder()
                    .WithModelAlias("claude-3")
                    .WithMappingEnabled(true)
                    .Build()
            };

            SetupModelProviderMappings(mappings);

            // Act
            var result = await Controller.GetModels();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            dynamic response = okResult.Value!;
            Assert.Equal(1, response.count);
        }
    }
}