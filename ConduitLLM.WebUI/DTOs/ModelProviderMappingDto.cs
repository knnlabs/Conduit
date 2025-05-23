using System;
using ConfigDTO = ConduitLLM.Configuration.DTOs;

namespace ConduitLLM.WebUI.DTOs
{
    /// <summary>
    /// Type alias for the ModelProviderMappingDto from ConduitLLM.Configuration.DTOs
    /// This exists to maintain backward compatibility while consolidating duplicate definitions.
    /// </summary>
    public class ModelProviderMappingDto : ConfigDTO.ModelProviderMappingDto
    {
        // All functionality is inherited from the base class
        
        /// <summary>
        /// The model alias used in client requests (for backward compatibility)
        /// </summary>
        public string ModelAlias
        {
            get => ModelId;
            set => ModelId = value;
        }
        
        /// <summary>
        /// The provider-specific model name (for backward compatibility)
        /// </summary>
        public string ProviderModelName
        {
            get => ProviderModelId;
            set => ProviderModelId = value;
        }
        
        /// <summary>
        /// The provider credential ID (for backward compatibility)
        /// </summary>
        public int ProviderCredentialId
        {
            get => int.TryParse(ProviderId, out int id) ? id : 0;
            set => ProviderId = value.ToString();
        }
        
        /// <summary>
        /// The maximum number of tokens the model can handle (for backward compatibility)
        /// </summary>
        public int? MaxContextTokens
        {
            get => MaxContextLength;
            set => MaxContextLength = value;
        }
        
        /// <summary>
        /// The provider name (for UI display)
        /// </summary>
        public string ProviderName { get; set; } = string.Empty;
        
        /// <summary>
        /// The provider-specific deployment name (for backward compatibility)
        /// </summary>
        public string? ProviderDeploymentName { get; set; }
    }
}