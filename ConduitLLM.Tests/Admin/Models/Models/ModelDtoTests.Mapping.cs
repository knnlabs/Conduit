using ConduitLLM.Admin.Models.Models;
using ConduitLLM.Admin.Models.ModelCapabilities;
using ConduitLLM.Configuration.Entities;
using FluentAssertions;

namespace ConduitLLM.Tests.Admin.Models.Models
{
    /// <summary>
    /// Tests for entity-to-DTO and DTO-to-entity mapping logic.
    /// These tests ensure data is correctly transformed between layers.
    /// </summary>
    public partial class ModelDtoTests
    {
        [Fact]
        public void Should_Map_Entity_To_ModelDto_Correctly()
        {
            // Arrange
            var entity = new Model
            {
                Id = 42,
                Name = "gpt-4-turbo",
                ModelSeriesId = 5,
                ModelCapabilitiesId = 10,
                Capabilities = new ConduitLLM.Configuration.Entities.ModelCapabilities
                {
                    Id = 10,
                    SupportsChat = true,
                    SupportsVision = true,
                    SupportsFunctionCalling = true,
                    SupportsStreaming = true,
                    MaxTokens = 128000,
                    MinTokens = 1,
                    TokenizerType = TokenizerType.Cl100KBase,
                    SupportedVoices = "alloy,echo,fable",
                    SupportedLanguages = "en,es,fr,de,ja,zh",
                    SupportedFormats = "text,json"
                },
                IsActive = true,
                CreatedAt = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2024, 1, 20, 14, 45, 0, DateTimeKind.Utc)
            };

            // Act - simulate the mapping logic from controller
            var dto = MapEntityToDto(entity);

            // Assert
            dto.Should().NotBeNull();
            dto.Id.Should().Be(entity.Id);
            dto.Name.Should().Be(entity.Name);
            dto.ModelSeriesId.Should().Be(entity.ModelSeriesId);
            dto.ModelCapabilitiesId.Should().Be(entity.ModelCapabilitiesId);
            dto.IsActive.Should().Be(entity.IsActive);
            dto.CreatedAt.Should().Be(entity.CreatedAt);
            dto.UpdatedAt.Should().Be(entity.UpdatedAt);
            
            // Capabilities mapping
            dto.Capabilities.Should().NotBeNull();
            dto.Capabilities!.Id.Should().Be(entity.Capabilities.Id);
            dto.Capabilities.SupportsChat.Should().Be(entity.Capabilities.SupportsChat);
            dto.Capabilities.SupportsVision.Should().Be(entity.Capabilities.SupportsVision);
            dto.Capabilities.MaxTokens.Should().Be(entity.Capabilities.MaxTokens);
            dto.Capabilities.MinTokens.Should().Be(entity.Capabilities.MinTokens);
            dto.Capabilities.TokenizerType.Should().Be(entity.Capabilities.TokenizerType);
        }

        [Fact]
        public void Should_Map_Entity_With_Null_Capabilities_To_Dto()
        {
            // Arrange
            var entity = new Model
            {
                Id = 1,
                Name = "test-model",
                ModelSeriesId = 1,
                ModelCapabilitiesId = 1,
                Capabilities = null, // No capabilities loaded
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Act
            var dto = MapEntityToDto(entity);

            // Assert
            dto.Should().NotBeNull();
            dto.Capabilities.Should().BeNull();
            dto.ModelCapabilitiesId.Should().Be(1); // ID still set even if object not loaded
        }

        [Fact]
        public void Should_Map_CreateModelDto_To_Entity()
        {
            // Arrange
            var createDto = new CreateModelDto
            {
                Name = "llama-3.1-405b",
                ModelSeriesId = 3,
                ModelCapabilitiesId = 7,
                IsActive = true
            };

            // Act - simulate controller logic
            var entity = new Model
            {
                Name = createDto.Name,
                ModelSeriesId = createDto.ModelSeriesId,
                ModelCapabilitiesId = createDto.ModelCapabilitiesId,
                IsActive = createDto.IsActive ?? true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Assert
            entity.Name.Should().Be(createDto.Name);
            entity.ModelSeriesId.Should().Be(createDto.ModelSeriesId);
            entity.ModelCapabilitiesId.Should().Be(createDto.ModelCapabilitiesId);
            entity.IsActive.Should().BeTrue();
        }

        [Fact]
        public void Should_Apply_UpdateModelDto_To_Entity_With_Partial_Updates()
        {
            // Arrange
            var existingEntity = new Model
            {
                Id = 42,
                Name = "original-name",
                ModelSeriesId = 1,
                ModelCapabilitiesId = 5,
                IsActive = true,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            };

            var updateDto = new UpdateModelDto
            {
                Id = 42,
                Name = "updated-name",  // Update name
                ModelSeriesId = null,   // Don't update series
                ModelCapabilitiesId = 10, // Update capabilities
                IsActive = null         // Don't update status
            };

            // Act - simulate controller update logic
            if (!string.IsNullOrEmpty(updateDto.Name))
                existingEntity.Name = updateDto.Name;
            if (updateDto.ModelSeriesId.HasValue)
                existingEntity.ModelSeriesId = updateDto.ModelSeriesId.Value;
            if (updateDto.ModelCapabilitiesId.HasValue)
                existingEntity.ModelCapabilitiesId = updateDto.ModelCapabilitiesId.Value;
            if (updateDto.IsActive.HasValue)
                existingEntity.IsActive = updateDto.IsActive.Value;
            existingEntity.UpdatedAt = DateTime.UtcNow;

            // Assert
            existingEntity.Name.Should().Be("updated-name");
            existingEntity.ModelSeriesId.Should().Be(1); // Unchanged
            existingEntity.ModelCapabilitiesId.Should().Be(10); // Updated
            existingEntity.IsActive.Should().BeTrue(); // Unchanged
        }

        [Fact]
        public void Should_Map_Entity_To_ModelWithProviderIdDto()
        {
            // Arrange
            var entity = new Model
            {
                Id = 99,
                Name = "claude-3-opus",
                ModelSeriesId = 2,
                ModelCapabilitiesId = 8,
                Capabilities = new ConduitLLM.Configuration.Entities.ModelCapabilities
                {
                    Id = 8,
                    SupportsChat = true,
                    MaxTokens = 200000
                },
                Identifiers = new List<ModelIdentifier>
                {
                    new ModelIdentifier
                    {
                        Id = 1,
                        ModelId = 99,
                        Provider = "anthropic",
                        Identifier = "claude-3-opus-20240229",
                        IsPrimary = true
                    },
                    new ModelIdentifier
                    {
                        Id = 2,
                        ModelId = 99,
                        Provider = "azure",
                        Identifier = "my-claude-deployment",
                        IsPrimary = false
                    }
                },
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var provider = "anthropic";

            // Act - simulate controller logic for provider-specific DTO
            var providerIdentifier = entity.Identifiers?.FirstOrDefault(i => 
                string.Equals(i.Provider, provider, StringComparison.OrdinalIgnoreCase))?.Identifier 
                ?? entity.Name;

            var dto = new ModelWithProviderIdDto
            {
                Id = entity.Id,
                Name = entity.Name,
                ModelSeriesId = entity.ModelSeriesId,
                ModelCapabilitiesId = entity.ModelCapabilitiesId,
                ProviderModelId = providerIdentifier,
                Capabilities = entity.Capabilities != null ? MapCapabilitiesToDto(entity.Capabilities) : null,
                IsActive = entity.IsActive,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt
            };

            // Assert
            dto.Should().NotBeNull();
            dto.ProviderModelId.Should().Be("claude-3-opus-20240229");
            dto.Name.Should().Be("claude-3-opus");
        }

        [Fact]
        public void Should_Use_Model_Name_As_Fallback_For_ProviderModelId()
        {
            // Arrange
            var entity = new Model
            {
                Id = 1,
                Name = "generic-model",
                ModelSeriesId = 1,
                ModelCapabilitiesId = 1,
                Identifiers = new List<ModelIdentifier>(), // No identifiers
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var provider = "unknown-provider";

            // Act
            var providerIdentifier = entity.Identifiers?.FirstOrDefault(i => 
                string.Equals(i.Provider, provider, StringComparison.OrdinalIgnoreCase))?.Identifier 
                ?? entity.Name; // Fallback to name

            // Assert
            providerIdentifier.Should().Be("generic-model");
        }

        // Helper methods that mirror controller mapping logic
        private static ModelDto MapEntityToDto(Model entity)
        {
            return new ModelDto
            {
                Id = entity.Id,
                Name = entity.Name,
                ModelSeriesId = entity.ModelSeriesId,
                ModelCapabilitiesId = entity.ModelCapabilitiesId,
                Capabilities = entity.Capabilities != null ? MapCapabilitiesToDto(entity.Capabilities) : null,
                IsActive = entity.IsActive,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt
            };
        }

        private static ModelCapabilitiesDto MapCapabilitiesToDto(ConduitLLM.Configuration.Entities.ModelCapabilities capabilities)
        {
            return new ModelCapabilitiesDto
            {
                Id = capabilities.Id,
                SupportsChat = capabilities.SupportsChat,
                SupportsVision = capabilities.SupportsVision,
                SupportsFunctionCalling = capabilities.SupportsFunctionCalling,
                SupportsStreaming = capabilities.SupportsStreaming,
                SupportsAudioTranscription = capabilities.SupportsAudioTranscription,
                SupportsTextToSpeech = capabilities.SupportsTextToSpeech,
                SupportsRealtimeAudio = capabilities.SupportsRealtimeAudio,
                SupportsImageGeneration = capabilities.SupportsImageGeneration,
                SupportsVideoGeneration = capabilities.SupportsVideoGeneration,
                SupportsEmbeddings = capabilities.SupportsEmbeddings,
                MaxTokens = capabilities.MaxTokens,
                MinTokens = capabilities.MinTokens,
                TokenizerType = capabilities.TokenizerType,
                SupportedVoices = capabilities.SupportedVoices,
                SupportedLanguages = capabilities.SupportedLanguages,
                SupportedFormats = capabilities.SupportedFormats
            };
        }

    }
}