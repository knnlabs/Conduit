using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.Configuration.DTOs;

namespace ConduitLLM.WebUI.Services;

/// <summary>
/// Service for validating model capability configurations and providing recommendations.
/// </summary>
public class ModelCapabilityValidationService : IModelCapabilityValidationService
{
    private readonly IAdminApiClient _adminApiClient;
    private readonly IConduitApiClient _conduitApiClient;
    private readonly ILogger<ModelCapabilityValidationService> _logger;

    /// <summary>
    /// Initializes a new instance of the ModelCapabilityValidationService class.
    /// </summary>
    public ModelCapabilityValidationService(
        IAdminApiClient adminApiClient,
        IConduitApiClient conduitApiClient,
        ILogger<ModelCapabilityValidationService> logger)
    {
        _adminApiClient = adminApiClient ?? throw new ArgumentNullException(nameof(adminApiClient));
        _conduitApiClient = conduitApiClient ?? throw new ArgumentNullException(nameof(conduitApiClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<ModelCapabilityValidationResult> ValidateModelCapabilitiesAsync(int mappingId)
    {
        try
        {
            var mapping = await _adminApiClient.GetModelProviderMappingByIdAsync(mappingId);
            if (mapping == null)
            {
                return new ModelCapabilityValidationResult
                {
                    IsValid = false,
                    Errors = new[] { "Model mapping not found" }
                };
            }

            return await ValidateModelCapabilitiesAsync(mapping);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating model capabilities for mapping ID: {MappingId}", mappingId);
            return new ModelCapabilityValidationResult
            {
                IsValid = false,
                Errors = new[] { "An error occurred while validating model capabilities" }
            };
        }
    }

    /// <inheritdoc />
    public async Task<ModelCapabilityValidationResult> ValidateModelCapabilitiesAsync(ModelProviderMappingDto mapping)
    {
        var result = new ModelCapabilityValidationResult
        {
            ModelId = mapping.ModelId,
            IsValid = true,
            Errors = new List<string>(),
            Warnings = new List<string>(),
            Recommendations = new List<string>()
        };

        try
        {
            // Test each declared capability against Discovery API
            await ValidateVisionCapability(mapping, result);
            await ValidateImageGenerationCapability(mapping, result);
            await ValidateAudioCapabilities(mapping, result);

            // Check for conflicting capability combinations
            ValidateCapabilityCombinations(mapping, result);

            // Provide recommendations for improvement
            await GenerateRecommendations(mapping, result);

            result.IsValid = !result.Errors.Any();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating capabilities for model: {Model}", mapping.ModelId);
            result.Errors.Add("An error occurred during capability validation");
            result.IsValid = false;
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<ModelCapabilityValidationResult>> ValidateAllModelCapabilitiesAsync()
    {
        try
        {
            var mappings = await _adminApiClient.GetAllModelProviderMappingsAsync();
            var results = new List<ModelCapabilityValidationResult>();

            foreach (var mapping in mappings ?? Enumerable.Empty<ModelProviderMappingDto>())
            {
                if (mapping.IsEnabled)
                {
                    var result = await ValidateModelCapabilitiesAsync(mapping);
                    results.Add(result);
                }
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating all model capabilities");
            return Enumerable.Empty<ModelCapabilityValidationResult>();
        }
    }

    private async Task ValidateVisionCapability(ModelProviderMappingDto mapping, ModelCapabilityValidationResult result)
    {
        if (mapping.SupportsVision)
        {
            try
            {
                var actuallySupportsVision = await _conduitApiClient.TestModelCapabilityAsync(mapping.ModelId, "Vision");
                if (!actuallySupportsVision)
                {
                    result.Errors.Add($"Model {mapping.ModelId} is configured to support vision but capability test failed");
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not test vision capability for model: {Model}", mapping.ModelId);
                result.Warnings.Add($"Could not verify vision capability for model {mapping.ModelId}");
            }
        }
    }

    private async Task ValidateImageGenerationCapability(ModelProviderMappingDto mapping, ModelCapabilityValidationResult result)
    {
        if (mapping.SupportsImageGeneration)
        {
            try
            {
                var actuallySupportsImageGen = await _conduitApiClient.TestModelCapabilityAsync(mapping.ModelId, "ImageGeneration");
                if (!actuallySupportsImageGen)
                {
                    result.Errors.Add($"Model {mapping.ModelId} is configured to support image generation but capability test failed");
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not test image generation capability for model: {Model}", mapping.ModelId);
                result.Warnings.Add($"Could not verify image generation capability for model {mapping.ModelId}");
            }
        }
    }

    private Task ValidateAudioCapabilities(ModelProviderMappingDto mapping, ModelCapabilityValidationResult result)
    {
        // Test audio transcription
        if (mapping.SupportsAudioTranscription)
        {
            try
            {
                // Note: Add audio capability testing when Discovery API supports it
                _logger.LogDebug("Audio transcription capability configured for model: {Model}", mapping.ModelId);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not test audio transcription capability for model: {Model}", mapping.ModelId);
            }
        }

        // Test TTS
        if (mapping.SupportsTextToSpeech)
        {
            try
            {
                // Note: Add TTS capability testing when Discovery API supports it
                _logger.LogDebug("Text-to-speech capability configured for model: {Model}", mapping.ModelId);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not test TTS capability for model: {Model}", mapping.ModelId);
            }
        }
        
        return Task.CompletedTask;
    }

    private static void ValidateCapabilityCombinations(ModelProviderMappingDto mapping, ModelCapabilityValidationResult result)
    {
        // Check for logical conflicts
        if (mapping.SupportsImageGeneration && mapping.SupportsVision)
        {
            result.Warnings.Add($"Model {mapping.ModelId} is configured for both image generation and vision - verify this is correct");
        }

        // Check for incomplete configurations
        if (mapping.SupportsTextToSpeech && string.IsNullOrEmpty(mapping.SupportedVoices))
        {
            result.Warnings.Add($"Model {mapping.ModelId} supports TTS but has no configured voices");
        }

        if (mapping.SupportsAudioTranscription && string.IsNullOrEmpty(mapping.SupportedLanguages))
        {
            result.Warnings.Add($"Model {mapping.ModelId} supports audio transcription but has no configured languages");
        }
    }

    private async Task GenerateRecommendations(ModelProviderMappingDto mapping, ModelCapabilityValidationResult result)
    {
        try
        {
            // Test for undeclared capabilities
            var capabilitiesToTest = new[]
            {
                ("Vision", mapping.SupportsVision),
                ("ImageGeneration", mapping.SupportsImageGeneration),
                ("Chat", true), // Most models should support chat
                ("ChatStream", true) // Most models should support streaming
            };

            foreach (var (capability, isDeclared) in capabilitiesToTest)
            {
                if (!isDeclared)
                {
                    try
                    {
                        var actuallySupports = await _conduitApiClient.TestModelCapabilityAsync(mapping.ModelId, capability);
                        if (actuallySupports)
                        {
                            result.Recommendations.Add($"Model {mapping.ModelId} appears to support {capability} but it's not configured - consider enabling this capability");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Could not test {Capability} for model: {Model}", capability, mapping.ModelId);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error generating recommendations for model: {Model}", mapping.ModelId);
        }
    }
}