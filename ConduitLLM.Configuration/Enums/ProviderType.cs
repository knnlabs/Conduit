namespace ConduitLLM.Configuration
{
    /// <summary>
    /// Strongly-typed enumeration of supported LLM providers
    /// This is NOT a unique identifer for providers! 
    /// It's really used for determining which provider to use for a given model.
    /// All other references to this enum are likely suspect and should be removed!
    /// </summary>
    public enum ProviderType
    {
        /// <summary>
        /// OpenAI provider (GPT models)
        /// </summary>
        OpenAI = 1,

        /// <summary>
        /// Groq
        /// </summary>
        Groq = 2,

        /// <summary>
        /// Replicate
        /// </summary>
        Replicate = 3,

        /// <summary>
        /// Fireworks AI
        /// </summary>
        Fireworks = 4,

        /// <summary>
        /// OpenAI-compatible generic provider
        /// </summary>
        OpenAICompatible = 5,

        /// <summary>
        /// MiniMax
        /// </summary>
        MiniMax = 6,

        /// <summary>
        /// Ultravox
        /// </summary>
        Ultravox = 7,

        /// <summary>
        /// ElevenLabs (audio)
        /// </summary>
        ElevenLabs = 8,

        /// <summary>
        /// Cerebras (high-performance inference)
        /// </summary>
        Cerebras = 9,

        /// <summary>
        /// SambaNova Cloud (ultra-fast inference provider)
        /// </summary>
        SambaNova = 10,

        /// <summary>
        /// DeepInfra (OpenAI-compatible LLM inference platform)
        /// </summary>
        DeepInfra = 11
    }
}