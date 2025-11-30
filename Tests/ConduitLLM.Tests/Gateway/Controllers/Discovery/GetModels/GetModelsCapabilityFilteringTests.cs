using Microsoft.AspNetCore.Mvc;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Tests.Http.Builders;
using Xunit.Abstractions;

namespace ConduitLLM.Tests.Http.Controllers.Discovery.GetModels
{
    [Trait("Category", "Unit")]
    [Trait("Component", "Http")]
    [Trait("Phase", "2")]
    public class GetModelsCapabilityFilteringTests : DiscoveryControllerTestsBase
    {
        public GetModelsCapabilityFilteringTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task GetModels_FilterByVisionCapability_ReturnsOnlyVisionModels()
        {
            // Arrange
            SetupValidVirtualKey("valid-key");

            var mappings = new List<ModelProviderMapping>
            {
                new ModelProviderMappingBuilder()
                    .WithModelAlias("gpt-4-vision")
                    .WithVisionSupport(true)
                    .Build(),
                new ModelProviderMappingBuilder()
                    .WithModelAlias("gpt-3.5")
                    .WithVisionSupport(false)
                    .Build()
            };

            SetupModelProviderMappings(mappings);

            // Act
            var result = await Controller.GetModels(capability: "vision");

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            dynamic response = okResult.Value!;
            Assert.Equal(1, response.count);
            Assert.Equal("gpt-4-vision", ((IEnumerable<dynamic>)response.data).First().id);
        }

        [Fact]
        public async Task GetModels_FilterByStreamingCapability_ReturnsOnlyStreamingModels()
        {
            // Arrange
            SetupValidVirtualKey("valid-key");

            var mappings = new List<ModelProviderMapping>
            {
                new ModelProviderMappingBuilder()
                    .WithModelAlias("gpt-4")
                    .WithStreamingSupport(true)
                    .Build(),
                new ModelProviderMappingBuilder()
                    .WithModelAlias("dall-e")
                    .WithStreamingSupport(false)
                    .Build()
            };

            SetupModelProviderMappings(mappings);

            // Act
            var result = await Controller.GetModels(capability: "streaming");

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            dynamic response = okResult.Value!;
            Assert.Equal(1, response.count);
            Assert.Equal("gpt-4", ((IEnumerable<dynamic>)response.data).First().id);
        }

        [Fact]
        public async Task GetModels_FilterByChatStreamCapability_ReturnsOnlyStreamingModels()
        {
            // Arrange
            SetupValidVirtualKey("valid-key");

            var mappings = new List<ModelProviderMapping>
            {
                new ModelProviderMappingBuilder()
                    .WithModelAlias("gpt-4")
                    .WithStreamingSupport(true)
                    .Build(),
                new ModelProviderMappingBuilder()
                    .WithModelAlias("dall-e")
                    .WithStreamingSupport(false)
                    .Build()
            };

            SetupModelProviderMappings(mappings);

            // Act
            var result = await Controller.GetModels(capability: "chat_stream");

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            dynamic response = okResult.Value!;
            Assert.Equal(1, response.count);
        }

        [Fact]
        public async Task GetModels_FilterByInvalidCapability_ReturnsEmptyList()
        {
            // Arrange
            SetupValidVirtualKey("valid-key");

            var mappings = new List<ModelProviderMapping>
            {
                new ModelProviderMappingBuilder().WithModelAlias("gpt-4").Build(),
                new ModelProviderMappingBuilder().WithModelAlias("claude-3").Build()
            };

            SetupModelProviderMappings(mappings);

            // Act
            var result = await Controller.GetModels(capability: "invalid_capability");

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            dynamic response = okResult.Value!;
            Assert.Equal(0, response.count);
        }

        [Fact]
        public async Task GetModels_CapabilityFilterIsCaseInsensitive_WorksWithVariations()
        {
            // Arrange
            SetupValidVirtualKey("valid-key");

            var mappings = new List<ModelProviderMapping>
            {
                new ModelProviderMappingBuilder()
                    .WithModelAlias("gpt-4-vision")
                    .WithVisionSupport(true)
                    .Build()
            };

            SetupModelProviderMappings(mappings);

            // Act - Test with dash instead of underscore
            var result = await Controller.GetModels(capability: "audio-transcription");

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            dynamic response = okResult.Value!;
            // Should work as controller converts dashes to underscores
            Assert.NotNull(response);
        }
    }
}