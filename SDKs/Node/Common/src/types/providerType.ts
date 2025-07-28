/**
 * Strongly-typed enumeration of supported LLM providers.
 * These numeric values must match the C# ProviderType enum exactly.
 * @see https://github.com/knnlabs/Conduit/blob/main/ConduitLLM.Core/Enums/ProviderType.cs
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

/**
 * Type guard to check if a value is a valid ProviderType
 */
export function isProviderType(value: unknown): value is ProviderType {
  return typeof value === 'number' && 
         value >= ProviderType.OpenAI && 
         value <= ProviderType.Cerebras;
}

/**
 * Get the display name for a provider type
 */
export function getProviderDisplayName(provider: ProviderType): string {
  const names: Record<ProviderType, string> = {
    [ProviderType.OpenAI]: 'OpenAI',
    [ProviderType.Anthropic]: 'Anthropic',
    [ProviderType.AzureOpenAI]: 'Azure OpenAI',
    [ProviderType.Gemini]: 'Google Gemini',
    [ProviderType.VertexAI]: 'Google Vertex AI',
    [ProviderType.Cohere]: 'Cohere',
    [ProviderType.Mistral]: 'Mistral AI',
    [ProviderType.Groq]: 'Groq',
    [ProviderType.Ollama]: 'Ollama',
    [ProviderType.Replicate]: 'Replicate',
    [ProviderType.Fireworks]: 'Fireworks AI',
    [ProviderType.Bedrock]: 'AWS Bedrock',
    [ProviderType.HuggingFace]: 'Hugging Face',
    [ProviderType.SageMaker]: 'AWS SageMaker',
    [ProviderType.OpenRouter]: 'OpenRouter',
    [ProviderType.OpenAICompatible]: 'OpenAI Compatible',
    [ProviderType.MiniMax]: 'MiniMax',
    [ProviderType.Ultravox]: 'Ultravox',
    [ProviderType.ElevenLabs]: 'ElevenLabs',
    [ProviderType.GoogleCloud]: 'Google Cloud',
    [ProviderType.Cerebras]: 'Cerebras'
  };
  
  return names[provider] || 'Unknown Provider';
}