using System;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Factory for creating real-time message translators for different providers.
    /// </summary>
    public interface IRealtimeMessageTranslatorFactory
    {
        /// <summary>
        /// Gets a translator for the specified provider.
        /// </summary>
        /// <param name="provider">The provider name (e.g., "OpenAI", "Ultravox", "ElevenLabs").</param>
        /// <returns>The message translator, or null if provider is not supported.</returns>
        IRealtimeMessageTranslator? GetTranslator(string provider);

        /// <summary>
        /// Registers a translator for a provider.
        /// </summary>
        /// <param name="provider">The provider name.</param>
        /// <param name="translator">The translator implementation.</param>
        void RegisterTranslator(string provider, IRealtimeMessageTranslator translator);

        /// <summary>
        /// Checks if a translator is available for a provider.
        /// </summary>
        /// <param name="provider">The provider name.</param>
        /// <returns>True if a translator is registered.</returns>
        bool HasTranslator(string provider);

        /// <summary>
        /// Gets all registered provider names.
        /// </summary>
        /// <returns>Array of provider names.</returns>
        string[] GetRegisteredProviders();
    }
}