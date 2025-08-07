/**
 * Provider Configuration and Business Logic
 * This file contains the canonical definitions for provider configurations and capabilities
 */

import { ProviderType } from './providerType';
import { ModelType } from './modelType';

/** Provider display configuration */
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
  [ProviderType.SambaNova]: 'SambaNova Cloud',
};

/** Provider categories for grouping in UI */
export enum ProviderCategory {
  Chat = 'chat',
  Audio = 'audio',
  Image = 'image',
  Video = 'video',
  Embedding = 'embedding',
  Custom = 'custom',
}

/** Map providers to their primary categories */
export const PROVIDER_CATEGORIES: Record<ProviderType, ProviderCategory[]> = {
  [ProviderType.OpenAI]: [ProviderCategory.Chat, ProviderCategory.Audio, ProviderCategory.Image, ProviderCategory.Embedding],
  [ProviderType.Groq]: [ProviderCategory.Chat],
  [ProviderType.Replicate]: [ProviderCategory.Chat, ProviderCategory.Image, ProviderCategory.Video],
  [ProviderType.Fireworks]: [ProviderCategory.Chat, ProviderCategory.Image],
  [ProviderType.OpenAICompatible]: [ProviderCategory.Chat, ProviderCategory.Embedding],
  [ProviderType.MiniMax]: [ProviderCategory.Chat, ProviderCategory.Audio],
  [ProviderType.Ultravox]: [ProviderCategory.Audio],
  [ProviderType.ElevenLabs]: [ProviderCategory.Audio],
  [ProviderType.Cerebras]: [ProviderCategory.Chat],
  [ProviderType.SambaNova]: [ProviderCategory.Chat],
};

/** Provider-specific configuration requirements */
export interface ProviderConfigRequirements {
  requiresApiKey: boolean;
  requiresEndpoint: boolean;
  requiresOrganizationId: boolean;
  supportsCustomEndpoint: boolean;
  helpUrl?: string;
  helpText?: string;
  supportedModelTypes: ModelType[];
}

export const PROVIDER_CONFIG_REQUIREMENTS: Record<ProviderType, ProviderConfigRequirements> = {
  [ProviderType.OpenAI]: {
    requiresApiKey: true,
    requiresEndpoint: false,
    requiresOrganizationId: false,
    supportsCustomEndpoint: true,
    helpUrl: 'https://platform.openai.com/api-keys',
    helpText: 'Get your API key from platform.openai.com/api-keys',
    supportedModelTypes: [ModelType.Chat, ModelType.Audio, ModelType.Image, ModelType.Embedding]
  },
  [ProviderType.Groq]: {
    requiresApiKey: true,
    requiresEndpoint: false,
    requiresOrganizationId: false,
    supportsCustomEndpoint: false,
    helpUrl: 'https://console.groq.com/keys',
    helpText: 'Get your API key from console.groq.com/keys',
    supportedModelTypes: [ModelType.Chat]
  },
  [ProviderType.Replicate]: {
    requiresApiKey: true,
    requiresEndpoint: false,
    requiresOrganizationId: false,
    supportsCustomEndpoint: false,
    helpUrl: 'https://replicate.com/account/api-tokens',
    helpText: 'Get your API token from replicate.com/account/api-tokens',
    supportedModelTypes: [ModelType.Chat, ModelType.Image, ModelType.Video]
  },
  [ProviderType.Fireworks]: {
    requiresApiKey: true,
    requiresEndpoint: false,
    requiresOrganizationId: false,
    supportsCustomEndpoint: false,
    helpUrl: 'https://app.fireworks.ai/account/api-keys',
    helpText: 'Get your API key from app.fireworks.ai/account/api-keys',
    supportedModelTypes: [ModelType.Chat, ModelType.Image]
  },
  [ProviderType.OpenAICompatible]: {
    requiresApiKey: true,
    requiresEndpoint: true,
    requiresOrganizationId: false,
    supportsCustomEndpoint: true,
    helpText: 'Configure OpenAI-compatible endpoint and API key',
    supportedModelTypes: [ModelType.Chat, ModelType.Embedding]
  },
  [ProviderType.MiniMax]: {
    requiresApiKey: true,
    requiresEndpoint: false,
    requiresOrganizationId: false,
    supportsCustomEndpoint: true,
    helpText: 'Contact MiniMax support for API access',
    supportedModelTypes: [ModelType.Chat, ModelType.Audio]
  },
  [ProviderType.Ultravox]: {
    requiresApiKey: true,
    requiresEndpoint: false,
    requiresOrganizationId: false,
    supportsCustomEndpoint: false,
    helpText: 'Get your API key from Ultravox platform',
    supportedModelTypes: [ModelType.Audio]
  },
  [ProviderType.ElevenLabs]: {
    requiresApiKey: true,
    requiresEndpoint: false,
    requiresOrganizationId: false,
    supportsCustomEndpoint: false,
    helpUrl: 'https://elevenlabs.io/api',
    helpText: 'Get your API key from elevenlabs.io/api',
    supportedModelTypes: [ModelType.Audio]
  },
  [ProviderType.Cerebras]: {
    requiresApiKey: true,
    requiresEndpoint: false,
    requiresOrganizationId: false,
    supportsCustomEndpoint: true,
    helpUrl: 'https://cloud.cerebras.ai',
    helpText: 'Get your API key from cloud.cerebras.ai - offers high-performance inference',
    supportedModelTypes: [ModelType.Chat]
  },
  [ProviderType.SambaNova]: {
    requiresApiKey: true,
    requiresEndpoint: false,
    requiresOrganizationId: false,
    supportsCustomEndpoint: true,
    helpUrl: 'https://cloud.sambanova.ai/plans/pricing',
    helpText: 'Get your API key from cloud.sambanova.ai - ultra-fast inference with 250+ tokens/second',
    supportedModelTypes: [ModelType.Chat]
  },
};

/** Utility functions for provider configuration */
export const ProviderConfigUtils = {
  /**
   * Get all providers as select options
   */
  getSelectOptions: () => {
    return Object.values(ProviderType)
      .filter((value): value is ProviderType => typeof value === 'number')
      .map(type => ({
        value: type.toString(),
        label: PROVIDER_DISPLAY_NAMES[type],
        categories: PROVIDER_CATEGORIES[type]
      }));
  },

  /**
   * Get LLM providers only (excluding audio-only providers)
   */
  getLLMProviderSelectOptions: () => {
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
        categories: PROVIDER_CATEGORIES[type]
      }));
  },

  /**
   * Get providers by category
   */
  getProvidersByCategory: (category: ProviderCategory) => {
    return Object.values(ProviderType)
      .filter((value): value is ProviderType => {
        if (typeof value !== 'number') return false;
        const categories = PROVIDER_CATEGORIES[value];
        return categories?.includes(category);
      })
      .map(type => ({
        value: type.toString(),
        label: PROVIDER_DISPLAY_NAMES[type],
        categories: PROVIDER_CATEGORIES[type]
      }));
  },

  /**
   * Get providers that support a specific model type
   */
  getProvidersByModelType: (modelType: ModelType) => {
    return Object.entries(PROVIDER_CONFIG_REQUIREMENTS)
      .filter(([, config]) => config.supportedModelTypes.includes(modelType))
      .map(([providerType]) => {
        const type = parseInt(providerType) as ProviderType;
        return {
          value: type.toString(),
          label: PROVIDER_DISPLAY_NAMES[type],
          categories: PROVIDER_CATEGORIES[type]
        };
      });
  },

  /**
   * Get configuration requirements for a provider
   */
  getConfigRequirements: (providerType: ProviderType) => {
    return PROVIDER_CONFIG_REQUIREMENTS[providerType];
  },

  /**
   * Check if a provider supports a specific model type
   */
  supportsModelType: (providerType: ProviderType, modelType: ModelType) => {
    const requirements = PROVIDER_CONFIG_REQUIREMENTS[providerType];
    return requirements?.supportedModelTypes.includes(modelType) ?? false;
  },

  /**
   * Get display name for a provider
   */
  getDisplayName: (providerType: ProviderType) => {
    return PROVIDER_DISPLAY_NAMES[providerType];
  },

  /**
   * Get categories for a provider
   */
  getCategories: (providerType: ProviderType) => {
    return PROVIDER_CATEGORIES[providerType] ?? [];
  }
};