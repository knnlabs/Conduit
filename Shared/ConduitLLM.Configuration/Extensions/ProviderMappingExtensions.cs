using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Models;

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
            var association = mapping.ModelProviderTypeAssociation;
            var capabilities = association?.Model is null
                ? null
                : ModelCapabilityResolver.Resolve(association.Model, association);

            return new ModelProviderMappingDto
            {
                Id = mapping.Id,
                ModelAlias = mapping.ModelAlias,
                ProviderModelId = mapping.ProviderModelId,
                ProviderId = mapping.ProviderId,
                Provider = mapping.Provider?.ToReferenceDto(),
                ModelProviderTypeAssociationId = mapping.ModelProviderTypeAssociationId,
                Priority = mapping.RoutingPriority,
                Weight = mapping.RoutingWeight,
                IsEnabled = mapping.IsEnabled,
                CreatedAt = mapping.CreatedAt,
                UpdatedAt = mapping.UpdatedAt,
                ProviderOptions = mapping.ProviderOptions,
                Capabilities = capabilities is not null ? new ModelCapabilitiesDto
                {
                    InputModalities = capabilities.InputModalities,
                    OutputModalities = capabilities.OutputModalities,
                    CapabilitySource = capabilities.Source,
                    CapabilitiesLastVerifiedAt = capabilities.LastVerifiedAt,
                    SupportsImageInput = capabilities.SupportsImageInput,
                    SupportsVideoInput = capabilities.SupportsVideoInput,
                    SupportsAudioInput = capabilities.SupportsAudioInput,
                    SupportsFileInput = capabilities.SupportsFileInput,
                    SupportsVideoUnderstanding = capabilities.SupportsVideoUnderstanding,
                    SupportsVision = capabilities.SupportsVision,
                    SupportsImageGeneration = capabilities.SupportsImageGeneration,
                    SupportsVideoGeneration = capabilities.SupportsVideoGeneration,
                    SupportsEmbeddings = capabilities.SupportsEmbeddings,
                    SupportsSpeechToText = capabilities.SupportsSpeechToText,
                    SupportsTextToSpeech = capabilities.SupportsTextToSpeech,
                    SupportsRerank = capabilities.SupportsRerank,
                    SupportsChat = capabilities.SupportsChat,
                    SupportsFunctionCalling = capabilities.SupportsFunctionCalling,
                    SupportsStreaming = capabilities.SupportsStreaming,
                    MaxInputTokens = association!.MaxInputTokens ?? association.Model.MaxInputTokens,
                    MaxOutputTokens = association.MaxOutputTokens ?? association.Model.MaxOutputTokens
                } : null
            };
        }

        /// <summary>
        /// Updates a ModelProviderMapping entity from a ModelProviderMappingDto
        /// </summary>
        public static void UpdateFromDto(this ModelProviderMapping mapping, ModelProviderMappingDto dto)
        {
            mapping.ModelAlias = dto.ModelAlias;
            mapping.ProviderModelId = dto.ProviderModelId;
            mapping.ProviderId = dto.ProviderId;
            mapping.ModelProviderTypeAssociationId = dto.ModelProviderTypeAssociationId;
            mapping.IsEnabled = dto.IsEnabled;
            mapping.ProviderOptions = dto.ProviderOptions;
            mapping.RoutingPriority = dto.Priority;
            mapping.RoutingWeight = dto.Weight;
            mapping.UpdatedAt = System.DateTime.UtcNow;
        }

        public static void UpdateFromDto(this ModelProviderMapping mapping, UpdateModelProviderMappingDto dto)
        {
            mapping.ModelAlias = dto.ModelAlias;
            mapping.ProviderModelId = dto.ProviderModelId;
            mapping.ProviderId = dto.ProviderId;
            mapping.ModelProviderTypeAssociationId = dto.ModelProviderTypeAssociationId;
            mapping.IsEnabled = dto.IsEnabled;
            mapping.ProviderOptions = dto.ProviderOptions;
            mapping.RoutingPriority = dto.Priority;
            mapping.RoutingWeight = dto.Weight;
            mapping.UpdatedAt = System.DateTime.UtcNow;
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

        public static ModelProviderMapping ToEntity(this CreateModelProviderMappingDto dto)
        {
            return new ModelProviderMapping
            {
                ModelAlias = dto.ModelAlias,
                ProviderModelId = dto.ProviderModelId,
                ProviderId = dto.ProviderId,
                ModelProviderTypeAssociationId = dto.ModelProviderTypeAssociationId,
                IsEnabled = dto.IsEnabled,
                ProviderOptions = dto.ProviderOptions,
                RoutingPriority = dto.Priority,
                RoutingWeight = dto.Weight,
                CreatedAt = System.DateTime.UtcNow,
                UpdatedAt = System.DateTime.UtcNow
            };
        }
    }
}
