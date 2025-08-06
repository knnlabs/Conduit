/**
 * Strongly-typed enumeration of supported LLM providers.
 * These numeric values must match the C# ProviderType enum exactly.
 */
export enum ProviderType {
  /** OpenAI provider (GPT models) */
  OpenAI = 1,
  
  /** Groq */
  Groq = 2,
  
  /** Replicate */
  Replicate = 3,
  
  /** Fireworks AI */
  Fireworks = 4,
  
  /** OpenAI-compatible generic provider */
  OpenAICompatible = 5,
  
  /** MiniMax */
  MiniMax = 6,
  
  /** Ultravox */
  Ultravox = 7,
  
  /** ElevenLabs (audio) */
  ElevenLabs = 8,
  
  /** Cerebras (high-performance inference) */
  Cerebras = 9
}