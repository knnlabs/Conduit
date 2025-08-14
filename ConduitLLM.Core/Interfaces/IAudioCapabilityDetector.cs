using System.Collections.Generic;

using ConduitLLM.Core.Models.Audio;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Interface for detecting and validating audio capabilities across different providers and models.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The IAudioCapabilityDetector provides a centralized way to determine which audio features
    /// are supported by different providers and models. This is essential for routing decisions
    /// and graceful feature degradation in multi-provider environments.
    /// </para>
    /// <para>
    /// Key responsibilities include:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Identifying which providers support specific audio operations</description></item>
    /// <item><description>Validating audio format compatibility</description></item>
    /// <item><description>Checking voice availability across providers</description></item>
    /// <item><description>Determining real-time conversation support</description></item>
    /// <item><description>Validating language support for transcription and synthesis</description></item>
    /// </list>
    /// </remarks>
    public interface IAudioCapabilityDetector
    {
        /// <summary>
        /// Determines if a provider supports audio transcription (Speech-to-Text).
        /// </summary>
        /// <param name="providerId">The provider ID from the Provider entity.</param>
        /// <param name="model">Optional specific model to check. If null, checks general provider support.</param>
        /// <returns>True if the provider/model supports transcription, false otherwise.</returns>
        /// <remarks>
        /// This method helps determine routing for transcription requests. Some providers
        /// may support transcription only with specific models (e.g., OpenAI's Whisper models).
        /// </remarks>
        bool SupportsTranscription(int providerId, string? model = null);

        /// <summary>
        /// Determines if a provider supports text-to-speech synthesis.
        /// </summary>
        /// <param name="providerId">The provider ID from the Provider entity.</param>
        /// <param name="model">Optional specific model to check. If null, checks general provider support.</param>
        /// <returns>True if the provider/model supports TTS, false otherwise.</returns>
        /// <remarks>
        /// Useful for routing TTS requests to appropriate providers. Some providers specialize
        /// in TTS (like ElevenLabs) while others offer it as an additional capability.
        /// </remarks>
        bool SupportsTextToSpeech(int providerId, string? model = null);

        /// <summary>
        /// Determines if a provider supports real-time conversational audio.
        /// </summary>
        /// <param name="providerId">The provider ID from the Provider entity.</param>
        /// <param name="model">Optional specific model to check. If null, checks general provider support.</param>
        /// <returns>True if the provider/model supports real-time audio, false otherwise.</returns>
        /// <remarks>
        /// Real-time support is currently limited to specific providers and models.
        /// This method helps identify which providers can handle bidirectional audio streaming.
        /// </remarks>
        bool SupportsRealtime(int providerId, string? model = null);

        /// <summary>
        /// Checks if a specific voice is available for a provider.
        /// </summary>
        /// <param name="providerId">The provider ID from the Provider entity.</param>
        /// <param name="voiceId">The voice identifier to check.</param>
        /// <returns>True if the voice is available, false otherwise.</returns>
        /// <remarks>
        /// Voice IDs are provider-specific. This method validates that a requested voice
        /// exists before attempting to use it for TTS or real-time conversations.
        /// </remarks>
        bool SupportsVoice(int providerId, string voiceId);

        /// <summary>
        /// Gets the audio formats supported by a provider for a specific operation.
        /// </summary>
        /// <param name="providerId">The provider ID from the Provider entity.</param>
        /// <param name="operation">The audio operation type (transcription, tts, realtime).</param>
        /// <returns>An array of supported audio format identifiers.</returns>
        /// <remarks>
        /// <para>
        /// Different providers support different audio formats for input and output.
        /// This method returns format identifiers like "mp3", "wav", "flac", "opus", etc.
        /// </para>
        /// <para>
        /// For transcription, these are input formats. For TTS, these are output formats.
        /// For real-time, separate input/output format queries may be needed.
        /// </para>
        /// </remarks>
        AudioFormat[] GetSupportedFormats(int providerId, AudioOperation operation);

        /// <summary>
        /// Gets the languages supported by a provider for a specific audio operation.
        /// </summary>
        /// <param name="providerId">The provider ID from the Provider entity.</param>
        /// <param name="operation">The audio operation type.</param>
        /// <returns>A collection of ISO 639-1 language codes.</returns>
        /// <remarks>
        /// Returns standard language codes (e.g., "en", "es", "fr", "zh") that the provider
        /// supports for the specified operation. Some providers may support different languages
        /// for transcription vs. synthesis.
        /// </remarks>
        IEnumerable<string> GetSupportedLanguages(int providerId, AudioOperation operation);

        /// <summary>
        /// Validates that an audio request can be processed by the specified provider.
        /// </summary>
        /// <param name="request">The audio request to validate.</param>
        /// <param name="providerId">The target provider ID.</param>
        /// <param name="errorMessage">Detailed error message if validation fails.</param>
        /// <returns>True if the request is valid for the provider, false otherwise.</returns>
        /// <remarks>
        /// <para>
        /// Performs comprehensive validation including:
        /// </para>
        /// <list type="bullet">
        /// <item><description>Audio format compatibility</description></item>
        /// <item><description>Language support verification</description></item>
        /// <item><description>Voice availability (for TTS/realtime)</description></item>
        /// <item><description>File size and duration limits</description></item>
        /// <item><description>Sample rate compatibility</description></item>
        /// </list>
        /// </remarks>
        bool ValidateAudioRequest(AudioRequestBase request, int providerId, out string errorMessage);

        /// <summary>
        /// Gets a list of all provider IDs that support a specific audio capability.
        /// </summary>
        /// <param name="capability">The audio capability to check.</param>
        /// <returns>A collection of provider IDs that support the capability.</returns>
        /// <remarks>
        /// Useful for discovering which providers can handle specific audio operations,
        /// enabling intelligent routing and fallback strategies.
        /// </remarks>
        IEnumerable<int> GetProvidersWithCapability(AudioCapability capability);

        /// <summary>
        /// Gets detailed capability information for a specific provider.
        /// </summary>
        /// <param name="providerId">The provider ID from the Provider entity.</param>
        /// <returns>Comprehensive capability information for the provider.</returns>
        /// <remarks>
        /// Returns a detailed breakdown of all audio capabilities, supported formats,
        /// languages, voices, and limitations for the specified provider.
        /// </remarks>
        AudioProviderCapabilities GetProviderCapabilities(int providerId);

        /// <summary>
        /// Determines the best provider for a specific audio request based on capabilities and requirements.
        /// </summary>
        /// <param name="request">The audio request with requirements.</param>
        /// <param name="availableProviderIds">List of available provider IDs to choose from.</param>
        /// <returns>The recommended provider ID, or null if none meet the requirements.</returns>
        /// <remarks>
        /// <para>
        /// Analyzes the request requirements and matches them against provider capabilities
        /// to recommend the most suitable provider. Considers factors like:
        /// </para>
        /// <list type="bullet">
        /// <item><description>Format support</description></item>
        /// <item><description>Language availability</description></item>
        /// <item><description>Voice selection (for TTS)</description></item>
        /// <item><description>Quality requirements</description></item>
        /// <item><description>Cost considerations</description></item>
        /// </list>
        /// </remarks>
        int? RecommendProvider(AudioRequestBase request, IEnumerable<int> availableProviderIds);
    }

    /// <summary>
    /// Enumeration of audio operations for capability checking.
    /// </summary>
    public enum AudioOperation
    {
        /// <summary>
        /// Speech-to-text transcription.
        /// </summary>
        Transcription,

        /// <summary>
        /// Text-to-speech synthesis.
        /// </summary>
        TextToSpeech,

        /// <summary>
        /// Real-time conversational audio.
        /// </summary>
        Realtime,

        /// <summary>
        /// Audio translation (transcription with translation).
        /// </summary>
        Translation
    }

    /// <summary>
    /// Enumeration of audio capabilities for provider discovery.
    /// </summary>
    public enum AudioCapability
    {
        /// <summary>
        /// Basic speech-to-text transcription.
        /// </summary>
        BasicTranscription,

        /// <summary>
        /// Transcription with word-level timestamps.
        /// </summary>
        TimestampedTranscription,

        /// <summary>
        /// Basic text-to-speech synthesis.
        /// </summary>
        BasicTTS,

        /// <summary>
        /// TTS with multiple voice options.
        /// </summary>
        MultiVoiceTTS,

        /// <summary>
        /// TTS with emotional control.
        /// </summary>
        EmotionalTTS,

        /// <summary>
        /// Real-time bidirectional audio.
        /// </summary>
        RealtimeConversation,

        /// <summary>
        /// Voice cloning capabilities.
        /// </summary>
        VoiceCloning,

        /// <summary>
        /// SSML (Speech Synthesis Markup Language) support.
        /// </summary>
        SSMLSupport,

        /// <summary>
        /// Streaming audio output.
        /// </summary>
        StreamingAudio,

        /// <summary>
        /// Function calling in real-time conversations.
        /// </summary>
        RealtimeFunctions
    }
}
