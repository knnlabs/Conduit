using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.OpenAI
{
    /// <summary>
    /// OpenAIClient partial class containing utility methods.
    /// </summary>
    public partial class OpenAIClient
    {
        /// <summary>
        /// Gets the default transcription model from configuration.
        /// </summary>
        private string? GetDefaultTranscriptionModel()
        {
            // Check provider-specific override first
            var providerOverride = DefaultModels?.Audio?.ProviderOverrides
                ?.GetValueOrDefault(ProviderName.ToLowerInvariant())?.TranscriptionModel;

            if (!string.IsNullOrWhiteSpace(providerOverride))
                return providerOverride;

            // Check global default
            var globalDefault = DefaultModels?.Audio?.DefaultTranscriptionModel;
            if (!string.IsNullOrWhiteSpace(globalDefault))
                return globalDefault;

            // Use ModelCapabilityService if available
            if (_capabilityService != null)
            {
                try
                {
                    var defaultModel = _capabilityService.GetDefaultModelAsync("openai", "transcription").GetAwaiter().GetResult();
                    if (!string.IsNullOrWhiteSpace(defaultModel))
                        return defaultModel;
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Failed to get default transcription model via ModelCapabilityService");
                }
            }

            // No default found - model must be specified
            return null;
        }

        /// <summary>
        /// Gets the default text-to-speech model from configuration.
        /// </summary>
        private string? GetDefaultTextToSpeechModel()
        {
            // Check provider-specific override first
            var providerOverride = DefaultModels?.Audio?.ProviderOverrides
                ?.GetValueOrDefault(ProviderName.ToLowerInvariant())?.TextToSpeechModel;

            if (!string.IsNullOrWhiteSpace(providerOverride))
                return providerOverride;

            // Check global default
            var globalDefault = DefaultModels?.Audio?.DefaultTextToSpeechModel;
            if (!string.IsNullOrWhiteSpace(globalDefault))
                return globalDefault;

            // Use ModelCapabilityService if available
            if (_capabilityService != null)
            {
                try
                {
                    var defaultModel = _capabilityService.GetDefaultModelAsync("openai", "tts").GetAwaiter().GetResult();
                    if (!string.IsNullOrWhiteSpace(defaultModel))
                        return defaultModel;
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Failed to get default TTS model via ModelCapabilityService");
                }
            }

            // No default found - model must be specified
            return null;
        }

        /// <summary>
        /// Gets the default realtime model from configuration.
        /// </summary>
        private string? GetDefaultRealtimeModel()
        {
            // Check provider-specific override first
            var providerOverride = DefaultModels?.Realtime?.ProviderOverrides
                ?.GetValueOrDefault(ProviderName.ToLowerInvariant());

            if (!string.IsNullOrWhiteSpace(providerOverride))
                return providerOverride;

            // Check global default
            var globalDefault = DefaultModels?.Realtime?.DefaultRealtimeModel;
            if (!string.IsNullOrWhiteSpace(globalDefault))
                return globalDefault;

            // Use ModelCapabilityService if available
            if (_capabilityService != null)
            {
                try
                {
                    var defaultModel = _capabilityService.GetDefaultModelAsync("openai", "realtime").GetAwaiter().GetResult();
                    if (!string.IsNullOrWhiteSpace(defaultModel))
                        return defaultModel;
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Failed to get default realtime model via ModelCapabilityService");
                }
            }

            // No default found - model must be specified
            return null;
        }
    }
}