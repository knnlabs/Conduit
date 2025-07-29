using Xunit;
using FluentAssertions;

namespace ConduitLLM.Tests.Configuration
{
    /// <summary>
    /// Simple tests for the SupportsChat property
    /// </summary>
    public class SimpleSupportsChatTests
    {
        [Fact]
        public void ModelProviderMapping_SupportsChatProperty_Exists()
        {
            // Arrange
            var mapping = new ConduitLLM.Configuration.Entities.ModelProviderMapping();

            // Act
            mapping.SupportsChat = true;

            // Assert
            mapping.SupportsChat.Should().BeTrue();
        }

        [Fact]
        public void ModelProviderMapping_SupportsChatDefaultValue_IsFalse()
        {
            // Arrange & Act
            var mapping = new ConduitLLM.Configuration.Entities.ModelProviderMapping();

            // Assert
            mapping.SupportsChat.Should().BeFalse();
        }

        [Fact]
        public void ModelProviderMapping_AllCapabilityFlags_DefaultToFalse()
        {
            // Arrange & Act
            var mapping = new ConduitLLM.Configuration.Entities.ModelProviderMapping();

            // Assert
            mapping.SupportsChat.Should().BeFalse();
            mapping.SupportsEmbeddings.Should().BeFalse();
            mapping.SupportsVision.Should().BeFalse();
            mapping.SupportsImageGeneration.Should().BeFalse();
            mapping.SupportsFunctionCalling.Should().BeFalse();
        }

        [Fact]
        public void ModelProviderMapping_CanSetMultipleCapabilities()
        {
            // Arrange
            var mapping = new ConduitLLM.Configuration.Entities.ModelProviderMapping();

            // Act
            mapping.SupportsChat = true;
            mapping.SupportsVision = true;
            mapping.SupportsFunctionCalling = true;

            // Assert
            mapping.SupportsChat.Should().BeTrue();
            mapping.SupportsVision.Should().BeTrue();
            mapping.SupportsFunctionCalling.Should().BeTrue();
            mapping.SupportsEmbeddings.Should().BeFalse(); // Still false
            mapping.SupportsImageGeneration.Should().BeFalse(); // Still false
        }
    }
}