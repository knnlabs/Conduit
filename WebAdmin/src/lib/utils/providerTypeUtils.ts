/**
 * Utility functions for provider type conversion and display.
 *
 * Provider choices and presentation metadata come from the backend provider catalog. These helpers
 * cover compatibility conversions and readable fallbacks only.
 */
import {
  ProviderType,
  getProviderTypeName,
  normalizeProviderType,
} from '@/lib/admin-api';

export { ProviderType };

export const getProviderDisplayName = (providerType: ProviderType | number): string =>
  getProviderTypeName(providerType);

export const providerTypeToString = (providerType: ProviderType): string =>
  providerType.toString();

export const stringToProviderType = (value: string): ProviderType => {
  const providerType = normalizeProviderType(value);
  if (!providerType) {
    throw new Error(`Invalid provider type string: ${value}`);
  }
  return providerType;
};

export const isValidProviderType = (value: unknown): value is ProviderType =>
  typeof value === 'string' && normalizeProviderType(value) !== undefined;

/** Canonical compact name used by bulk-mapping matching. */
export const providerTypeToName = (providerType: ProviderType): string =>
  providerType.toLowerCase().replace(/[^a-z0-9]/g, '');

export const getProviderTypeFromDto = (
  dto: { providerType: string | number }
): ProviderType =>
  normalizeProviderType(dto.providerType) ?? ProviderType.Unknown;
