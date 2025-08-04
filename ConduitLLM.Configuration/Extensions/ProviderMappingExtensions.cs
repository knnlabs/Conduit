using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Configuration.Extensions
{
    /// <summary>
    /// Extension methods for converting between Provider-related entities and DTOs
    /// </summary>
    public static class ProviderMappingExtensions
    {
        /// <summary>
        /// Converts a Provider entity to a ProviderReferenceDto
        /// </summary>
        public static ProviderReferenceDto ToReferenceDto(this Provider provider)
        {
            return new ProviderReferenceDto
            {
                Id = provider.Id,
                ProviderType = provider.ProviderType,
                DisplayName = provider.ProviderName,
                IsEnabled = provider.IsEnabled
            };
        }

        /// <summary>
        /// Converts a ModelProviderMapping entity to a ModelProviderMappingDto
        /// </summary>
        public static ModelProviderMappingDto ToDto(this ModelProviderMapping mapping)
        {
            return new ModelProviderMappingDto
            {
                Id = mapping.Id,
                ModelId = mapping.ModelAlias,
                ProviderModelId = mapping.ProviderModelId,
                ProviderId = mapping.ProviderId,
                Provider = mapping.Provider?.ToReferenceDto(),
                Priority = 0, // Entity doesn't have Priority
                IsEnabled = mapping.IsEnabled,
                Capabilities = null, // Entity doesn't have this as a string
                MaxContextLength = mapping.MaxContextTokens,
                SupportsVision = mapping.SupportsVision,
                SupportsAudioTranscription = mapping.SupportsAudioTranscription,
                SupportsTextToSpeech = mapping.SupportsTextToSpeech,
                SupportsRealtimeAudio = mapping.SupportsRealtimeAudio,
                SupportsImageGeneration = mapping.SupportsImageGeneration,
                SupportsVideoGeneration = mapping.SupportsVideoGeneration,
                SupportsEmbeddings = mapping.SupportsEmbeddings,
                SupportsChat = mapping.SupportsChat,
                SupportsFunctionCalling = mapping.SupportsFunctionCalling,
                SupportsStreaming = mapping.SupportsStreaming,
                TokenizerType = mapping.TokenizerType,
                SupportedVoices = mapping.SupportedVoices,
                SupportedLanguages = mapping.SupportedLanguages,
                SupportedFormats = mapping.SupportedFormats,
                IsDefault = mapping.IsDefault,
                DefaultCapabilityType = mapping.DefaultCapabilityType,
                CreatedAt = mapping.CreatedAt,
                UpdatedAt = mapping.UpdatedAt,
                Notes = null // Entity doesn't have Notes
            };
        }

        /// <summary>
        /// Updates a ModelProviderMapping entity from a ModelProviderMappingDto
        /// </summary>
        public static void UpdateFromDto(this ModelProviderMapping mapping, ModelProviderMappingDto dto)
        {
            mapping.ModelAlias = dto.ModelId;
            mapping.ProviderModelId = dto.ProviderModelId;
            mapping.ProviderId = dto.ProviderId;
            // Entity doesn't have Priority property
            mapping.IsEnabled = dto.IsEnabled;
            // Entity doesn't have Capabilities as a single string
            mapping.MaxContextTokens = dto.MaxContextLength;
            mapping.SupportsVision = dto.SupportsVision;
            mapping.SupportsAudioTranscription = dto.SupportsAudioTranscription;
            mapping.SupportsTextToSpeech = dto.SupportsTextToSpeech;
            mapping.SupportsRealtimeAudio = dto.SupportsRealtimeAudio;
            mapping.SupportsImageGeneration = dto.SupportsImageGeneration;
            mapping.SupportsVideoGeneration = dto.SupportsVideoGeneration;
            mapping.SupportsEmbeddings = dto.SupportsEmbeddings;
            mapping.SupportsChat = dto.SupportsChat;
            mapping.SupportsFunctionCalling = dto.SupportsFunctionCalling;
            mapping.SupportsStreaming = dto.SupportsStreaming;
            mapping.TokenizerType = dto.TokenizerType;
            mapping.SupportedVoices = dto.SupportedVoices;
            mapping.SupportedLanguages = dto.SupportedLanguages;
            mapping.SupportedFormats = dto.SupportedFormats;
            mapping.IsDefault = dto.IsDefault;
            mapping.DefaultCapabilityType = dto.DefaultCapabilityType;
            mapping.UpdatedAt = System.DateTime.UtcNow;
            // Entity doesn't have Notes property
        }

        /// <summary>
        /// Creates a new ModelProviderMapping entity from a ModelProviderMappingDto
        /// </summary>
        public static ModelProviderMapping ToEntity(this ModelProviderMappingDto dto)
        {
            var mapping = new ModelProviderMapping();
            mapping.UpdateFromDto(dto);
            mapping.Id = 0; // Reset ID for new entities
            mapping.CreatedAt = System.DateTime.UtcNow;
            return mapping;
        }
    }
}