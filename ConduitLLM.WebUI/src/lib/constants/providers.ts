/**
 * Provider-related constants for the WebUI
 * Re-exports from the Admin SDK to maintain compatibility
 * @deprecated - Import directly from @knn_labs/conduit-admin-client instead
 */

import { 
  ProviderType,
  PROVIDER_DISPLAY_NAMES,
  ProviderCategory,
  PROVIDER_CATEGORIES,
  PROVIDER_CONFIG_REQUIREMENTS,
  ProviderConfigUtils,
  type ProviderConfigRequirements
} from '@knn_labs/conduit-admin-client';

// Re-export everything from the SDK for compatibility
export { ProviderType };
export { PROVIDER_DISPLAY_NAMES };
export { ProviderCategory };
export { PROVIDER_CATEGORIES };
export { PROVIDER_CONFIG_REQUIREMENTS };
export type { ProviderConfigRequirements };

// Re-export utility functions from SDK
export const getProviderSelectOptions = ProviderConfigUtils.getSelectOptions;
export const getLLMProviderSelectOptions = ProviderConfigUtils.getLLMProviderSelectOptions;
export const getAudioProviderSelectOptions = () => ProviderConfigUtils.getProvidersByCategory(ProviderCategory.Audio);