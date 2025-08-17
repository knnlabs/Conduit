    /// <summary>
    /// Enum representing the type of model (e.g., Text, Image, etc.)
    /// </summary>
    public enum ModelType
    {
        Text,           // Language models (GPT, Claude, LLaMA, Gemini, etc.)
        Image,          // Image generation (DALL-E, Stable Diffusion, Midjourney)
        Audio,          // Audio generation/transcription (Whisper, ElevenLabs, TTS)
        Video,          // Video generation (Runway, Pika, Stable Video)
        Embedding      // Text embeddings (text-embedding-ada-002, etc.)
        // Vision,         // Vision/multimodal models (GPT-4V, Claude Vision, Gemini Vision)
        // Moderation,     // Content moderation models
        // Reranking       // Reranking models for search/RAG
    }
