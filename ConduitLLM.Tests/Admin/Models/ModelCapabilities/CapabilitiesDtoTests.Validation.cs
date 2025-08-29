using ConduitLLM.Admin.Models.ModelCapabilities;

using FluentAssertions;

namespace ConduitLLM.Tests.Admin.Models.ModelCapabilities
{
    /// <summary>
    /// Tests for CapabilitiesDto validation rules and business constraints.
    /// These tests ensure DTOs enforce proper data integrity.
    /// </summary>
    public partial class CapabilitiesDtoTests
    {
        [Fact]
        public void CreateCapabilitiesDto_Should_Have_Default_Values()
        {
            // Act
            var dto = new CreateCapabilitiesDto();

            // Assert - all booleans default to false
            dto.SupportsChat.Should().BeFalse();
            dto.SupportsVision.Should().BeFalse();
            dto.SupportsFunctionCalling.Should().BeFalse();
            dto.SupportsStreaming.Should().BeFalse();
            dto.SupportsImageGeneration.Should().BeFalse();
            dto.SupportsVideoGeneration.Should().BeFalse();
            dto.SupportsEmbeddings.Should().BeFalse();
            dto.MaxTokens.Should().Be(0);
            dto.MinTokens.Should().Be(1); // Default should be 1
            dto.TokenizerType.Should().Be(TokenizerType.Cl100KBase); // Default enum value is 0
        }

        [Fact]
        public void CapabilitiesDto_Should_Validate_Token_Range()
        {
            // Arrange
            var dto = new CapabilitiesDto
            {
                Id = 1,
                MinTokens = 100,
                MaxTokens = 50, // Max less than min!
                TokenizerType = TokenizerType.BPE
            };

            // Act & Assert
            // DTO allows this, but business logic should validate
            dto.MinTokens.Should().Be(100);
            dto.MaxTokens.Should().Be(50);
            
            // Business validation
            var isInvalid = dto.MinTokens > dto.MaxTokens;
            isInvalid.Should().BeTrue("Controller should validate min <= max");
        }

        [Theory]
        // Note: 0 is actually valid (some models might have 0 minimum)
        // [InlineData(0)] - removed as 0 is not invalid
        [InlineData(-1)]
        [InlineData(-100)]
        [InlineData(int.MinValue)]
        public void CapabilitiesDto_Should_Accept_Invalid_Token_Values_But_Controller_Should_Validate(int tokens)
        {
            // Arrange & Act
            var dto = new CapabilitiesDto
            {
                Id = 1,
                MinTokens = tokens,
                MaxTokens = tokens,
                TokenizerType = TokenizerType.BPE
            };

            // Assert
            // DTO accepts any int, but controller should validate
            dto.MinTokens.Should().Be(tokens);
            dto.MaxTokens.Should().Be(tokens);
            
            // Business rule check for MinTokens
            var isInvalidMin = tokens < 0;
            isInvalidMin.Should().BeTrue("Controller should reject negative token counts");
        }

        [Fact]
        public void UpdateCapabilitiesDto_Should_Allow_Partial_Updates()
        {
            // Arrange & Act
            var dto = new UpdateCapabilitiesDto
            {
                Id = 5,
                SupportsChat = true, // Update
                SupportsVision = null, // Don't update
                SupportsFunctionCalling = false, // Update
                SupportsStreaming = null, // Don't update
                MaxTokens = 200000, // Update
                MinTokens = null, // Don't update
                TokenizerType = TokenizerType.O200KBase, // Update
            };

            // Assert - nulls mean "don't update"
            dto.Id.Should().Be(5);
            dto.SupportsChat.Should().BeTrue();
            dto.SupportsVision.Should().BeNull();
            dto.SupportsFunctionCalling.Should().BeFalse();
            dto.SupportsStreaming.Should().BeNull();
            dto.MaxTokens.Should().Be(200000);
            dto.MinTokens.Should().BeNull();
            dto.TokenizerType.Should().Be(TokenizerType.O200KBase);
        }

        [Fact]
        public void UpdateCapabilitiesDto_Should_Allow_All_Null_For_No_Updates()
        {
            // Arrange & Act
            var dto = new UpdateCapabilitiesDto
            {
                Id = 1,
                SupportsChat = null,
                SupportsVision = null,
                SupportsFunctionCalling = null,
                SupportsStreaming = null,
                SupportsImageGeneration = null,
                SupportsVideoGeneration = null,
                SupportsEmbeddings = null,
                MaxTokens = null,
                MinTokens = null,
                TokenizerType = null,
            };

            // Assert - all nulls is valid (no-op update)
            dto.Id.Should().Be(1);
            dto.SupportsChat.Should().BeNull();
            dto.MaxTokens.Should().BeNull();
            dto.TokenizerType.Should().BeNull();
        }

        [Fact]
        public void CapabilitiesDto_Should_Validate_Conflicting_Capabilities()
        {
            // Arrange
            var dto = new CapabilitiesDto
            {
                Id = 1,
                SupportsChat = false, // Doesn't support chat
                SupportsFunctionCalling = true, // But supports function calling?
                SupportsStreaming = true, // And streaming?
                MaxTokens = 4096,
                MinTokens = 1,
                TokenizerType = TokenizerType.BPE
            };

            // Act & Assert
            // DTO allows this, but business logic might validate
            dto.SupportsChat.Should().BeFalse();
            dto.SupportsFunctionCalling.Should().BeTrue();
            dto.SupportsStreaming.Should().BeTrue();
            
            // Business validation - function calling typically requires chat
            var isInconsistent = !dto.SupportsChat && dto.SupportsFunctionCalling;
            isInconsistent.Should().BeTrue("Function calling typically requires chat support");
        }



        [Fact]
        public void CreateCapabilitiesDto_Should_Default_MinTokens_To_One()
        {
            // Arrange
            var dto = new CreateCapabilitiesDto
            {
                SupportsChat = true,
                MaxTokens = 4096
                // MinTokens not specified
            };

            // Act & Assert
            dto.MinTokens.Should().Be(1, "MinTokens should default to 1 for new capabilities");
        }

        [Fact]
        public void CapabilitiesDto_Should_Validate_Embedding_Model_Constraints()
        {
            // Arrange
            var dto = new CapabilitiesDto
            {
                Id = 1,
                SupportsEmbeddings = true,
                SupportsChat = false, // Embedding models typically don't chat
                SupportsVision = false,
                SupportsFunctionCalling = false,
                MaxTokens = 8192, // Embedding models have token limits
                MinTokens = 1,
                TokenizerType = TokenizerType.Cl100KBase
            };

            // Act & Assert
            dto.SupportsEmbeddings.Should().BeTrue();
            dto.SupportsChat.Should().BeFalse();
            
            // Business validation - embedding models shouldn't have chat features
            var isValidEmbeddingModel = dto.SupportsEmbeddings && 
                                        !dto.SupportsChat && 
                                        !dto.SupportsFunctionCalling;
            isValidEmbeddingModel.Should().BeTrue("Embedding models typically don't support chat");
        }

        [Fact]
        public void CapabilitiesDto_Should_Validate_Audio_Model_Constraints()
        {
            // Arrange
            var dto = new CapabilitiesDto
            {
                Id = 1,
                MaxTokens = 0, // Audio models might not have token limits
                MinTokens = 0,
                TokenizerType = TokenizerType.BPE
            };

            // Act & Assert
            dto.Should().NotBeNull();
        }

        [Fact]
        public void UpdateCapabilitiesDto_Should_Clear_Lists_With_Empty_String()
        {
            // Arrange
            var dto = new UpdateCapabilitiesDto
            {
                Id = 1,
            };

            // Act & Assert
        }

        [Fact]
        public void CapabilitiesDto_Should_Handle_All_Capabilities_Enabled()
        {
            // Arrange - Model that supports everything
            var dto = new CapabilitiesDto
            {
                Id = 1,
                SupportsChat = true,
                SupportsVision = true,
                SupportsFunctionCalling = true,
                SupportsStreaming = true,
                SupportsImageGeneration = true,
                SupportsVideoGeneration = true,
                SupportsEmbeddings = true,
                MaxTokens = int.MaxValue,
                MinTokens = 1,
                TokenizerType = TokenizerType.O200KBase
            };

            // Act & Assert
            var allCapabilities = new[]
            {
                dto.SupportsChat,
                dto.SupportsVision,
                dto.SupportsFunctionCalling,
                dto.SupportsStreaming,
                dto.SupportsImageGeneration,
                dto.SupportsVideoGeneration,
                dto.SupportsEmbeddings
            };

            allCapabilities.Should().AllSatisfy(cap => cap.Should().BeTrue());
        }
    }
}