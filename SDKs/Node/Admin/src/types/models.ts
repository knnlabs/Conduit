/**
 * Model and Provider Type Association types
 */

import { ProviderType } from './providers';

/**
 * Provider type association for model identifiers
 */
export interface ProviderTypeAssociation {
  id: number;
  identifier: string;
  provider: number | null;
  isPrimary: boolean;
  modelCostId?: number | null;
  maxInputTokens?: number | null;
  maxOutputTokens?: number | null;
  speedScore?: number | null;
  qualityScore?: number | null;
  providerVariation?: string | null;
}

/**
 * Input type for creating/updating provider type associations
 */
export interface ProviderTypeAssociationInput {
  identifier: string;
  provider?: string | number;
  isPrimary?: boolean;
  metadata?: string;
  maxInputTokens?: number | null;
  maxOutputTokens?: number | null;
  speedScore?: number | null;
  qualityScore?: number | null;
  providerVariation?: string | null;
}

/**
 * Normalized provider type association with validated provider type
 */
export interface NormalizedProviderTypeAssociation extends ProviderTypeAssociation {
  normalizedProvider: ProviderType | null;
  providerName: string | null;
}

/**
 * Validation result for provider type associations
 */
export interface ValidationResult<T = unknown> {
  valid: boolean;
  data?: T;
  errors?: Record<string, string | string[]>;
}

/**
 * Validation error details
 */
export interface ValidationErrorDetails {
  field: string;
  message: string;
  value?: unknown;
}

/**
 * Type guard for ProviderTypeAssociation
 */
export function isProviderTypeAssociation(obj: unknown): obj is ProviderTypeAssociation {
  if (!obj || typeof obj !== 'object') return false;
  
  const assoc = obj as Record<string, unknown>;
  return (
    typeof assoc.id === 'number' &&
    typeof assoc.identifier === 'string' &&
    typeof assoc.provider === 'string' &&
    typeof assoc.isPrimary === 'boolean'
  );
}

/**
 * Type guard for ProviderTypeAssociationInput
 */
export function isProviderTypeAssociationInput(obj: unknown): obj is ProviderTypeAssociationInput {
  if (!obj || typeof obj !== 'object') return false;
  
  const input = obj as Record<string, unknown>;
  return typeof input.identifier === 'string';
}