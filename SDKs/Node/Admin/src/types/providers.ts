// Import and re-export the existing ProviderType enum
import { ProviderType } from '../models/providerType';
export { ProviderType };

/**
 * Provider metadata including display information and capabilities
 */
export interface ProviderMetadata {
  value: ProviderType;
  name: string;
  label: string;
  supportsSpeedScore: boolean;
  supportsQualityScore: boolean;
  supportsVariation: boolean;
  defaultMaxInputTokens?: number;
  defaultMaxOutputTokens?: number;
  description?: string;
}

/**
 * Provider configuration registry
 */
export const PROVIDER_REGISTRY: Record<number, ProviderMetadata> = {
  [ProviderType.OpenAI]: {
    value: ProviderType.OpenAI,
    name: 'OpenAI',
    label: 'OpenAI',
    supportsSpeedScore: true,
    supportsQualityScore: true,
    supportsVariation: false,
    defaultMaxInputTokens: 128000,
    defaultMaxOutputTokens: 4096,
    description: 'OpenAI GPT models'
  },
  [ProviderType.Groq]: {
    value: ProviderType.Groq,
    name: 'Groq',
    label: 'Groq',
    supportsSpeedScore: true,
    supportsQualityScore: true,
    supportsVariation: true,
    defaultMaxInputTokens: 32768,
    defaultMaxOutputTokens: 8192,
    description: 'Groq high-speed inference'
  },
  [ProviderType.Replicate]: {
    value: ProviderType.Replicate,
    name: 'Replicate',
    label: 'Replicate',
    supportsSpeedScore: true,
    supportsQualityScore: true,
    supportsVariation: true,
    description: 'Replicate model hosting'
  },
  [ProviderType.Fireworks]: {
    value: ProviderType.Fireworks,
    name: 'Fireworks',
    label: 'Fireworks',
    supportsSpeedScore: true,
    supportsQualityScore: true,
    supportsVariation: false,
    description: 'Fireworks AI inference'
  },
  [ProviderType.OpenAICompatible]: {
    value: ProviderType.OpenAICompatible,
    name: 'OpenAICompatible',
    label: 'OpenAI Compatible',
    supportsSpeedScore: true,
    supportsQualityScore: true,
    supportsVariation: true,
    description: 'OpenAI-compatible APIs'
  },
  [ProviderType.MiniMax]: {
    value: ProviderType.MiniMax,
    name: 'MiniMax',
    label: 'MiniMax',
    supportsSpeedScore: true,
    supportsQualityScore: true,
    supportsVariation: false,
    description: 'MiniMax AI models'
  },
  [ProviderType.Cerebras]: {
    value: ProviderType.Cerebras,
    name: 'Cerebras',
    label: 'Cerebras',
    supportsSpeedScore: true,
    supportsQualityScore: true,
    supportsVariation: false,
    defaultMaxInputTokens: 128000,
    defaultMaxOutputTokens: 8192,
    description: 'Cerebras high-performance inference'
  },
  [ProviderType.SambaNova]: {
    value: ProviderType.SambaNova,
    name: 'SambaNova',
    label: 'SambaNova',
    supportsSpeedScore: true,
    supportsQualityScore: true,
    supportsVariation: false,
    description: 'SambaNova ultra-fast inference'
  },
  [ProviderType.DeepInfra]: {
    value: ProviderType.DeepInfra,
    name: 'DeepInfra',
    label: 'DeepInfra',
    supportsSpeedScore: true,
    supportsQualityScore: true,
    supportsVariation: true,
    description: 'DeepInfra model hosting'
  },
  [ProviderType.Ultravox]: {
    value: ProviderType.Ultravox,
    name: 'Ultravox',
    label: 'Ultravox',
    supportsSpeedScore: false,
    supportsQualityScore: false,
    supportsVariation: false,
    description: 'Ultravox voice models'
  },
  [ProviderType.ElevenLabs]: {
    value: ProviderType.ElevenLabs,
    name: 'ElevenLabs',
    label: 'ElevenLabs',
    supportsSpeedScore: false,
    supportsQualityScore: true,
    supportsVariation: false,
    description: 'ElevenLabs audio synthesis'
  }
};

/**
 * Get list of available providers for dropdown/selection
 */
export function getAvailableProviders(): ProviderMetadata[] {
  return Object.values(PROVIDER_REGISTRY);
}

/**
 * Get provider metadata by type
 */
export function getProviderMetadata(provider: string | number): ProviderMetadata | undefined {
  // Try parsing as number first
  const numProvider = typeof provider === 'number' ? provider : parseInt(provider as string, 10);
  if (!isNaN(numProvider) && numProvider in PROVIDER_REGISTRY) {
    return PROVIDER_REGISTRY[numProvider];
  }
  
  // Try case-insensitive match
  const normalizedProvider = normalizeProviderType(provider);
  if (normalizedProvider !== undefined) {
    return PROVIDER_REGISTRY[normalizedProvider];
  }
  
  return undefined;
}

/**
 * Get provider type name from enum value
 */
export function getProviderTypeName(value: ProviderType): string {
  const metadata = PROVIDER_REGISTRY[value];
  return metadata ? metadata.name : String(value);
}

/**
 * Normalize provider type string to enum value
 */
export function normalizeProviderType(provider: string | number): ProviderType | undefined {
  if (!provider && provider !== 0) return undefined;
  
  // If it's already a number, check if it's valid
  if (typeof provider === 'number') {
    return provider in PROVIDER_REGISTRY ? provider as ProviderType : undefined;
  }
  
  // Try to parse as number
  const numValue = parseInt(provider, 10);
  if (!isNaN(numValue) && numValue in PROVIDER_REGISTRY) {
    return numValue as ProviderType;
  }
  
  // Try case-insensitive match against provider names
  const upperProvider = provider.toUpperCase();
  for (const [key, metadata] of Object.entries(PROVIDER_REGISTRY)) {
    if (metadata.name.toUpperCase() === upperProvider) {
      return Number(key) as ProviderType;
    }
  }
  
  // Handle special cases
  const specialCases: Record<string, ProviderType> = {
    'openai': ProviderType.OpenAI,
    'openaicompatible': ProviderType.OpenAICompatible,
    'openai-compatible': ProviderType.OpenAICompatible,
    'deepinfra': ProviderType.DeepInfra,
    'deep-infra': ProviderType.DeepInfra,
    'elevenlabs': ProviderType.ElevenLabs,
    'eleven-labs': ProviderType.ElevenLabs,
    'sambanova': ProviderType.SambaNova,
    'samba-nova': ProviderType.SambaNova
  };
  
  const lowerProvider = provider.toLowerCase().replace(/[\s_]/g, '');
  if (lowerProvider in specialCases) {
    return specialCases[lowerProvider];
  }
  
  return undefined;
}

/**
 * Check if a provider type is valid
 */
export function isValidProviderType(provider: string | number): boolean {
  return normalizeProviderType(provider) !== undefined;
}

/**
 * Provider validation constraints
 */
export interface ProviderConstraints {
  speedScore: {
    min: number;
    max: number;
    step: number;
  };
  qualityScore: {
    min: number;
    max: number;
    step: number;
  };
  maxInputTokens: {
    min: number;
    max?: number;
  };
  maxOutputTokens: {
    min: number;
    max?: number;
  };
}

/**
 * Get validation constraints for all providers
 */
export function getProviderConstraints(): ProviderConstraints {
  return {
    speedScore: {
      min: 0.01,
      max: 100,
      step: 0.01
    },
    qualityScore: {
      min: 0,
      max: 1,
      step: 0.05
    },
    maxInputTokens: {
      min: 0,
      max: 2000000
    },
    maxOutputTokens: {
      min: 0,
      max: 200000
    }
  };
}