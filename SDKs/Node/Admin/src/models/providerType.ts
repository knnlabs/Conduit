/**
 * Strongly-typed enumeration of supported LLM providers.
 * These numeric values must match the C# ProviderType enum exactly.
 */
export enum ProviderType {
  /** OpenAI provider (GPT models) */
  OpenAI = 1,
  
  /** Anthropic provider (Claude models) */
  Anthropic = 2,
  
  /** Azure OpenAI Service */
  AzureOpenAI = 3,
  
  /** Google Gemini */
  Gemini = 4,
  
  /** Google Vertex AI */
  VertexAI = 5,
  
  /** Cohere */
  Cohere = 6,
  
  /** Mistral AI */
  Mistral = 7,
  
  /** Groq */
  Groq = 8,
  
  /** Ollama (local models) */
  Ollama = 9,
  
  /** Replicate */
  Replicate = 10,
  
  /** Fireworks AI */
  Fireworks = 11,
  
  /** AWS Bedrock */
  Bedrock = 12,
  
  /** Hugging Face */
  HuggingFace = 13,
  
  /** AWS SageMaker */
  SageMaker = 14,
  
  /** OpenRouter */
  OpenRouter = 15,
  
  /** OpenAI-compatible generic provider */
  OpenAICompatible = 16,
  
  /** MiniMax */
  MiniMax = 17,
  
  /** Ultravox */
  Ultravox = 18,
  
  /** ElevenLabs (audio) */
  ElevenLabs = 19,
  
  /** Google Cloud (audio) */
  GoogleCloud = 20,
  
  /** Cerebras (high-performance inference) */
  Cerebras = 21
}