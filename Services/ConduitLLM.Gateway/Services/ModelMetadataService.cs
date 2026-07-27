using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Models;
using ConduitLLM.Gateway.DTOs;

namespace ConduitLLM.Gateway.Services;

/// <summary>Retrieves effective model metadata from the configured routing database.</summary>
public interface IModelMetadataService
{
    Task<ModelMetadataDto?> GetModelMetadataAsync(string modelId);
}

/// <summary>
/// Resolves metadata for the highest-priority enabled mapping instead of relying on a
/// separately deployed static file that can drift from the active configuration.
/// </summary>
public sealed class ModelMetadataService : IModelMetadataService
{
    private readonly IModelProviderMappingRepository _mappingRepository;
    private readonly ILogger<ModelMetadataService> _logger;

    public ModelMetadataService(
        IModelProviderMappingRepository mappingRepository,
        ILogger<ModelMetadataService> logger)
    {
        _mappingRepository = mappingRepository;
        _logger = logger;
    }

    public async Task<ModelMetadataDto?> GetModelMetadataAsync(string modelId)
    {
        var mappings = await _mappingRepository.GetAllByModelNameAsync(modelId);
        var mapping = mappings.FirstOrDefault(candidate =>
            candidate.IsEnabled &&
            candidate.Provider?.IsEnabled == true &&
            candidate.ModelProviderTypeAssociation?.IsEnabled == true);

        var association = mapping?.ModelProviderTypeAssociation;
        var model = association?.Model;
        if (model is null)
        {
            _logger.LogDebug("No enabled metadata found for model {ModelId}", modelId);
            return null;
        }

        var capabilities = ModelCapabilityResolver.Resolve(model, association);
        return new ModelMetadataDto(
            modelId,
            model.Id,
            model.Name,
            mapping!.Provider?.ProviderType.ToString().ToLowerInvariant(),
            association!.Identifier,
            model.Description,
            model.ModelCardUrl,
            capabilities.InputModalities ?? [],
            capabilities.OutputModalities ?? [],
            capabilities.CapabilitySource.ToString().ToLowerInvariant(),
            capabilities.CapabilitiesLastVerifiedAt,
            new GatewayModelCapabilitiesDto(
                capabilities.SupportsChat,
                capabilities.SupportsStreaming,
                capabilities.SupportsImageInput,
                capabilities.SupportsVideoInput,
                capabilities.SupportsAudioInput,
                capabilities.SupportsFileInput,
                capabilities.SupportsVision,
                capabilities.SupportsVideoUnderstanding,
                capabilities.SupportsImageGeneration,
                capabilities.SupportsVideoGeneration,
                capabilities.SupportsEmbeddings,
                capabilities.SupportsFunctionCalling,
                capabilities.SupportsSpeechToText,
                capabilities.SupportsTextToSpeech,
                capabilities.SupportsRerank,
                PdfInput: capabilities.SupportsFileInput ||
                          mapping.Provider?.ProviderType == ConduitLLM.Configuration.ProviderType.OpenRouter),
            capabilities.MaxInputTokens,
            capabilities.MaxOutputTokens);
    }
}
