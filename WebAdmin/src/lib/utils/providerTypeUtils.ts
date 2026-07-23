/**
 * Utility functions for provider type conversion and display
 * Provides type-safe conversion between numeric provider types and display strings
 */

// Import ProviderType and registry utilities from SDK
import {
  ProviderType,
  PROVIDER_REGISTRY,
  getProviderTypeName,
  getAvailableProviders,
} from '@/lib/admin-api';

// Re-export the SDK's ProviderType enum and registry utilities
export { ProviderType, PROVIDER_REGISTRY };

// Convert ProviderType to display name using SDK registry
export const getProviderDisplayName = (providerType: ProviderType): string => {
  return getProviderTypeName(providerType);
};

// Convert display name back to ProviderType (for reverse lookups)
export const getProviderTypeFromDisplayName = (displayName: string): ProviderType | undefined => {
  const entry = Object.entries(PROVIDER_REGISTRY).find(
    ([, metadata]) => metadata.name === displayName || metadata.label === displayName
  );
  return entry ? entry[0] as ProviderType : undefined;
};

// Convert ProviderType to select options using SDK registry
export const getProviderSelectOptions = () => {
  return getAvailableProviders().map(provider => ({
    value: provider.value.toString(),
    label: provider.label,
  }));
};

// Convert ProviderType enum value to string for API compatibility
export const providerTypeToString = (providerType: ProviderType): string => {
  return providerType.toString();
};

// Convert string back to ProviderType
export const stringToProviderType = (str: string): ProviderType => {
  const value = Object.values(ProviderType).find(candidate => candidate === str);
  if (!value) {
    throw new Error(`Invalid provider type string: ${str}`);
  }
  return value;
};

// Type guard to check if a value is a valid ProviderType
export const isValidProviderType = (value: unknown): value is ProviderType => {
  return typeof value === 'string' && Object.values(ProviderType).includes(value as ProviderType);
};



// Get provider name string from ProviderType using SDK registry
export const providerTypeToName = (providerType: ProviderType): string => {
  const metadata = PROVIDER_REGISTRY[providerType];
  if (!metadata) {
    throw new Error(`Invalid provider type: ${providerType}`);
  }
  return metadata.name.toLowerCase().replace(/\s+/g, '');
};

// Helper to get ProviderType from a DTO
export const getProviderTypeFromDto = (dto: { providerType: string | number }): ProviderType => {
  if (typeof dto.providerType === 'number') {
    return Object.values(ProviderType).filter(value => value !== ProviderType.Unknown)[dto.providerType - 1]
      ?? ProviderType.Unknown;
  }
  return Object.values(ProviderType).find(value => value === dto.providerType)
    ?? ProviderType.Unknown;
};
