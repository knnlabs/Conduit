using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.Configuration.DTOs
{
    /// <summary>
    /// Request DTO for bulk model mapping creation
    /// </summary>
    public class BulkModelMappingRequest
    {
        /// <summary>
        /// Collection of model mappings to create
        /// </summary>
        [Required]
        public List<CreateModelProviderMappingDto> Mappings { get; set; } = new();

        /// <summary>
        /// Whether to replace existing mappings with the same model ID
        /// </summary>
        public bool ReplaceExisting { get; set; } = false;

        /// <summary>
        /// Whether to validate provider model existence before creation
        /// </summary>
        public bool ValidateProviderModels { get; set; } = true;
    }

    /// <summary>
    /// DTO for creating a single model provider mapping
    /// </summary>
    public class CreateModelProviderMappingDto
    {
        /// <summary>
        /// The model alias used in client requests
        /// </summary>
        [Required(ErrorMessage = "Model Alias is required")]
        public string ModelAlias { get; set; } = string.Empty;

        /// <summary>
        /// The provider-specific model identifier
        /// </summary>
        [Required(ErrorMessage = "Provider Model ID is required")]
        public string ProviderModelId { get; set; } = string.Empty;

        /// <summary>
        /// The provider ID
        /// </summary>
        [Required(ErrorMessage = "Provider ID is required")]
        public int ProviderId { get; set; }

        /// <summary>
        /// The ID of the ModelProviderTypeAssociation entity.
        /// Links this mapping to provider-specific model metadata including variations, quality scores, and costs.
        /// </summary>
        [Required(ErrorMessage = "Model Provider Type Association is required")]
        public int ModelProviderTypeAssociationId { get; set; }

        /// <summary>
        /// The priority of this mapping (lower values have higher priority)
        /// </summary>
        public int Priority { get; set; }

        [Range(0.1, 2.0)]
        public decimal Weight { get; set; } = 1.0m;

        /// <summary>
        /// Whether this mapping is currently enabled
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Optional notes or description for this mapping
        /// </summary>
        public string? Notes { get; set; }

        /// <summary>
        /// Optional provider-specific request options as a JSON object.
        /// </summary>
        public string? ProviderOptions { get; set; }
    }

    /// <summary>
    /// Response DTO for bulk model mapping creation
    /// </summary>
    public class BulkModelMappingResponse
    {
        /// <summary>
        /// Successfully created mappings
        /// </summary>
        public List<ModelProviderMappingDto> Created { get; set; } = new();

        /// <summary>
        /// Updated mappings (when ReplaceExisting is true)
        /// </summary>
        public List<ModelProviderMappingDto> Updated { get; set; } = new();

        /// <summary>
        /// Failed mapping attempts
        /// </summary>
        public List<BulkMappingError> Failed { get; set; } = new();

        /// <summary>
        /// Total number of mappings processed
        /// </summary>
        public int TotalProcessed { get; set; }

        /// <summary>
        /// Number of successful operations
        /// </summary>
        public int SuccessCount => Created.Count + Updated.Count;

        /// <summary>
        /// Number of failed operations
        /// </summary>
        public int FailureCount => Failed.Count;

        /// <summary>
        /// Whether the bulk operation was completely successful
        /// </summary>
        public bool IsSuccess => Failed.Count == 0;
    }

    /// <summary>
    /// Details about a failed mapping creation
    /// </summary>
    public class BulkMappingError
    {
        /// <summary>
        /// Index of the failed mapping in the original request
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// The mapping that failed to be created
        /// </summary>
        public CreateModelProviderMappingDto Mapping { get; set; } = new();

        /// <summary>
        /// Error message describing the failure
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// Detailed error information
        /// </summary>
        public string? Details { get; set; }

        /// <summary>
        /// Error category for UI handling
        /// </summary>
        public BulkMappingErrorType ErrorType { get; set; }
    }

    /// <summary>
    /// Categories of bulk mapping errors
    /// </summary>
    public enum BulkMappingErrorType
    {
        /// <summary>
        /// Validation error in the input data
        /// </summary>
        Validation,

        /// <summary>
        /// Duplicate model ID conflict
        /// </summary>
        Duplicate,

        /// <summary>
        /// Provider model does not exist
        /// </summary>
        ProviderModelNotFound,

        /// <summary>
        /// Database or system error
        /// </summary>
        SystemError,

        /// <summary>
        /// Provider not found or unavailable
        /// </summary>
        ProviderNotFound
    }
}
