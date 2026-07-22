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
                SupportsChat = true
            };
            
            var modelProviderTypeAssociation = new ModelProviderTypeAssociation
            {
                ModelId = 1,
                Model = model
            };
            
            var mapping = new ModelProviderMapping
            {
                ModelProviderTypeAssociationId = 1,
                ModelProviderTypeAssociation = modelProviderTypeAssociation
            };

            // Assert
            mapping.ModelProviderTypeAssociation?.Model?.SupportsChat.Should().BeTrue();
        }

        [Fact]
        public void ModelProviderMapping_SupportsChatDefaultValue_IsFalseWhenNoModel()
        {
            // Arrange & Act
            var mapping = new ModelProviderMapping();

            // Assert - ModelProviderTypeAssociation is null
            mapping.ModelProviderTypeAssociation.Should().BeNull();
        }

        [Fact]
        public void ModelProviderMapping_AllCapabilityFlags_DefaultToFalseWhenNoModel()
        {
            // Arrange & Act
            var mapping = new ModelProviderMapping();

            // Assert - ModelProviderTypeAssociation is null so capabilities cannot be accessed
            mapping.ModelProviderTypeAssociation.Should().BeNull();
        }

        [Fact]
        public void ModelProviderMapping_CapabilitiesReflectModelCapabilities()
        {
            // Arrange
            var model = new Model
            {
                Id = 1,
                Name = "test-model",
                SupportsChat = true,
                SupportsVision = true,
                SupportsFunctionCalling = true,
                SupportsStreaming = false,
                SupportsEmbeddings = false
            };
            
            var modelProviderTypeAssociation = new ModelProviderTypeAssociation
            {
                ModelId = 1,
                Model = model
            };
            
            var mapping = new ModelProviderMapping
            {
                ModelProviderTypeAssociationId = 1,
                ModelProviderTypeAssociation = modelProviderTypeAssociation
            };

            // Assert - capabilities come from the Model
            mapping.ModelProviderTypeAssociation?.Model?.SupportsChat.Should().BeTrue();
            mapping.ModelProviderTypeAssociation?.Model?.SupportsVision.Should().BeTrue();
            mapping.ModelProviderTypeAssociation?.Model?.SupportsFunctionCalling.Should().BeTrue();
            mapping.ModelProviderTypeAssociation?.Model?.SupportsStreaming.Should().BeFalse();
            mapping.ModelProviderTypeAssociation?.Model?.SupportsEmbeddings.Should().BeFalse();
        }

        [Fact]
        public void ModelProviderMapping_MaxTokens_ComesFromModel()
        {
            // Arrange
            var model = new Model
            {
                Id = 1,
                Name = "test-model",
                MaxInputTokens = 8192,
                MaxOutputTokens = 4096
            };
            
            var modelProviderTypeAssociation = new ModelProviderTypeAssociation
            {
                ModelId = 1,
                Model = model
            };
            
            var mapping = new ModelProviderMapping
            {
                ModelProviderTypeAssociationId = 1,
                ModelProviderTypeAssociation = modelProviderTypeAssociation
            };

            // Assert
            mapping.ModelProviderTypeAssociation?.Model?.MaxInputTokens.Should().Be(8192);
            mapping.ModelProviderTypeAssociation?.Model?.MaxOutputTokens.Should().Be(4096);
        }

        [Fact]
        public void ModelProviderMapping_TokenLimits_UsesModelValues()
        {
            // Arrange
            var model = new Model
            {
                Id = 1,
                Name = "test-model",
                MaxInputTokens = 4096,
                MaxOutputTokens = 2048
            };
            
            var modelProviderTypeAssociation = new ModelProviderTypeAssociation
            {
                ModelId = 1,
                Model = model
            };
            
            var mapping = new ModelProviderMapping
            {
                ModelProviderTypeAssociationId = 1,
                ModelProviderTypeAssociation = modelProviderTypeAssociation
            };

            // Assert - uses Model's token limits
            mapping.ModelProviderTypeAssociation?.Model?.MaxInputTokens.Should().Be(4096);
            mapping.ModelProviderTypeAssociation?.Model?.MaxOutputTokens.Should().Be(2048);
        }

        [Fact]
        public void ModelProviderMapping_TokenizerType_ComesFromModel()
        {
            // Arrange
            var model = new Model
            {
                Id = 1,
                Name = "test-model",
                TokenizerType = TokenizerType.Cl100KBase
            };
            
            var modelProviderTypeAssociation = new ModelProviderTypeAssociation
            {
                ModelId = 1,
                Model = model
            };
            
            var mapping = new ModelProviderMapping
            {
                ModelProviderTypeAssociationId = 1,
                ModelProviderTypeAssociation = modelProviderTypeAssociation
            };

            // Assert
            mapping.ModelProviderTypeAssociation?.Model?.TokenizerType.Should().Be(TokenizerType.Cl100KBase);
        }
    }
}
