// TODO: Update tests for new Model architecture where capabilities come from Model entity
using Xunit;
using FluentAssertions;
using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Tests.Configuration
{
    /// <summary>
    /// Simple tests for the SupportsChat property
    /// </summary>
    public class SimpleSupportsChatTests
    {
        [Fact]
        public void ModelProviderMapping_SupportsChatProperty_ComesFromModel()
        {
            // Arrange
            var model = new Model
            {
                Id = 1,
                Name = "test-model",
                ModelType = ModelType.Text,
                Capabilities = new ModelCapabilities
                {
                    SupportsChat = true
                }
            };
            
            var mapping = new ModelProviderMapping
            {
                ModelId = 1,
                Model = model
            };

            // Assert
            mapping.SupportsChat.Should().BeTrue();
        }

        [Fact]
        public void ModelProviderMapping_SupportsChatDefaultValue_IsFalseWhenNoModel()
        {
            // Arrange & Act
            var mapping = new ModelProviderMapping();

            // Assert - returns false when Model is null
            mapping.SupportsChat.Should().BeFalse();
        }

        [Fact]
        public void ModelProviderMapping_AllCapabilityFlags_DefaultToFalseWhenNoModel()
        {
            // Arrange & Act
            var mapping = new ModelProviderMapping();

            // Assert - all return false when Model is null
            mapping.SupportsChat.Should().BeFalse();
            mapping.SupportsEmbeddings.Should().BeFalse();
            mapping.SupportsVision.Should().BeFalse();
            mapping.SupportsImageGeneration.Should().BeFalse();
            mapping.SupportsFunctionCalling.Should().BeFalse();
        }

        [Fact]
        public void ModelProviderMapping_CapabilitiesReflectModelCapabilities()
        {
            // Arrange
            var model = new Model
            {
                Id = 1,
                Name = "test-model",
                ModelType = ModelType.Text,
                Capabilities = new ModelCapabilities
                {
                    SupportsChat = true,
                    SupportsVision = true,
                    SupportsFunctionCalling = true,
                    SupportsStreaming = false,
                    SupportsEmbeddings = false
                }
            };
            
            var mapping = new ModelProviderMapping
            {
                ModelId = 1,
                Model = model
            };

            // Assert - capabilities come from the Model
            mapping.SupportsChat.Should().BeTrue();
            mapping.SupportsVision.Should().BeTrue();
            mapping.SupportsFunctionCalling.Should().BeTrue();
            mapping.SupportsStreaming.Should().BeFalse();
            mapping.SupportsEmbeddings.Should().BeFalse();
        }

        [Fact]
        public void ModelProviderMapping_MaxContextTokens_UsesOverrideWhenSet()
        {
            // Arrange
            var model = new Model
            {
                Id = 1,
                Name = "test-model",
                ModelType = ModelType.Text,
                Capabilities = new ModelCapabilities
                {
                    MaxTokens = 4096
                }
            };
            
            var mapping = new ModelProviderMapping
            {
                ModelId = 1,
                Model = model,
                MaxContextTokensOverride = 8192
            };

            // Assert - override takes precedence
            mapping.MaxContextTokens.Should().Be(8192);
        }

        [Fact]
        public void ModelProviderMapping_MaxContextTokens_UsesModelCapabilitiesWhenNoOverride()
        {
            // Arrange
            var model = new Model
            {
                Id = 1,
                Name = "test-model",
                ModelType = ModelType.Text,
                Capabilities = new ModelCapabilities
                {
                    MaxTokens = 4096
                }
            };
            
            var mapping = new ModelProviderMapping
            {
                ModelId = 1,
                Model = model,
                MaxContextTokensOverride = null
            };

            // Assert - uses Model's capabilities
            mapping.MaxContextTokens.Should().Be(4096);
        }

        [Fact]
        public void ModelProviderMapping_TokenizerType_ComesFromModelCapabilities()
        {
            // Arrange
            var model = new Model
            {
                Id = 1,
                Name = "test-model",
                ModelType = ModelType.Text,
                Capabilities = new ModelCapabilities
                {
                    TokenizerType = TokenizerType.Cl100KBase
                }
            };
            
            var mapping = new ModelProviderMapping
            {
                ModelId = 1,
                Model = model
            };

            // Assert
            mapping.TokenizerType.Should().Be(TokenizerType.Cl100KBase);
        }
    }
}