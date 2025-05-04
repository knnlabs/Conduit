using System.Text.Json.Serialization;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Providers.InternalModels
{
    /// <summary>
    /// Extended model information with additional provider-specific data.
    /// Extends the core ModelInfo with properties needed by provider implementations.
    /// </summary>
    public class ExtendedModelInfo : ModelInfo
    {
        /// <summary>
        /// Gets or sets the display name of the model.
        /// </summary>
        public string? Name { get; set; }
        
        /// <summary>
        /// Gets or sets the provider name for this model.
        /// </summary>
        public string? Provider { get; set; }
        
        /// <summary>
        /// Gets or sets the capabilities of this model.
        /// </summary>
        public ModelCapabilities? Capabilities { get; set; }
        
        /// <summary>
        /// Gets or sets the token limits for this model.
        /// </summary>
        public ModelTokenLimits? TokenLimits { get; set; }
        
        /// <summary>
        /// Gets or sets the original model alias used in routing.
        /// </summary>
        public string? OriginalModelAlias { get; set; }
        
        /// <summary>
        /// Gets or sets the seed used for this model.
        /// </summary>
        public int? Seed { get; set; }
        
        /// <summary>
        /// Gets or sets the provider model ID (actual model name used by the provider API).
        /// </summary>
        public string? ProviderModelId { get; set; }
        
        /// <summary>
        /// Creates a new instance of the ExtendedModelInfo class.
        /// </summary>
        public ExtendedModelInfo()
        {
            // Set a default value for required property OwnedBy
            OwnedBy = "unknown";
        }
        
        /// <summary>
        /// Helper method to create an ExtendedModelInfo with required fields populated.
        /// </summary>
        /// <param name="id">The model ID (used as model alias)</param>
        /// <param name="provider">The provider name</param>
        /// <param name="providerModelId">The provider-specific model ID</param>
        /// <returns>A new ExtendedModelInfo instance with required fields populated</returns>
        public static ExtendedModelInfo Create(string id, string provider, string providerModelId)
        {
            return new ExtendedModelInfo
            {
                Id = id,
                OwnedBy = provider,
                Provider = provider,
                ProviderModelId = providerModelId
            };
        }
        
        /// <summary>
        /// Adds a display name to the model information.
        /// </summary>
        /// <param name="name">The display name for the model.</param>
        /// <returns>This instance for method chaining.</returns>
        public ExtendedModelInfo WithName(string name)
        {
            Name = name;
            return this;
        }
        
        /// <summary>
        /// Adds capabilities information to the model.
        /// </summary>
        /// <param name="capabilities">The capabilities of the model.</param>
        /// <returns>This instance for method chaining.</returns>
        public ExtendedModelInfo WithCapabilities(ModelCapabilities capabilities)
        {
            Capabilities = capabilities;
            return this;
        }
        
        /// <summary>
        /// Adds token limit information to the model.
        /// </summary>
        /// <param name="tokenLimits">The token limits for the model.</param>
        /// <returns>This instance for method chaining.</returns>
        public ExtendedModelInfo WithTokenLimits(ModelTokenLimits tokenLimits)
        {
            TokenLimits = tokenLimits;
            return this;
        }
    }
    
    /// <summary>
    /// Describes token limits for a model.
    /// </summary>
    public class ModelTokenLimits
    {
        /// <summary>
        /// Gets or sets the maximum context length in tokens.
        /// </summary>
        public int? Context { get; set; }
        
        /// <summary>
        /// Gets or sets the maximum input length in tokens.
        /// </summary>
        public int? Input { get; set; }
        
        /// <summary>
        /// Gets or sets the maximum output length in tokens.
        /// </summary>
        public int? Output { get; set; }
        
        /// <summary>
        /// Gets or sets the maximum input tokens for the model.
        /// </summary>
        /// <remarks>Alternate name for Input property used by some providers.</remarks>
        public int? MaxInputTokens
        {
            get => Input;
            set => Input = value;
        }
        
        /// <summary>
        /// Gets or sets the maximum output tokens for the model.
        /// </summary>
        /// <remarks>Alternate name for Output property used by some providers.</remarks>
        public int? MaxOutputTokens
        {
            get => Output;
            set => Output = value;
        }
        
        /// <summary>
        /// Gets or sets the maximum total tokens for the model.
        /// </summary>
        /// <remarks>Alternate name for Context property used by some providers.</remarks>
        public int? MaxTotalTokens
        {
            get => Context;
            set => Context = value;
        }
    }
}