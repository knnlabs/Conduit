/**
 * Provider-related constants for the WebUI
 * This file serves as the single source of truth for provider types and configurations
 */

// Provider types supported by Conduit
// TODO: These should ideally come from the SDK, but currently they're not exported
// The backend expects these exact string values
export enum ProviderType {
  OpenAI = 'openai',
  Anthropic = 'anthropic',
  Google = 'google',
  Gemini = 'gemini',
  Azure = 'azure',
  AWSBedrock = 'aws-bedrock',
  Bedrock = 'bedrock',
  Cohere = 'cohere',
  MiniMax = 'minimax',
  Replicate = 'replicate',
  HuggingFace = 'huggingface',
  DeepSeek = 'deepseek',
  Perplexity = 'perplexity',
  ElevenLabs = 'elevenlabs',
  Mistral = 'mistral',
  MistralAI = 'mistralai',
  Groq = 'groq',
  VertexAI = 'vertexai',
  Ollama = 'ollama',
  Fireworks = 'fireworks',
  FireworksAI = 'fireworksai',
  SageMaker = 'sagemaker',
  OpenRouter = 'openrouter',
  OpenAICompatible = 'openai-compatible',
  OpenAICompatibleAlt = 'openaicompatible',
  Ultravox = 'ultravox',
  GoogleCloud = 'googlecloud',
  GoogleCloudAlt = 'google-cloud',
  GCP = 'gcp',
  AWS = 'aws',
  AWSTranscribe = 'awstranscribe',
  AWSTranscribeAlt = 'aws-transcribe',
  Cerebras = 'cerebras',
  Custom = 'custom',
}

// Provider display configuration
export const PROVIDER_DISPLAY_NAMES: Record<ProviderType, string> = {
  [ProviderType.OpenAI]: 'OpenAI',
  [ProviderType.Anthropic]: 'Anthropic',
  [ProviderType.Google]: 'Google AI',
  [ProviderType.Gemini]: 'Google Gemini',
  [ProviderType.Azure]: 'Azure OpenAI',
  [ProviderType.AWSBedrock]: 'AWS Bedrock',
  [ProviderType.Bedrock]: 'AWS Bedrock',
  [ProviderType.Cohere]: 'Cohere',
  [ProviderType.MiniMax]: 'MiniMax',
  [ProviderType.Replicate]: 'Replicate',
  [ProviderType.HuggingFace]: 'Hugging Face',
  [ProviderType.DeepSeek]: 'DeepSeek',
  [ProviderType.Perplexity]: 'Perplexity',
  [ProviderType.ElevenLabs]: 'ElevenLabs',
  [ProviderType.Mistral]: 'Mistral AI',
  [ProviderType.MistralAI]: 'Mistral AI',
  [ProviderType.Groq]: 'Groq',
  [ProviderType.VertexAI]: 'Google Vertex AI',
  [ProviderType.Ollama]: 'Ollama',
  [ProviderType.Fireworks]: 'Fireworks AI',
  [ProviderType.FireworksAI]: 'Fireworks AI',
  [ProviderType.SageMaker]: 'AWS SageMaker',
  [ProviderType.OpenRouter]: 'OpenRouter',
  [ProviderType.OpenAICompatible]: 'OpenAI Compatible',
  [ProviderType.OpenAICompatibleAlt]: 'OpenAI Compatible',
  [ProviderType.Ultravox]: 'Ultravox',
  [ProviderType.GoogleCloud]: 'Google Cloud',
  [ProviderType.GoogleCloudAlt]: 'Google Cloud',
  [ProviderType.GCP]: 'Google Cloud',
  [ProviderType.AWS]: 'AWS',
  [ProviderType.AWSTranscribe]: 'AWS Transcribe',
  [ProviderType.AWSTranscribeAlt]: 'AWS Transcribe',
  [ProviderType.Cerebras]: 'Cerebras',
  [ProviderType.Custom]: 'Custom Provider',
};

// Provider categories for grouping in UI
export enum ProviderCategory {
  Chat = 'chat',
  Audio = 'audio',
  Image = 'image',
  Video = 'video',
  Embedding = 'embedding',
  Custom = 'custom',
}

// Map providers to their primary categories
export const PROVIDER_CATEGORIES: Record<ProviderType, ProviderCategory[]> = {
  [ProviderType.OpenAI]: [ProviderCategory.Chat, ProviderCategory.Audio, ProviderCategory.Image, ProviderCategory.Embedding],
  [ProviderType.Anthropic]: [ProviderCategory.Chat],
  [ProviderType.Google]: [ProviderCategory.Chat, ProviderCategory.Embedding],
  [ProviderType.Gemini]: [ProviderCategory.Chat, ProviderCategory.Embedding],
  [ProviderType.Azure]: [ProviderCategory.Chat, ProviderCategory.Embedding],
  [ProviderType.AWSBedrock]: [ProviderCategory.Chat, ProviderCategory.Image],
  [ProviderType.Bedrock]: [ProviderCategory.Chat, ProviderCategory.Image],
  [ProviderType.Cohere]: [ProviderCategory.Chat, ProviderCategory.Embedding],
  [ProviderType.MiniMax]: [ProviderCategory.Chat, ProviderCategory.Audio],
  [ProviderType.Replicate]: [ProviderCategory.Chat, ProviderCategory.Image, ProviderCategory.Video],
  [ProviderType.HuggingFace]: [ProviderCategory.Chat, ProviderCategory.Image, ProviderCategory.Embedding],
  [ProviderType.DeepSeek]: [ProviderCategory.Chat],
  [ProviderType.Perplexity]: [ProviderCategory.Chat],
  [ProviderType.ElevenLabs]: [ProviderCategory.Audio],
  [ProviderType.Mistral]: [ProviderCategory.Chat, ProviderCategory.Embedding],
  [ProviderType.MistralAI]: [ProviderCategory.Chat, ProviderCategory.Embedding],
  [ProviderType.Groq]: [ProviderCategory.Chat],
  [ProviderType.VertexAI]: [ProviderCategory.Chat, ProviderCategory.Embedding],
  [ProviderType.Ollama]: [ProviderCategory.Chat, ProviderCategory.Embedding],
  [ProviderType.Fireworks]: [ProviderCategory.Chat],
  [ProviderType.FireworksAI]: [ProviderCategory.Chat],
  [ProviderType.SageMaker]: [ProviderCategory.Chat, ProviderCategory.Embedding],
  [ProviderType.OpenRouter]: [ProviderCategory.Chat],
  [ProviderType.OpenAICompatible]: [ProviderCategory.Chat, ProviderCategory.Embedding],
  [ProviderType.OpenAICompatibleAlt]: [ProviderCategory.Chat, ProviderCategory.Embedding],
  [ProviderType.Ultravox]: [ProviderCategory.Audio],
  [ProviderType.GoogleCloud]: [ProviderCategory.Audio],
  [ProviderType.GoogleCloudAlt]: [ProviderCategory.Audio],
  [ProviderType.GCP]: [ProviderCategory.Audio],
  [ProviderType.AWS]: [ProviderCategory.Audio],
  [ProviderType.AWSTranscribe]: [ProviderCategory.Audio],
  [ProviderType.AWSTranscribeAlt]: [ProviderCategory.Audio],
  [ProviderType.Cerebras]: [ProviderCategory.Chat],
  [ProviderType.Custom]: [ProviderCategory.Custom],
};

// Provider-specific configuration requirements
export interface ProviderConfigRequirements {
  requiresApiKey: boolean;
  requiresEndpoint: boolean;
  requiresOrganizationId: boolean;
  supportsCustomEndpoint: boolean;
  helpUrl?: string;
  helpText?: string;
}

export const PROVIDER_CONFIG_REQUIREMENTS: Record<ProviderType, ProviderConfigRequirements> = {
  [ProviderType.OpenAI]: {
    requiresApiKey: true,
    requiresEndpoint: false,
    requiresOrganizationId: false, // Optional
    supportsCustomEndpoint: true,
    helpUrl: 'https://platform.openai.com/api-keys',
    helpText: 'Get your API key from platform.openai.com/api-keys',
  },
  [ProviderType.Anthropic]: {
    requiresApiKey: true,
    requiresEndpoint: false,
    requiresOrganizationId: false,
    supportsCustomEndpoint: true,
    helpUrl: 'https://console.anthropic.com/account/keys',
    helpText: 'Get your API key from console.anthropic.com/account/keys',
  },
  [ProviderType.Google]: {
    requiresApiKey: true,
    requiresEndpoint: false,
    requiresOrganizationId: false,
    supportsCustomEndpoint: false,
    helpUrl: 'https://makersuite.google.com/app/apikey',
    helpText: 'Get your API key from makersuite.google.com/app/apikey',
  },
  [ProviderType.Gemini]: {
    requiresApiKey: true,
    requiresEndpoint: false,
    requiresOrganizationId: false,
    supportsCustomEndpoint: false,
    helpUrl: 'https://makersuite.google.com/app/apikey',
    helpText: 'Get your API key from makersuite.google.com/app/apikey',
  },
  [ProviderType.Azure]: {
    requiresApiKey: true,
    requiresEndpoint: true,
    requiresOrganizationId: false,
    supportsCustomEndpoint: true,
    helpText: 'Requires both API key and your Azure OpenAI endpoint URL',
  },
  [ProviderType.AWSBedrock]: {
    requiresApiKey: true, // Access key
    requiresEndpoint: false,
    requiresOrganizationId: true, // Secret key stored here
    supportsCustomEndpoint: true,
    helpText: 'Requires AWS access key and secret key',
  },
  [ProviderType.Cohere]: {
    requiresApiKey: true,
    requiresEndpoint: false,
    requiresOrganizationId: false,
    supportsCustomEndpoint: true,
    helpUrl: 'https://dashboard.cohere.com/api-keys',
    helpText: 'Get your API key from dashboard.cohere.com/api-keys',
  },
  [ProviderType.MiniMax]: {
    requiresApiKey: true,
    requiresEndpoint: false,
    requiresOrganizationId: false,
    supportsCustomEndpoint: true,
    helpText: 'Contact MiniMax support for API access.',
  },
  [ProviderType.Replicate]: {
    requiresApiKey: true,
    requiresEndpoint: false,
    requiresOrganizationId: false,
    supportsCustomEndpoint: false,
    helpUrl: 'https://replicate.com/account/api-tokens',
    helpText: 'Get your API token from replicate.com/account/api-tokens',
  },
  [ProviderType.HuggingFace]: {
    requiresApiKey: true,
    requiresEndpoint: false,
    requiresOrganizationId: false,
    supportsCustomEndpoint: true,
    helpUrl: 'https://huggingface.co/settings/tokens',
    helpText: 'Get your API token from huggingface.co/settings/tokens',
  },
  [ProviderType.DeepSeek]: {
    requiresApiKey: true,
    requiresEndpoint: false,
    requiresOrganizationId: false,
    supportsCustomEndpoint: true,
    helpText: 'Get your API key from the DeepSeek platform',
  },
  [ProviderType.Perplexity]: {
    requiresApiKey: true,
    requiresEndpoint: false,
    requiresOrganizationId: false,
    supportsCustomEndpoint: false,
    helpText: 'Get your API key from the Perplexity platform',
  },
  [ProviderType.ElevenLabs]: {
    requiresApiKey: true,
    requiresEndpoint: false,
    requiresOrganizationId: false,
    supportsCustomEndpoint: false,
    helpUrl: 'https://elevenlabs.io/api',
    helpText: 'Get your API key from elevenlabs.io/api',
  },
  [ProviderType.Mistral]: {
    requiresApiKey: true,
    requiresEndpoint: false,
    requiresOrganizationId: false,
    supportsCustomEndpoint: true,
    helpUrl: 'https://console.mistral.ai/api-keys',
    helpText: 'Get your API key from console.mistral.ai/api-keys',
  },
  [ProviderType.MistralAI]: {
    requiresApiKey: true,
    requiresEndpoint: false,
    requiresOrganizationId: false,
    supportsCustomEndpoint: true,
    helpUrl: 'https://console.mistral.ai/api-keys',
    helpText: 'Get your API key from console.mistral.ai/api-keys',
  },
  [ProviderType.Groq]: {
    requiresApiKey: true,
    requiresEndpoint: false,
    requiresOrganizationId: false,
    supportsCustomEndpoint: false,
    helpUrl: 'https://console.groq.com/keys',
    helpText: 'Get your API key from console.groq.com/keys',
  },
  [ProviderType.VertexAI]: {
    requiresApiKey: true, // Service account key
    requiresEndpoint: false,
    requiresOrganizationId: true, // Project ID
    supportsCustomEndpoint: false,
    helpText: 'Requires Google Cloud service account key and project ID',
  },
  [ProviderType.Ollama]: {
    requiresApiKey: false,
    requiresEndpoint: true,
    requiresOrganizationId: false,
    supportsCustomEndpoint: true,
    helpText: 'Provide your Ollama server URL (e.g., http://localhost:11434)',
  },
  [ProviderType.Fireworks]: {
    requiresApiKey: true,
    requiresEndpoint: false,
    requiresOrganizationId: false,
    supportsCustomEndpoint: false,
    helpUrl: 'https://app.fireworks.ai/account/api-keys',
    helpText: 'Get your API key from app.fireworks.ai/account/api-keys',
  },
  [ProviderType.FireworksAI]: {
    requiresApiKey: true,
    requiresEndpoint: false,
    requiresOrganizationId: false,
    supportsCustomEndpoint: false,
    helpUrl: 'https://app.fireworks.ai/account/api-keys',
    helpText: 'Get your API key from app.fireworks.ai/account/api-keys',
  },
  [ProviderType.Bedrock]: {
    requiresApiKey: true, // Access key
    requiresEndpoint: false,
    requiresOrganizationId: true, // Secret key stored here
    supportsCustomEndpoint: true,
    helpText: 'Requires AWS access key and secret key',
  },
  [ProviderType.SageMaker]: {
    requiresApiKey: true, // Access key
    requiresEndpoint: true, // Endpoint name
    requiresOrganizationId: true, // Secret key
    supportsCustomEndpoint: false,
    helpText: 'Requires AWS credentials and SageMaker endpoint name',
  },
  [ProviderType.OpenRouter]: {
    requiresApiKey: true,
    requiresEndpoint: false,
    requiresOrganizationId: false,
    supportsCustomEndpoint: false,
    helpUrl: 'https://openrouter.ai/keys',
    helpText: 'Get your API key from openrouter.ai/keys',
  },
  [ProviderType.OpenAICompatible]: {
    requiresApiKey: true,
    requiresEndpoint: true,
    requiresOrganizationId: false,
    supportsCustomEndpoint: true,
    helpText: 'Configure OpenAI-compatible endpoint and API key',
  },
  [ProviderType.OpenAICompatibleAlt]: {
    requiresApiKey: true,
    requiresEndpoint: true,
    requiresOrganizationId: false,
    supportsCustomEndpoint: true,
    helpText: 'Configure OpenAI-compatible endpoint and API key',
  },
  [ProviderType.Ultravox]: {
    requiresApiKey: true,
    requiresEndpoint: false,
    requiresOrganizationId: false,
    supportsCustomEndpoint: false,
    helpText: 'Get your API key from Ultravox platform',
  },
  [ProviderType.GoogleCloud]: {
    requiresApiKey: true, // Service account key
    requiresEndpoint: false,
    requiresOrganizationId: true, // Project ID
    supportsCustomEndpoint: false,
    helpText: 'Requires Google Cloud service account key and project ID',
  },
  [ProviderType.GoogleCloudAlt]: {
    requiresApiKey: true, // Service account key
    requiresEndpoint: false,
    requiresOrganizationId: true, // Project ID
    supportsCustomEndpoint: false,
    helpText: 'Requires Google Cloud service account key and project ID',
  },
  [ProviderType.GCP]: {
    requiresApiKey: true, // Service account key
    requiresEndpoint: false,
    requiresOrganizationId: true, // Project ID
    supportsCustomEndpoint: false,
    helpText: 'Requires Google Cloud service account key and project ID',
  },
  [ProviderType.AWS]: {
    requiresApiKey: true, // Access key
    requiresEndpoint: false,
    requiresOrganizationId: true, // Secret key
    supportsCustomEndpoint: false,
    helpText: 'Requires AWS access key and secret key',
  },
  [ProviderType.AWSTranscribe]: {
    requiresApiKey: true, // Access key
    requiresEndpoint: false,
    requiresOrganizationId: true, // Secret key
    supportsCustomEndpoint: false,
    helpText: 'Requires AWS access key and secret key for Transcribe service',
  },
  [ProviderType.AWSTranscribeAlt]: {
    requiresApiKey: true, // Access key
    requiresEndpoint: false,
    requiresOrganizationId: true, // Secret key
    supportsCustomEndpoint: false,
    helpText: 'Requires AWS access key and secret key for Transcribe service',
  },
  [ProviderType.Cerebras]: {
    requiresApiKey: true,
    requiresEndpoint: false,
    requiresOrganizationId: false,
    supportsCustomEndpoint: true,
    helpUrl: 'https://cloud.cerebras.ai',
    helpText: 'Get your API key from cloud.cerebras.ai - offers high-performance inference',
  },
  [ProviderType.Custom]: {
    requiresApiKey: true,
    requiresEndpoint: true,
    requiresOrganizationId: false,
    supportsCustomEndpoint: true,
    helpText: 'Configure custom provider with your API endpoint and authentication',
  },
};

// Define provider aliases to avoid duplicates in UI
const PROVIDER_ALIASES: Record<string, string> = {
  [ProviderType.MistralAI]: ProviderType.Mistral,
  [ProviderType.Gemini]: ProviderType.Google,
  [ProviderType.FireworksAI]: ProviderType.Fireworks,
  [ProviderType.Bedrock]: ProviderType.AWSBedrock,
  [ProviderType.OpenAICompatibleAlt]: ProviderType.OpenAICompatible,
  [ProviderType.GoogleCloudAlt]: ProviderType.GoogleCloud,
  [ProviderType.GCP]: ProviderType.GoogleCloud,
  [ProviderType.AWS]: ProviderType.AWSTranscribe,
  [ProviderType.AWSTranscribeAlt]: ProviderType.AWSTranscribe,
};

// Check if a provider is an alias
const isProviderAlias = (provider: ProviderType): boolean => {
  return provider in PROVIDER_ALIASES;
};

// Convert provider type to select options
export const getProviderSelectOptions = () => {
  return Object.values(ProviderType)
    .filter(type => !isProviderAlias(type)) // Filter out aliases
    .map(type => ({
      value: type,
      label: PROVIDER_DISPLAY_NAMES[type],
    }));
};

// Get LLM providers only (excluding audio-only providers)
export const getLLMProviderSelectOptions = () => {
  return Object.values(ProviderType)
    .filter(type => {
      if (isProviderAlias(type)) return false; // Filter out aliases
      const categories = PROVIDER_CATEGORIES[type];
      return categories.includes(ProviderCategory.Chat) || 
             categories.includes(ProviderCategory.Embedding);
    })
    .map(type => ({
      value: type,
      label: PROVIDER_DISPLAY_NAMES[type],
    }));
};

// Get audio providers only
export const getAudioProviderSelectOptions = () => {
  return Object.values(ProviderType)
    .filter(type => {
      if (isProviderAlias(type)) return false; // Filter out aliases
      const categories = PROVIDER_CATEGORIES[type];
      return categories.includes(ProviderCategory.Audio);
    })
    .map(type => ({
      value: type,
      label: PROVIDER_DISPLAY_NAMES[type],
    }));
};