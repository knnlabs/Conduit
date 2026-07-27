/**
 * Utility functions for provider type conversion and display.
 *
 * Provider choices and presentation metadata come from the backend provider catalog. These helpers
 * cover compatibility conversions and readable fallbacks only.
 */
import {
  ProviderType,
  getProviderTypeName,
  isValidProviderType,
  normalizeProviderType,
} from '@/lib/admin-api';

export { ProviderType, isValidProviderType };

export const getProviderDisplayName = (providerType: ProviderType | number): string =>
  getProviderTypeName(providerType);

/** Canonical compact name used by bulk-mapping matching. */
export const providerTypeToName = (providerType: ProviderType): string =>
  providerType.toLowerCase().replace(/[^a-z0-9]/g, '');

export const getProviderTypeFromDto = (
  dto: { providerType: string | number }
): ProviderType =>
  normalizeProviderType(dto.providerType) ?? ProviderType.Unknown;
