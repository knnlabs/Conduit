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
        public static ModelProviderMapping? ToDto(Entities.ModelProviderMapping? entity)
        {
            if (entity == null)
            {
                return null;
            }

            return new ModelProviderMapping
            {
                ModelAlias = entity.ModelAlias,
                ProviderName = entity.ProviderCredential?.ProviderName ?? string.Empty,
                ProviderModelId = entity.ProviderModelName
            };
        }

        /// <summary>
        /// Converts a DTO to an entity
        /// </summary>
        /// <param name="dto">The DTO to convert</param>
        /// <param name="existingEntity">Optional existing entity to update</param>
        /// <returns>The converted entity</returns>
        public static Entities.ModelProviderMapping? ToEntity(ModelProviderMapping? dto, Entities.ModelProviderMapping? existingEntity = null)
        {
            if (dto == null)
            {
                return null;
            }

            var entity = existingEntity ?? new Entities.ModelProviderMapping();
            
            entity.ModelAlias = dto.ModelAlias;
            entity.ProviderModelName = dto.ProviderModelId;
            // Note: ProviderCredentialId needs to be set separately
            
            return entity;
        }

        /// <summary>
        /// Converts a list of entities to DTOs
        /// </summary>
        /// <param name="entities">The entities to convert</param>
        /// <returns>The converted DTOs</returns>
        public static List<ModelProviderMapping> ToDtoList(IEnumerable<Entities.ModelProviderMapping>? entities)
        {
            if (entities == null)
            {
                return new List<ModelProviderMapping>();
            }

            return entities.Select(ToDto)
                .Where(dto => dto != null)
                .Select(dto => dto!) // Non-null assertion after filtering
                .ToList();
        }
    }
}