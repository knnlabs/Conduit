using System.Text.Json;
using FluentAssertions;
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
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            dynamic response = okResult.Value!;
            var model = ((List<JsonElement>)response.data).First();
            var capabilities = model.GetProperty("capabilities");

            // Capabilities are now nested under a 'capabilities' object
            Assert.True(capabilities.GetProperty("chat").GetBoolean());
            Assert.True(capabilities.GetProperty("chat_stream").GetBoolean());
            Assert.True(capabilities.GetProperty("vision").GetBoolean());
            Assert.True(capabilities.GetProperty("function_calling").GetBoolean());
            Assert.True(capabilities.GetProperty("video_generation").GetBoolean());
            Assert.True(capabilities.GetProperty("image_generation").GetBoolean());
            Assert.True(capabilities.GetProperty("embeddings").GetBoolean());
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
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            dynamic response = okResult.Value!;
            var model = ((List<JsonElement>)response.data).First();

            Assert.Equal("gpt-4", model.GetProperty("id").GetString());
            Assert.Equal("gpt-4", model.GetProperty("display_name").GetString());
            Assert.Equal("Advanced language model", model.GetProperty("description").GetString());
            Assert.Equal("https://example.com/gpt-4", model.GetProperty("model_card_url").GetString());
            Assert.Equal(8192, model.GetProperty("max_tokens").GetInt32());
            Assert.Equal("cl100kbase", model.GetProperty("tokenizer_type").GetString());
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
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            dynamic response = okResult.Value!;
            var model = ((List<JsonElement>)response.data).First();

            Assert.Equal(string.Empty, model.GetProperty("description").GetString());
            Assert.Equal(string.Empty, model.GetProperty("model_card_url").GetString());
        }

        [Fact]
        public async Task GetModels_UsesAssociationTokenOverrides_WhenPresent()
        {
            // Arrange
            SetupValidVirtualKey("valid-key");

            // Create a mapping with default model token values
            var mapping = new ModelProviderMappingBuilder()
                .WithModelAlias("gpt-oss-120b")
                .WithMaxTokens(8192) // This sets Model.MaxInputTokens = 4096, MaxOutputTokens = 4096
                .Build();

            // Override token limits at the association level (like the real gpt-oss-120b)
            mapping.ModelProviderTypeAssociation.MaxInputTokens = 642111;
            mapping.ModelProviderTypeAssociation.MaxOutputTokens = null; // Explicitly test null override

            var mappings = new List<ModelProviderMapping> { mapping };
            SetupModelProviderMappings(mappings);

            // Act
            var result = await Controller.GetModels();

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            dynamic response = okResult.Value!;
            var model = ((List<JsonElement>)response.data).First();

            // Verify that association overrides are used, not model defaults
            Assert.Equal(642111, model.GetProperty("max_input_tokens").GetInt32()); // Association override
            Assert.Equal(4096, model.GetProperty("max_output_tokens").GetInt32());  // Falls back to Model default since association is null
            Assert.Equal(642111 + 4096, model.GetProperty("max_tokens").GetInt32()); // Combined total
        }
    }
}
