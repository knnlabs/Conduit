using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Models;

namespace ConduitLLM.Gateway.Services;

/// <summary>Retrieves effective model metadata from the configured routing database.</summary>
public interface IModelMetadataService
{
    Task<object?> GetModelMetadataAsync(string modelId);
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

    public async Task<object?> GetModelMetadataAsync(string modelId)
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
        return new
        {
            id = modelId,
            canonical_model_id = model.Id,
            canonical_name = model.Name,
            provider = mapping!.Provider?.ProviderType.ToString().ToLowerInvariant(),
            provider_model_id = association!.Identifier,
            description = model.Description,
            model_card_url = model.ModelCardUrl,
            input_modalities = capabilities.InputModalities,
            output_modalities = capabilities.OutputModalities,
            capability_source = capabilities.Source.ToString().ToLowerInvariant(),
            capabilities_last_verified_at = capabilities.LastVerifiedAt,
            capabilities = new
            {
                chat = capabilities.SupportsChat,
                chat_stream = capabilities.SupportsStreaming,
                image_input = capabilities.SupportsImageInput,
                video_input = capabilities.SupportsVideoInput,
                audio_input = capabilities.SupportsAudioInput,
                file_input = capabilities.SupportsFileInput,
                vision = capabilities.SupportsVision,
                video_understanding = capabilities.SupportsVideoUnderstanding,
                image_generation = capabilities.SupportsImageGeneration,
                video_generation = capabilities.SupportsVideoGeneration,
                embeddings = capabilities.SupportsEmbeddings,
                function_calling = capabilities.SupportsFunctionCalling,
                speech_to_text = capabilities.SupportsSpeechToText,
                text_to_speech = capabilities.SupportsTextToSpeech,
                rerank = capabilities.SupportsRerank
            },
            max_input_tokens = association.MaxInputTokens ?? model.MaxInputTokens,
            max_output_tokens = association.MaxOutputTokens ?? model.MaxOutputTokens
        };
    }
}
