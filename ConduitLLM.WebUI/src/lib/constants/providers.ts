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
  [ProviderType.Groq]: 'Groq',
  [ProviderType.Replicate]: 'Replicate',
  [ProviderType.Fireworks]: 'Fireworks AI',
  [ProviderType.OpenAICompatible]: 'OpenAI Compatible',
  [ProviderType.MiniMax]: 'MiniMax',
  [ProviderType.Ultravox]: 'Ultravox',
  [ProviderType.ElevenLabs]: 'ElevenLabs',
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
  [ProviderType.Groq]: [ProviderCategory.Chat],
  [ProviderType.Replicate]: [ProviderCategory.Chat, ProviderCategory.Image, ProviderCategory.Video],
  [ProviderType.Fireworks]: [ProviderCategory.Chat],
  [ProviderType.OpenAICompatible]: [ProviderCategory.Chat, ProviderCategory.Embedding],
  [ProviderType.MiniMax]: [ProviderCategory.Chat, ProviderCategory.Audio],
  [ProviderType.Ultravox]: [ProviderCategory.Audio],
  [ProviderType.ElevenLabs]: [ProviderCategory.Audio],
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
  [ProviderType.Groq]: {
    requiresApiKey: true,
    requiresEndpoint: false,
    requiresOrganizationId: false,
    supportsCustomEndpoint: false,
    helpUrl: 'https://console.groq.com/keys',
    helpText: 'Get your API key from console.groq.com/keys',
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