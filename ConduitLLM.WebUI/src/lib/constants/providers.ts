/**
 * Provider-related constants for the WebUI
 * This file serves as the single source of truth for provider types and configurations
 */

import { ProviderType } from '@knn_labs/conduit-admin-client';

// Re-export the ProviderType from the SDK for convenience
export { ProviderType };

// Provider display configuration using SDK enum
export const PROVIDER_DISPLAY_NAMES: Record<ProviderType, string> = {
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
  [ProviderType.Cerebras]: 'Cerebras',
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

// Map providers to their primary categories using SDK enum
export const PROVIDER_CATEGORIES: Record<ProviderType, ProviderCategory[]> = {
  [ProviderType.OpenAI]: [ProviderCategory.Chat, ProviderCategory.Audio, ProviderCategory.Image, ProviderCategory.Embedding],
  [ProviderType.Anthropic]: [ProviderCategory.Chat],
  [ProviderType.AzureOpenAI]: [ProviderCategory.Chat, ProviderCategory.Embedding],
  [ProviderType.Gemini]: [ProviderCategory.Chat, ProviderCategory.Embedding],
  [ProviderType.VertexAI]: [ProviderCategory.Chat, ProviderCategory.Embedding],
  [ProviderType.Cohere]: [ProviderCategory.Chat, ProviderCategory.Embedding],
  [ProviderType.Mistral]: [ProviderCategory.Chat, ProviderCategory.Embedding],
  [ProviderType.Groq]: [ProviderCategory.Chat],
  [ProviderType.Ollama]: [ProviderCategory.Chat, ProviderCategory.Embedding],
  [ProviderType.Replicate]: [ProviderCategory.Chat, ProviderCategory.Image, ProviderCategory.Video],
  [ProviderType.Fireworks]: [ProviderCategory.Chat],
  [ProviderType.Bedrock]: [ProviderCategory.Chat, ProviderCategory.Image],
  [ProviderType.HuggingFace]: [ProviderCategory.Chat, ProviderCategory.Image, ProviderCategory.Embedding],
  [ProviderType.SageMaker]: [ProviderCategory.Chat, ProviderCategory.Embedding],
  [ProviderType.OpenRouter]: [ProviderCategory.Chat],
  [ProviderType.OpenAICompatible]: [ProviderCategory.Chat, ProviderCategory.Embedding],
  [ProviderType.MiniMax]: [ProviderCategory.Chat, ProviderCategory.Audio],
  [ProviderType.Ultravox]: [ProviderCategory.Audio],
  [ProviderType.ElevenLabs]: [ProviderCategory.Audio],
  [ProviderType.GoogleCloud]: [ProviderCategory.Audio],
  [ProviderType.Cerebras]: [ProviderCategory.Chat],
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
  [ProviderType.AzureOpenAI]: {
    requiresApiKey: true,
    requiresEndpoint: true,
    requiresOrganizationId: false,
    supportsCustomEndpoint: true,
    helpText: 'Requires both API key and your Azure OpenAI endpoint URL',
  },
  [ProviderType.Gemini]: {
    requiresApiKey: true,
    requiresEndpoint: false,
    requiresOrganizationId: false,
    supportsCustomEndpoint: false,
    helpUrl: 'https://makersuite.google.com/app/apikey',
    helpText: 'Get your API key from makersuite.google.com/app/apikey',
  },
  [ProviderType.VertexAI]: {
    requiresApiKey: true, // Service account key
    requiresEndpoint: false,
    requiresOrganizationId: true, // Project ID
    supportsCustomEndpoint: false,
    helpText: 'Requires Google Cloud service account key and project ID',
  },
  [ProviderType.Cohere]: {
    requiresApiKey: true,
    requiresEndpoint: false,
    requiresOrganizationId: false,
    supportsCustomEndpoint: true,
    helpUrl: 'https://dashboard.cohere.com/api-keys',
    helpText: 'Get your API key from dashboard.cohere.com/api-keys',
  },
  [ProviderType.Mistral]: {
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
  [ProviderType.Ollama]: {
    requiresApiKey: false,
    requiresEndpoint: true,
    requiresOrganizationId: false,
    supportsCustomEndpoint: true,
    helpText: 'Provide your Ollama server URL (e.g., http://localhost:11434)',
  },
  [ProviderType.Replicate]: {
    requiresApiKey: true,
    requiresEndpoint: false,
    requiresOrganizationId: false,
    supportsCustomEndpoint: false,
    helpUrl: 'https://replicate.com/account/api-tokens',
    helpText: 'Get your API token from replicate.com/account/api-tokens',
  },
  [ProviderType.Fireworks]: {
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
  [ProviderType.HuggingFace]: {
    requiresApiKey: true,
    requiresEndpoint: false,
    requiresOrganizationId: false,
    supportsCustomEndpoint: true,
    helpUrl: 'https://huggingface.co/settings/tokens',
    helpText: 'Get your API token from huggingface.co/settings/tokens',
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
  [ProviderType.MiniMax]: {
    requiresApiKey: true,
    requiresEndpoint: false,
    requiresOrganizationId: false,
    supportsCustomEndpoint: true,
    helpText: 'Contact MiniMax support for API access.',
  },
  [ProviderType.Ultravox]: {
    requiresApiKey: true,
    requiresEndpoint: false,
    requiresOrganizationId: false,
    supportsCustomEndpoint: false,
    helpText: 'Get your API key from Ultravox platform',
  },
  [ProviderType.ElevenLabs]: {
    requiresApiKey: true,
    requiresEndpoint: false,
    requiresOrganizationId: false,
    supportsCustomEndpoint: false,
    helpUrl: 'https://elevenlabs.io/api',
    helpText: 'Get your API key from elevenlabs.io/api',
  },
  [ProviderType.GoogleCloud]: {
    requiresApiKey: true, // Service account key
    requiresEndpoint: false,
    requiresOrganizationId: true, // Project ID
    supportsCustomEndpoint: false,
    helpText: 'Requires Google Cloud service account key and project ID',
  },
  [ProviderType.Cerebras]: {
    requiresApiKey: true,
    requiresEndpoint: false,
    requiresOrganizationId: false,
    supportsCustomEndpoint: true,
    helpUrl: 'https://cloud.cerebras.ai',
    helpText: 'Get your API key from cloud.cerebras.ai - offers high-performance inference',
  },
};

// Convert provider type to select options
export const getProviderSelectOptions = () => {
  return Object.values(ProviderType)
    .filter((value): value is ProviderType => typeof value === 'number')
    .map(type => ({
      value: type.toString(),
      label: PROVIDER_DISPLAY_NAMES[type],
    }));
};

// Get LLM providers only (excluding audio-only providers)
export const getLLMProviderSelectOptions = () => {
  return Object.values(ProviderType)
    .filter((value): value is ProviderType => {
      if (typeof value !== 'number') return false;
      const categories = PROVIDER_CATEGORIES[value];
      return categories?.includes(ProviderCategory.Chat) || 
             categories?.includes(ProviderCategory.Embedding);
    })
    .map(type => ({
      value: type.toString(),
      label: PROVIDER_DISPLAY_NAMES[type],
    }));
};

// Get audio providers only
export const getAudioProviderSelectOptions = () => {
  return Object.values(ProviderType)
    .filter((value): value is ProviderType => {
      if (typeof value !== 'number') return false;
      const categories = PROVIDER_CATEGORIES[value];
      return categories?.includes(ProviderCategory.Audio);
    })
    .map(type => ({
      value: type.toString(),
      label: PROVIDER_DISPLAY_NAMES[type],
    }));
};