using ConduitLLM.Configuration.DTOs;

namespace ConduitLLM.WebUI.Interfaces;

/// <summary>
/// Service interface for validating model capability configurations.
/// </summary>
public interface IModelCapabilityValidationService
{
    /// <summary>
    /// Validates the capabilities configured for a specific model mapping.
    /// </summary>
    /// <param name="mappingId">The ID of the model mapping to validate.</param>
    /// <returns>The validation result containing errors, warnings, and recommendations.</returns>
    Task<ModelCapabilityValidationResult> ValidateModelCapabilitiesAsync(int mappingId);

    /// <summary>
    /// Validates the capabilities configured for a specific model mapping.
    /// </summary>
    /// <param name="mapping">The model mapping to validate.</param>
    /// <returns>The validation result containing errors, warnings, and recommendations.</returns>
    Task<ModelCapabilityValidationResult> ValidateModelCapabilitiesAsync(ModelProviderMappingDto mapping);

    /// <summary>
    /// Validates the capabilities for all enabled model mappings.
    /// </summary>
    /// <returns>A collection of validation results for all models.</returns>
    Task<IEnumerable<ModelCapabilityValidationResult>> ValidateAllModelCapabilitiesAsync();
}

/// <summary>
/// Result of model capability validation.
/// </summary>
public class ModelCapabilityValidationResult
{
    /// <summary>
    /// The model ID that was validated.
    /// </summary>
    public string ModelId { get; set; } = "";

    /// <summary>
    /// Whether the model configuration is valid.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Critical errors that prevent the model from working correctly.
    /// </summary>
    public ICollection<string> Errors { get; set; } = new List<string>();

    /// <summary>
    /// Warnings about potential issues or suboptimal configurations.
    /// </summary>
    public ICollection<string> Warnings { get; set; } = new List<string>();

    /// <summary>
    /// Recommendations for improving the model configuration.
    /// </summary>
    public ICollection<string> Recommendations { get; set; } = new List<string>();

    /// <summary>
    /// Gets a summary of validation issues.
    /// </summary>
    public string Summary => 
        $"Errors: {Errors.Count}, Warnings: {Warnings.Count}, Recommendations: {Recommendations.Count}";
}