using Microsoft.AspNetCore.Mvc;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Tests.Http.Builders;
using Xunit.Abstractions;

namespace ConduitLLM.Tests.Http.Controllers.Discovery.GetModels
{
    [Trait("Category", "Unit")]
    [Trait("Component", "Http")]
    [Trait("Phase", "2")]
    public class GetModelsResponseStructureTests : DiscoveryControllerTestsBase
    {
        public GetModelsResponseStructureTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task GetModels_ReturnsFlatStructureWithBooleanCapabilityFlags()
        {
            // Arrange
            SetupValidVirtualKey("valid-key");

            var mappings = new List<ModelProviderMapping>
            {
                new ModelProviderMappingBuilder()
                    .WithModelAlias("gpt-4")
                    .WithFullCapabilities()
                    .Build()
            };

            SetupModelProviderMappings(mappings);

            // Act
            var result = await Controller.GetModels();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            dynamic response = okResult.Value!;
            dynamic model = ((IEnumerable<dynamic>)response.data).First();
            
            Assert.True(model.supports_chat);
            Assert.True(model.supports_streaming);
            Assert.True(model.supports_vision);
            Assert.True(model.supports_function_calling);
            Assert.True(model.supports_audio_transcription);
            Assert.True(model.supports_text_to_speech);
            Assert.True(model.supports_realtime_audio);
            Assert.True(model.supports_video_generation);
            Assert.True(model.supports_image_generation);
            Assert.True(model.supports_embeddings);
        }

        [Fact]
        public async Task GetModels_IncludesMetadataFields_ReturnsCompleteModelInfo()
        {
            // Arrange
            SetupValidVirtualKey("valid-key");

            var mappings = new List<ModelProviderMapping>
            {
                new ModelProviderMappingBuilder()
                    .WithModelAlias("gpt-4")
                    .WithDescription("Advanced language model")
                    .WithModelCardUrl("https://example.com/gpt-4")
                    .WithMaxTokens(8192)
                    .WithTokenizerType(TokenizerType.Cl100KBase)
                    .Build()
            };

            SetupModelProviderMappings(mappings);

            // Act
            var result = await Controller.GetModels();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            dynamic response = okResult.Value!;
            dynamic model = ((IEnumerable<dynamic>)response.data).First();
            
            Assert.Equal("gpt-4", model.id);
            Assert.Equal("gpt-4", model.display_name);
            Assert.Equal("Advanced language model", model.description);
            Assert.Equal("https://example.com/gpt-4", model.model_card_url);
            Assert.Equal(8192, model.max_tokens);
            Assert.Equal("cl100kbase", model.tokenizer_type);
        }

        [Fact]
        public async Task GetModels_HandlesNullDescriptionAndModelCardUrl_ReturnsEmptyStrings()
        {
            // Arrange
            SetupValidVirtualKey("valid-key");

            var mappings = new List<ModelProviderMapping>
            {
                new ModelProviderMappingBuilder()
                    .WithModelAlias("gpt-4")
                    .WithDescription(null)
                    .WithModelCardUrl(null)
                    .Build()
            };

            SetupModelProviderMappings(mappings);

            // Act
            var result = await Controller.GetModels();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            dynamic response = okResult.Value!;
            dynamic model = ((IEnumerable<dynamic>)response.data).First();
            
            Assert.Equal(string.Empty, model.description);
            Assert.Equal(string.Empty, model.model_card_url);
        }
    }
}