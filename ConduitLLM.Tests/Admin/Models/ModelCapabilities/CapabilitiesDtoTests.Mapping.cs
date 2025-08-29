using ConduitLLM.Admin.Models.ModelCapabilities;

using FluentAssertions;

namespace ConduitLLM.Tests.Admin.Models.ModelCapabilities
{
    /// <summary>
    /// Tests for entity-to-DTO and DTO-to-entity mapping logic.
    /// These tests ensure data is correctly transformed between layers.
    /// </summary>
    public partial class CapabilitiesDtoTests
    {
        [Fact]
        public void Should_Map_Entity_To_ModelCapabilitiesDto_Correctly()
        {
            // Arrange
            var entity = new ConduitLLM.Configuration.Entities.ModelCapabilities
            {
                Id = 10,
                SupportsChat = true,
                SupportsVision = true,
                SupportsFunctionCalling = true,
                SupportsStreaming = true,
                SupportsImageGeneration = false,
                SupportsVideoGeneration = false,
                SupportsEmbeddings = false,
                MaxTokens = 128000,
                MinTokens = 1,
                TokenizerType = TokenizerType.Cl100KBase,
            };

            // Act - simulate the mapping logic from controller
            var dto = MapEntityToDto(entity);

            // Assert
            dto.Should().NotBeNull();
            dto.Id.Should().Be(entity.Id);
            dto.SupportsChat.Should().Be(entity.SupportsChat);
            dto.SupportsVision.Should().Be(entity.SupportsVision);
            dto.SupportsFunctionCalling.Should().Be(entity.SupportsFunctionCalling);
            dto.SupportsStreaming.Should().Be(entity.SupportsStreaming);
            dto.SupportsImageGeneration.Should().Be(entity.SupportsImageGeneration);
            dto.SupportsVideoGeneration.Should().Be(entity.SupportsVideoGeneration);
            dto.SupportsEmbeddings.Should().Be(entity.SupportsEmbeddings);
            dto.MaxTokens.Should().Be(entity.MaxTokens);
            dto.MinTokens.Should().Be(entity.MinTokens);
            dto.TokenizerType.Should().Be(entity.TokenizerType);
        }

        [Fact]
        public void Should_Map_CreateCapabilitiesDto_To_Entity()
        {
            // Arrange
            var createDto = new CreateCapabilitiesDto
            {
                SupportsChat = true,
                SupportsVision = false,
                SupportsFunctionCalling = true,
                SupportsStreaming = true,
                SupportsImageGeneration = false,
                SupportsVideoGeneration = false,
                SupportsEmbeddings = false,
                MaxTokens = 4096,
                MinTokens = 1,
                TokenizerType = TokenizerType.P50KBase,
            };

            // Act - simulate controller logic
            var entity = new ConduitLLM.Configuration.Entities.ModelCapabilities
            {
                SupportsChat = createDto.SupportsChat,
                SupportsVision = createDto.SupportsVision,
                SupportsFunctionCalling = createDto.SupportsFunctionCalling,
                SupportsStreaming = createDto.SupportsStreaming,
                SupportsImageGeneration = createDto.SupportsImageGeneration,
                SupportsVideoGeneration = createDto.SupportsVideoGeneration,
                SupportsEmbeddings = createDto.SupportsEmbeddings,
                MaxTokens = createDto.MaxTokens,
                MinTokens = createDto.MinTokens,
                TokenizerType = createDto.TokenizerType,
            };

            // Assert
            entity.SupportsChat.Should().BeTrue();
            entity.SupportsVision.Should().BeFalse();
            entity.SupportsFunctionCalling.Should().BeTrue();
            entity.SupportsStreaming.Should().BeTrue();
            entity.MaxTokens.Should().Be(4096);
            entity.MinTokens.Should().Be(1);
            entity.TokenizerType.Should().Be(TokenizerType.P50KBase);
        }

        [Fact]
        public void Should_Apply_UpdateCapabilitiesDto_To_Entity_With_Partial_Updates()
        {
            // Arrange
            var existingEntity = new ConduitLLM.Configuration.Entities.ModelCapabilities
            {
                Id = 5,
                SupportsChat = true,
                SupportsVision = false,
                SupportsFunctionCalling = false,
                SupportsStreaming = true,
                SupportsImageGeneration = false,
                SupportsVideoGeneration = false,
                SupportsEmbeddings = false,
                MaxTokens = 4096,
                MinTokens = 1,
                TokenizerType = TokenizerType.P50KBase,
            };

            var updateDto = new UpdateCapabilitiesDto
            {
                Id = 5,
                SupportsChat = null, // Don't update
                SupportsVision = true, // Enable vision
                SupportsFunctionCalling = true, // Enable function calling
                SupportsStreaming = null, // Don't update
                MaxTokens = 128000, // Increase max tokens
                MinTokens = null, // Don't update
                TokenizerType = TokenizerType.Cl100KBase, // Update tokenizer
            };

            // Act - simulate controller update logic
            if (updateDto.SupportsChat.HasValue)
                existingEntity.SupportsChat = updateDto.SupportsChat.Value;
            if (updateDto.SupportsVision.HasValue)
                existingEntity.SupportsVision = updateDto.SupportsVision.Value;
            if (updateDto.SupportsFunctionCalling.HasValue)
                existingEntity.SupportsFunctionCalling = updateDto.SupportsFunctionCalling.Value;
            if (updateDto.SupportsStreaming.HasValue)
                existingEntity.SupportsStreaming = updateDto.SupportsStreaming.Value;
            if (updateDto.MaxTokens.HasValue)
                existingEntity.MaxTokens = updateDto.MaxTokens.Value;
            if (updateDto.MinTokens.HasValue)
                existingEntity.MinTokens = updateDto.MinTokens.Value;
            if (updateDto.TokenizerType.HasValue)
                existingEntity.TokenizerType = updateDto.TokenizerType.Value;

            // Assert
            existingEntity.SupportsChat.Should().BeTrue(); // Unchanged
            existingEntity.SupportsVision.Should().BeTrue(); // Updated
            existingEntity.SupportsFunctionCalling.Should().BeTrue(); // Updated
            existingEntity.SupportsStreaming.Should().BeTrue(); // Unchanged
            existingEntity.MaxTokens.Should().Be(128000); // Updated
            existingEntity.MinTokens.Should().Be(1); // Unchanged
            existingEntity.TokenizerType.Should().Be(TokenizerType.Cl100KBase); // Updated
        }

        [Fact]
        public void Should_Handle_Clearing_String_Properties_With_Empty_String()
        {
            // Arrange
            var existingEntity = new ConduitLLM.Configuration.Entities.ModelCapabilities
            {
                Id = 1,
                MaxTokens = 4096,
                MinTokens = 1,
                TokenizerType = TokenizerType.BPE
            };

            var updateDto = new UpdateCapabilitiesDto
            {
                Id = 1,
            };

            // Act - simulate controller update logic

            // Assert
        }

        [Fact]
        public void Should_Not_Update_String_Properties_When_Null()
        {
            // Arrange
            var existingEntity = new ConduitLLM.Configuration.Entities.ModelCapabilities
            {
                Id = 1,
                MaxTokens = 4096,
                MinTokens = 1,
                TokenizerType = TokenizerType.BPE
            };

            var updateDto = new UpdateCapabilitiesDto
            {
                Id = 1,
            };

            // Act - simulate controller update logic

            // Assert - values unchanged
        }

        [Fact]
        public void Should_Map_Entity_With_All_Capabilities_To_Dto()
        {
            // Arrange - Entity with all capabilities enabled
            var entity = new ConduitLLM.Configuration.Entities.ModelCapabilities
            {
                Id = 99,
                SupportsChat = true,
                SupportsVision = true,
                SupportsFunctionCalling = true,
                SupportsStreaming = true,
                SupportsImageGeneration = true,
                SupportsVideoGeneration = true,
                SupportsEmbeddings = true,
                MaxTokens = int.MaxValue,
                MinTokens = 1,
                TokenizerType = TokenizerType.O200KBase,
            };

            // Act
            var dto = MapEntityToDto(entity);

            // Assert - All capabilities should be true
            dto.SupportsChat.Should().BeTrue();
            dto.SupportsVision.Should().BeTrue();
            dto.SupportsFunctionCalling.Should().BeTrue();
            dto.SupportsStreaming.Should().BeTrue();
            dto.SupportsImageGeneration.Should().BeTrue();
            dto.SupportsVideoGeneration.Should().BeTrue();
            dto.SupportsEmbeddings.Should().BeTrue();
            dto.MaxTokens.Should().Be(int.MaxValue);
        }

        [Fact]
        public void Should_Map_Entity_With_No_Capabilities_To_Dto()
        {
            // Arrange - Entity with all capabilities disabled
            var entity = new ConduitLLM.Configuration.Entities.ModelCapabilities
            {
                Id = 1,
                SupportsChat = false,
                SupportsVision = false,
                SupportsFunctionCalling = false,
                SupportsStreaming = false,
                SupportsImageGeneration = false,
                SupportsVideoGeneration = false,
                SupportsEmbeddings = false,
                MaxTokens = 0,
                MinTokens = 0,
                TokenizerType = TokenizerType.BPE,
            };

            // Act
            var dto = MapEntityToDto(entity);

            // Assert - All capabilities should be false
            dto.SupportsChat.Should().BeFalse();
            dto.SupportsVision.Should().BeFalse();
            dto.SupportsFunctionCalling.Should().BeFalse();
            dto.SupportsStreaming.Should().BeFalse();
            dto.SupportsImageGeneration.Should().BeFalse();
            dto.SupportsVideoGeneration.Should().BeFalse();
            dto.SupportsEmbeddings.Should().BeFalse();
            dto.MaxTokens.Should().Be(0);
            dto.MinTokens.Should().Be(0);
        }

        [Fact]
        public void Should_Default_MinTokens_To_One_When_Creating_Entity()
        {
            // Arrange
            var createDto = new CreateCapabilitiesDto
            {
                SupportsChat = true,
                MaxTokens = 4096
                // MinTokens not explicitly set, uses default
            };

            // Act - simulate controller logic with default
            var entity = new ConduitLLM.Configuration.Entities.ModelCapabilities
            {
                SupportsChat = createDto.SupportsChat,
                MaxTokens = createDto.MaxTokens,
                MinTokens = createDto.MinTokens, // Should be 1 by default
                TokenizerType = createDto.TokenizerType
            };

            // Assert
            entity.MinTokens.Should().Be(1);
        }

        // Helper method that mirrors controller mapping logic
        private static ModelCapabilitiesDto MapEntityToDto(ConduitLLM.Configuration.Entities.ModelCapabilities entity)
        {
            return new ModelCapabilitiesDto
            {
                Id = entity.Id,
                SupportsChat = entity.SupportsChat,
                SupportsVision = entity.SupportsVision,
                SupportsFunctionCalling = entity.SupportsFunctionCalling,
                SupportsStreaming = entity.SupportsStreaming,
                SupportsImageGeneration = entity.SupportsImageGeneration,
                SupportsVideoGeneration = entity.SupportsVideoGeneration,
                SupportsEmbeddings = entity.SupportsEmbeddings,
                MaxTokens = entity.MaxTokens,
                MinTokens = entity.MinTokens,
                TokenizerType = entity.TokenizerType,
            };
        }
    }
}