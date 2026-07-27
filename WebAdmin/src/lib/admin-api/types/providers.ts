import { ProviderType } from '../models/providerType';
import {
  formatProviderTypeFallback,
  getCachedProviderConfiguration,
  getCachedProviderConfigurationById,
} from '../models/providerConfiguration';

export { ProviderType };

const knownProviderValues = Object.values(ProviderType);
const knownConfigurableProviderValues = knownProviderValues
  .filter(value => value !== ProviderType.Unknown);

/**
 * Legacy defaults applied when editing a model/provider association.
 *
 * This is deliberately not a provider catalog: it has no display data, is never enumerated, and a
 * provider absent from it remains fully usable. These values preserve existing model-editor
 * behavior until model capability data replaces them.
 */
export interface ProviderAssociationDefaults {
  supportsSpeedScore: boolean;
  supportsQualityScore: boolean;
  supportsVariation: boolean;
  defaultMaxInputTokens?: number;
  defaultMaxOutputTokens?: number;
}

const PROVIDER_ASSOCIATION_DEFAULTS: Partial<
  Record<ProviderType, ProviderAssociationDefaults>
> = {
  [ProviderType.OpenAI]: {
    supportsSpeedScore: true,
    supportsQualityScore: true,
    supportsVariation: false,
    defaultMaxInputTokens: 128000,
    defaultMaxOutputTokens: 4096,
  },
  [ProviderType.Groq]: {
    supportsSpeedScore: true,
    supportsQualityScore: true,
    supportsVariation: true,
    defaultMaxInputTokens: 32768,
    defaultMaxOutputTokens: 8192,
  },
  [ProviderType.Replicate]: {
    supportsSpeedScore: true,
    supportsQualityScore: true,
    supportsVariation: true,
  },
  [ProviderType.Fireworks]: {
    supportsSpeedScore: true,
    supportsQualityScore: true,
    supportsVariation: false,
  },
  [ProviderType.OpenAICompatible]: {
    supportsSpeedScore: true,
    supportsQualityScore: true,
    supportsVariation: true,
  },
  [ProviderType.MiniMax]: {
    supportsSpeedScore: true,
    supportsQualityScore: true,
    supportsVariation: false,
  },
  [ProviderType.Cerebras]: {
    supportsSpeedScore: true,
    supportsQualityScore: true,
    supportsVariation: false,
    defaultMaxInputTokens: 128000,
    defaultMaxOutputTokens: 8192,
  },
  [ProviderType.SambaNova]: {
    supportsSpeedScore: true,
    supportsQualityScore: true,
    supportsVariation: false,
  },
  [ProviderType.DeepInfra]: {
    supportsSpeedScore: true,
    supportsQualityScore: true,
    supportsVariation: true,
  },
  [ProviderType.Meta]: {
    supportsSpeedScore: true,
    supportsQualityScore: true,
    supportsVariation: false,
    defaultMaxInputTokens: 1048576,
    defaultMaxOutputTokens: 131072,
  },
  [ProviderType.Azure]: {
    supportsSpeedScore: true,
    supportsQualityScore: true,
    supportsVariation: false,
    defaultMaxInputTokens: 128000,
    defaultMaxOutputTokens: 16384,
  },
  [ProviderType.Bedrock]: {
    supportsSpeedScore: true,
    supportsQualityScore: true,
    supportsVariation: false,
    defaultMaxInputTokens: 200000,
    defaultMaxOutputTokens: 65536,
  },
  [ProviderType.Ultravox]: {
    supportsSpeedScore: false,
    supportsQualityScore: false,
    supportsVariation: false,
  },
  [ProviderType.ElevenLabs]: {
    supportsSpeedScore: false,
    supportsQualityScore: true,
    supportsVariation: false,
  },
  [ProviderType.Cloudflare]: {
    supportsSpeedScore: true,
    supportsQualityScore: true,
    supportsVariation: false,
  },
  [ProviderType.OpenRouter]: {
    supportsSpeedScore: true,
    supportsQualityScore: true,
    supportsVariation: true,
  },
};

function normalizeProviderName(value: string): string {
  return value.toLowerCase().replace(/[^a-z0-9]/g, '');
}

/**
 * Normalize provider values used by legacy numeric association contracts and import surfaces.
 *
 * The backend catalog supplies numeric IDs for newly added providers. Named constants above are
 * only compatibility fallbacks for code that runs before that catalog has loaded.
 */
export function normalizeProviderType(provider: string | number): ProviderType | undefined {
  if (typeof provider === 'number') {
    if (!Number.isInteger(provider) || provider <= 0) {
      return undefined;
    }
    return getCachedProviderConfigurationById(provider)?.providerType
      ?? knownConfigurableProviderValues[provider - 1];
  }

  const value = provider.trim();
  if (!value) {
    return undefined;
  }

  const numericValue = Number(value);
  if (Number.isInteger(numericValue) && String(numericValue) === value) {
    return normalizeProviderType(numericValue);
  }

  const normalizedName = normalizeProviderName(value);
  const known = knownProviderValues.find(
    candidate => normalizeProviderName(candidate) === normalizedName
  );
  if (known) {
    return known;
  }

  const catalogValue = getCachedProviderConfiguration(value as ProviderType)?.providerType;
  if (catalogValue) {
    return catalogValue;
  }

  const cached = getCachedProviderConfigurationById(
    Number.isInteger(numericValue) ? numericValue : -1
  );
  if (cached) {
    return cached.providerType;
  }

  // Compatibility aliases whose display names do not normalize to their wire enum value.
  if (normalizedName === 'workersai') {
    return ProviderType.Cloudflare;
  }
  if (normalizedName === 'metaai') {
    return ProviderType.Meta;
  }

  // Generated contract types remain authoritative. Accept a canonical enum-shaped value that a
  // newer generated contract added even when no provider-specific frontend constant exists.
  return /^[A-Za-z][A-Za-z0-9]*$/.test(value)
    ? value as ProviderType
    : undefined;
}

/** Return the backend-owned display name when cached, with a readable pre-load fallback. */
export function getProviderTypeName(value: string | number): string {
  const providerType = normalizeProviderType(value);
  if (!providerType) {
    return typeof value === 'number' ? `Provider ${value}` : 'Unknown';
  }
  return getCachedProviderConfiguration(providerType)?.displayName
    ?? formatProviderTypeFallback(providerType);
}

export function getProviderAssociationDefaults(
  provider: string | number
): ProviderAssociationDefaults | undefined {
  const providerType = normalizeProviderType(provider);
  return providerType ? PROVIDER_ASSOCIATION_DEFAULTS[providerType] : undefined;
}

/** Check whether a value can identify a configurable provider. */
export function isValidProviderType(
  provider: unknown,
): provider is ProviderType | number {
  if (typeof provider === 'number') {
    // The backend remains authoritative for numeric enum membership. Accept positive integer IDs so
    // a newly generated provider contract is usable before this compatibility module changes.
    return Number.isInteger(provider) && provider > 0;
  }
  if (typeof provider !== 'string') {
    return false;
  }
  const normalized = normalizeProviderType(provider);
  return normalized !== undefined && normalized !== ProviderType.Unknown;
}

/**
 * Validation constraints for legacy model-provider association inputs.
 *
 * Token limits and operation support are model/association data, not provider-type metadata, so no
 * provider-specific defaults live here.
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

export function getProviderConstraints(): ProviderConstraints {
  return {
    speedScore: {
      min: 0.01,
      max: 100,
      step: 0.01,
    },
    qualityScore: {
      min: 0,
      max: 1,
      step: 0.05,
    },
    maxInputTokens: {
      min: 0,
      max: 2000000,
    },
    maxOutputTokens: {
      min: 0,
      max: 200000,
    },
  };
}

/** Converts a canonical wire provider value for legacy numeric association fields. */
export function providerTypeToOrdinal(provider: ProviderType): number {
  if (provider === ProviderType.Unknown) {
    return 0;
  }

  const catalogId = getCachedProviderConfiguration(provider)?.providerTypeId;
  if (catalogId) {
    return catalogId;
  }

  const knownIndex = knownConfigurableProviderValues.indexOf(provider);
  return knownIndex >= 0 ? knownIndex + 1 : 0;
}
