import type { ProviderType } from './providerType';

/**
 * A structured, provider-scoped setting an operator supplies in addition to the API key
 * (for example a Cloudflare account ID).
 *
 * The backend ProviderConfigurationRegistry owns these declarations. This type only narrows the
 * generated wire DTO into the shape forms consume.
 */
export interface ProviderSettingField {
  /** Stable machine key; also the storage key in the provider's settings map. */
  key: string;
  /** Human-readable field label. */
  label: string;
  /** Optional help text describing where to find the value. */
  helpText?: string;
  /** Whether the operator must supply this setting. */
  required: boolean;
  /** Whether the value is sensitive (rendered masked). */
  secret?: boolean;
  /** Optional regular-expression source the value must match. */
  validationRegexSource?: string;
  /** Optional placeholder shown in the input. */
  placeholder?: string;
}

/** Complete backend-owned configuration metadata for one provider type. */
export interface ProviderConfigurationDefinition {
  providerType: ProviderType;
  /** Stable numeric value used by legacy provider-association contracts. */
  providerTypeId: number;
  displayName: string;
  requiresApiKey: boolean;
  requiresEndpoint: boolean;
  supportsCustomEndpoint: boolean;
  helpUrl?: string;
  helpText?: string;
  settings: ProviderSettingField[];
}

/** Provider configuration metadata keyed by the canonical wire provider value. */
export type ProviderConfigurationSchema =
  Partial<Record<ProviderType, ProviderConfigurationDefinition>>;

/** Declared settings per provider type, retained for key-credential forms. */
export type ProviderSettingsSchema = Partial<Record<ProviderType, ProviderSettingField[]>>;

// Some older synchronous formatting utilities cannot fetch the catalog themselves. Cache the
// latest contract response so those fallbacks can use backend-owned names and numeric IDs after any
// provider surface has loaded it, without recreating a client-side provider registry.
let cachedProviderConfigurations: ProviderConfigurationSchema = {};

export function cacheProviderConfigurations(schema: ProviderConfigurationSchema): void {
  cachedProviderConfigurations = schema;
}

export function getCachedProviderConfiguration(
  providerType: ProviderType
): ProviderConfigurationDefinition | undefined {
  return cachedProviderConfigurations[providerType];
}

export function getCachedProviderConfigurationById(
  providerTypeId: number
): ProviderConfigurationDefinition | undefined {
  return Object.values(cachedProviderConfigurations)
    .find(configuration => configuration?.providerTypeId === providerTypeId);
}

/** Human-readable fallback used only before the backend provider catalog has loaded. */
export function formatProviderTypeFallback(providerType: string): string {
  if (!providerType || providerType.toLowerCase() === 'unknown') {
    return 'Unknown';
  }

  const words = providerType
    .replace(/([a-z0-9])([A-Z])/g, '$1 $2')
    .replace(/[-_]+/g, ' ');

  return words
    .split(/\s+/)
    .filter(Boolean)
    .map(word => word.toUpperCase() === 'AI'
      ? 'AI'
      : `${word.charAt(0).toUpperCase()}${word.slice(1)}`)
    .join(' ');
}
