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
                ModelAlias = mapping.ModelAlias,
                ProviderModelId = mapping.ProviderModelId,
                ProviderId = mapping.ProviderId,
                Provider = mapping.Provider?.ToReferenceDto(),
                ModelProviderTypeAssociationId = mapping.ModelProviderTypeAssociationId,
                Priority = 0, // Entity doesn't have Priority
                IsEnabled = mapping.IsEnabled,
                CreatedAt = mapping.CreatedAt,
                UpdatedAt = mapping.UpdatedAt,
                Notes = null, // Entity doesn't have Notes
                Capabilities = mapping.ModelProviderTypeAssociation?.Model != null ? new ModelCapabilitiesDto
                {
                    SupportsVision = mapping.ModelProviderTypeAssociation.Model.SupportsVision,
                    SupportsImageGeneration = mapping.ModelProviderTypeAssociation.Model.SupportsImageGeneration,
                    SupportsVideoGeneration = mapping.ModelProviderTypeAssociation.Model.SupportsVideoGeneration,
                    SupportsEmbeddings = mapping.ModelProviderTypeAssociation.Model.SupportsEmbeddings,
                    SupportsChat = mapping.ModelProviderTypeAssociation.Model.SupportsChat,
                    SupportsFunctionCalling = mapping.ModelProviderTypeAssociation.Model.SupportsFunctionCalling,
                    SupportsStreaming = mapping.ModelProviderTypeAssociation.Model.SupportsStreaming,
                    MaxInputTokens = mapping.ModelProviderTypeAssociation.Model.MaxInputTokens,
                    MaxOutputTokens = mapping.ModelProviderTypeAssociation.Model.MaxOutputTokens
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
            mapping.UpdatedAt = System.DateTime.UtcNow;
            // Note: Priority and Notes are DTO-only properties
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