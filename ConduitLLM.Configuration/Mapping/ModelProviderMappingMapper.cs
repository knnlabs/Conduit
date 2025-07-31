using System;
using System.Collections.Generic;
using System.Linq;

using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Configuration.Mapping
{
    /// <summary>
    /// Mapper for converting between DTO and entity objects for ModelProviderMapping
    /// </summary>
    public static class ModelProviderMappingMapper
    {
        /// <summary>
        /// Converts an entity to a DTO
        /// </summary>
        /// <param name="entity">The entity to convert</param>
        /// <returns>The converted DTO</returns>
        public static ConduitLLM.Configuration.ModelProviderMapping? ToDto(Entities.ModelProviderMapping? entity)
        {
            if (entity == null)
            {
                return null;
            }

            return new ConduitLLM.Configuration.ModelProviderMapping
            {
                ModelAlias = entity.ModelAlias,
                ProviderId = entity.ProviderCredentialId,
                ProviderModelId = entity.ProviderModelName,
                DeploymentName = null, // DeploymentName is not stored in the entity currently
                SupportsVision = entity.SupportsVision,
                SupportsAudioTranscription = entity.SupportsAudioTranscription,
                SupportsTextToSpeech = entity.SupportsTextToSpeech,
                SupportsRealtimeAudio = entity.SupportsRealtimeAudio,
                SupportsImageGeneration = entity.SupportsImageGeneration,
                SupportsVideoGeneration = entity.SupportsVideoGeneration,
                SupportsEmbeddings = entity.SupportsEmbeddings,
                SupportsChat = entity.SupportsChat,
                SupportsFunctionCalling = entity.SupportsFunctionCalling,
                SupportsStreaming = entity.SupportsStreaming,
                TokenizerType = entity.TokenizerType,
                SupportedVoices = entity.SupportedVoices,
                SupportedLanguages = entity.SupportedLanguages,
                SupportedFormats = entity.SupportedFormats,
                IsDefault = entity.IsDefault,
                DefaultCapabilityType = entity.DefaultCapabilityType
            };
        }

        /// <summary>
        /// Converts a DTO to an entity
        /// </summary>
        /// <param name="dto">The DTO to convert</param>
        /// <param name="existingEntity">Optional existing entity to update</param>
        /// <returns>The converted entity</returns>
        public static Entities.ModelProviderMapping? ToEntity(
            ConduitLLM.Configuration.ModelProviderMapping? dto,
            Entities.ModelProviderMapping? existingEntity = null)
        {
            if (dto == null)
            {
                return null;
            }

            var entity = existingEntity ?? new Entities.ModelProviderMapping();

            entity.ModelAlias = dto.ModelAlias;
            entity.ProviderModelName = dto.ProviderModelId;
            entity.SupportsVision = dto.SupportsVision;
            entity.SupportsAudioTranscription = dto.SupportsAudioTranscription;
            entity.SupportsTextToSpeech = dto.SupportsTextToSpeech;
            entity.SupportsRealtimeAudio = dto.SupportsRealtimeAudio;
            entity.SupportsImageGeneration = dto.SupportsImageGeneration;
            entity.SupportsVideoGeneration = dto.SupportsVideoGeneration;
            entity.SupportsEmbeddings = dto.SupportsEmbeddings;
            entity.SupportsChat = dto.SupportsChat;
            entity.SupportsFunctionCalling = dto.SupportsFunctionCalling;
            entity.SupportsStreaming = dto.SupportsStreaming;
            entity.TokenizerType = dto.TokenizerType;
            entity.SupportedVoices = dto.SupportedVoices;
            entity.SupportedLanguages = dto.SupportedLanguages;
            entity.SupportedFormats = dto.SupportedFormats;
            entity.IsDefault = dto.IsDefault;
            entity.DefaultCapabilityType = dto.DefaultCapabilityType;
            // Note: ProviderCredentialId needs to be set separately

            return entity;
        }

        /// <summary>
        /// Converts a list of entities to DTOs
        /// </summary>
        /// <param name="entities">The entities to convert</param>
        /// <returns>The converted DTOs</returns>
        public static List<ConduitLLM.Configuration.ModelProviderMapping> ToDtoList(
            IEnumerable<Entities.ModelProviderMapping>? entities)
        {
            if (entities == null)
            {
                return new List<ConduitLLM.Configuration.ModelProviderMapping>();
            }

            return entities.Select(ToDto)
                .Where(dto => dto != null)
                .Select(dto => dto!) // Non-null assertion after filtering
                .ToList();
        }
    }
}
