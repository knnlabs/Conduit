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
        /// Anthropic provider (Claude models)
        /// </summary>
        Anthropic = 2,

        /// <summary>
        /// Azure OpenAI Service
        /// </summary>
        AzureOpenAI = 3,

        /// <summary>
        /// Google Gemini
        /// </summary>
        Gemini = 4,

        /// <summary>
        /// Google Vertex AI
        /// </summary>
        VertexAI = 5,

        /// <summary>
        /// Cohere
        /// </summary>
        Cohere = 6,

        /// <summary>
        /// Mistral AI
        /// </summary>
        Mistral = 7,

        /// <summary>
        /// Groq
        /// </summary>
        Groq = 8,

        /// <summary>
        /// Ollama (local models)
        /// </summary>
        Ollama = 9,

        /// <summary>
        /// Replicate
        /// </summary>
        Replicate = 10,

        /// <summary>
        /// Fireworks AI
        /// </summary>
        Fireworks = 11,

        /// <summary>
        /// AWS Bedrock
        /// </summary>
        Bedrock = 12,

        /// <summary>
        /// Hugging Face
        /// </summary>
        HuggingFace = 13,

        /// <summary>
        /// AWS SageMaker
        /// </summary>
        SageMaker = 14,

        /// <summary>
        /// OpenRouter
        /// </summary>
        OpenRouter = 15,

        /// <summary>
        /// OpenAI-compatible generic provider
        /// </summary>
        OpenAICompatible = 16,

        /// <summary>
        /// MiniMax
        /// </summary>
        MiniMax = 17,

        /// <summary>
        /// Ultravox
        /// </summary>
        Ultravox = 18,

        /// <summary>
        /// ElevenLabs (audio)
        /// </summary>
        ElevenLabs = 19,

        /// <summary>
        /// Google Cloud (audio)
        /// </summary>
        GoogleCloud = 20,

        /// <summary>
        /// Cerebras (high-performance inference)
        /// </summary>
        Cerebras = 21,

        /// <summary>
        /// AWS Transcribe (speech-to-text)
        /// </summary>
        AWSTranscribe = 22
    }
}