using ConduitLLM.Configuration.Entities;
using ConfigDTOs = ConduitLLM.Configuration.DTOs;
using ConfigEntityTypes = ConduitLLM.Configuration.Entities;

namespace ConduitLLM.WebUI.DTOs
{
    /// <summary>
    /// WebUI wrapper for ModelProviderMapping to handle compatibility between different formats
    /// This wrapper allows us to work with multiple ModelProviderMapping types in the codebase
    /// during the transition to standardized DTOs
    /// </summary>
    public class ModelProviderMapping : ConduitLLM.Configuration.ModelProviderMapping
    {
        /// <summary>
        /// Gets or sets the ID of the mapping (compatibility with entity class)
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Creates a new empty instance
        /// </summary>
        public ModelProviderMapping()
        {
            // Required properties must be initialized
            ModelAlias = string.Empty;
            ProviderModelId = string.Empty;
            ProviderName = string.Empty;
        }

        /// <summary>
        /// Creates a new instance from a Configuration DTO
        /// </summary>
        /// <param name="dto">The source DTO</param>
        public ModelProviderMapping(ConfigDTOs.ModelProviderMappingDto dto)
        {
            Id = dto.Id;
            ModelAlias = dto.ModelId;
            ProviderModelId = dto.ProviderModelId;
            ProviderName = dto.ProviderId;
            // Handle other properties as needed
        }

        /// <summary>
        /// Creates a new instance from a Configuration Entity
        /// </summary>
        /// <param name="entity">The source entity</param>
        public ModelProviderMapping(ConfigEntityTypes.ModelProviderMapping entity)
        {
            Id = entity.Id;
            ModelAlias = entity.ModelAlias;
            ProviderModelId = entity.ProviderModelName;
            ProviderName = entity.ProviderCredential?.ProviderName ?? entity.ProviderCredentialId.ToString();
            // Handle other properties as needed
        }

        /// <summary>
        /// Creates a new instance from a Configuration ModelProviderMapping
        /// </summary>
        /// <param name="mapping">The source mapping</param>
        public ModelProviderMapping(ConduitLLM.Configuration.ModelProviderMapping mapping)
        {
            // Copy properties from the base mapping
            ModelAlias = mapping.ModelAlias;
            ProviderModelId = mapping.ProviderModelId;
            ProviderName = mapping.ProviderName;
            // Additional properties like DeploymentName if needed
            if (mapping.GetType().FullName == "ConduitLLM.Configuration.Entities.ModelProviderMapping")
            {
                // Use reflection to get the Id property
                var idProperty = mapping.GetType().GetProperty("Id");
                if (idProperty != null)
                {
                    var value = idProperty.GetValue(mapping);
                    if (value != null)
                    {
                        Id = (int)value;
                    }
                }
            }
        }

        /// <summary>
        /// Converts this instance to a Configuration Entity
        /// </summary>
        /// <returns>The entity representation</returns>
        public ConfigEntityTypes.ModelProviderMapping ToEntity()
        {
            return new ConfigEntityTypes.ModelProviderMapping
            {
                Id = this.Id,
                ModelAlias = this.ModelAlias,
                ProviderModelName = this.ProviderModelId,
                ProviderCredentialId = int.TryParse(this.ProviderName, out int id) ? id : 0,
                IsEnabled = true,
                MaxContextTokens = null,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Converts this instance to a Configuration DTO
        /// </summary>
        /// <returns>The DTO representation</returns>
        public ConfigDTOs.ModelProviderMappingDto ToDto()
        {
            return new ConfigDTOs.ModelProviderMappingDto
            {
                Id = this.Id,
                ModelId = this.ModelAlias,
                ProviderModelId = this.ProviderModelId,
                ProviderId = this.ProviderName,
                IsEnabled = true,
                Priority = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }
    }
}