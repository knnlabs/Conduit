using System;

using ConfigDTO = ConduitLLM.Configuration.DTOs;

namespace ConduitLLM.WebUI.DTOs
{
    /// <summary>
    /// Type alias for the ProviderCredentialDto from ConduitLLM.Configuration.DTOs
    /// This exists to maintain backward compatibility while consolidating duplicate definitions.
    /// </summary>
    public class ProviderCredentialDto : ConfigDTO.ProviderCredentialDto
    {
        // All functionality is inherited from the base class

        /// <summary>
        /// Base URL for the provider API (alias for backward compatibility)
        /// </summary>
        public string BaseUrl
        {
            get => ApiBase;
            set => ApiBase = value;
        }
    }
}
