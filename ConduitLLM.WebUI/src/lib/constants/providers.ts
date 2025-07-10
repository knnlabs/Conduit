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
  Azure = 'azure',
  AWSBedrock = 'aws-bedrock',
  Cohere = 'cohere',
  MiniMax = 'minimax',
  Replicate = 'replicate',
  HuggingFace = 'huggingface',
  DeepSeek = 'deepseek',
  Perplexity = 'perplexity',
  ElevenLabs = 'elevenlabs',
  Custom = 'custom',
}

// Provider display configuration
export const PROVIDER_DISPLAY_NAMES: Record<ProviderType, string> = {
  [ProviderType.OpenAI]: 'OpenAI',
  [ProviderType.Anthropic]: 'Anthropic',
  [ProviderType.Google]: 'Google AI',
  [ProviderType.Azure]: 'Azure OpenAI',
  [ProviderType.AWSBedrock]: 'AWS Bedrock',
  [ProviderType.Cohere]: 'Cohere',
  [ProviderType.MiniMax]: 'MiniMax',
  [ProviderType.Replicate]: 'Replicate',
  [ProviderType.HuggingFace]: 'Hugging Face',
  [ProviderType.DeepSeek]: 'DeepSeek',
  [ProviderType.Perplexity]: 'Perplexity',
  [ProviderType.ElevenLabs]: 'ElevenLabs',
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
  [ProviderType.Azure]: [ProviderCategory.Chat, ProviderCategory.Embedding],
  [ProviderType.AWSBedrock]: [ProviderCategory.Chat, ProviderCategory.Image],
  [ProviderType.Cohere]: [ProviderCategory.Chat, ProviderCategory.Embedding],
  [ProviderType.MiniMax]: [ProviderCategory.Chat, ProviderCategory.Audio],
  [ProviderType.Replicate]: [ProviderCategory.Chat, ProviderCategory.Image, ProviderCategory.Video],
  [ProviderType.HuggingFace]: [ProviderCategory.Chat, ProviderCategory.Image, ProviderCategory.Embedding],
  [ProviderType.DeepSeek]: [ProviderCategory.Chat],
  [ProviderType.Perplexity]: [ProviderCategory.Chat],
  [ProviderType.ElevenLabs]: [ProviderCategory.Audio],
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
    requiresOrganizationId: true,
    supportsCustomEndpoint: true,
    helpText: 'Contact MiniMax support for API access. You will need both API key and organization ID.',
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
  [ProviderType.Custom]: {
    requiresApiKey: true,
    requiresEndpoint: true,
    requiresOrganizationId: false,
    supportsCustomEndpoint: true,
    helpText: 'Configure custom provider with your API endpoint and authentication',
  },
};

// Convert provider type to select options
export const getProviderSelectOptions = () => {
  return Object.values(ProviderType).map(type => ({
    value: type,
    label: PROVIDER_DISPLAY_NAMES[type],
  }));
};

// Get LLM providers only (excluding audio-only providers)
export const getLLMProviderSelectOptions = () => {
  return Object.values(ProviderType)
    .filter(type => {
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
      const categories = PROVIDER_CATEGORIES[type];
      return categories.includes(ProviderCategory.Audio);
    })
    .map(type => ({
      value: type,
      label: PROVIDER_DISPLAY_NAMES[type],
    }));
};