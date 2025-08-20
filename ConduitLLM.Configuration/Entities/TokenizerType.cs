public enum TokenizerType
{
    // OpenAI tokenizers
    Cl100KBase,      // GPT-3.5-turbo, GPT-4, GPT-4-turbo
    P50KBase,        // Older GPT-3 models (text-davinci-002, etc.)
    P50KEdit,        // Edit models (text-davinci-edit-001)
    R50KBase,        // Codex models
    O200KBase,       // GPT-4o, GPT-4o-mini (newest tokenizer)
    
    // Anthropic tokenizers
    Claude,          // Claude 2 and earlier
    Claude3,         // Claude 3 (Haiku, Sonnet, Opus)
    
    // Google tokenizers
    Gemini,          // Gemini models
    PaLM,            // PaLM and PaLM 2 models
    
    // Meta tokenizers
    LLaMA,           // LLaMA 1
    LLaMA2,          // LLaMA 2
    LLaMA3,          // LLaMA 3
    
    // Mistral tokenizers
    Mistral,         // Mistral models
    
    // Cohere tokenizers
    Cohere,          // Command and other Cohere models
    
    // Other provider tokenizers
    Groq,            // Groq-specific if different from base models
    Cerebras,        // Cerebras-specific if different
    MiniMax,         // MiniMax models
    
    // Common open-source tokenizers
    SentencePiece,   // Used by various models
    BPE,             // Byte Pair Encoding (generic)
    WordPiece,       // Used by BERT-style models
    Tiktoken         // OpenAI's open-source tokenizer library
}