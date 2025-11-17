/**
 * Type guards for runtime type checking and type narrowing
 */

import type { ModelDto } from '@knn_labs/conduit-admin-client';

// Removed capabilities-related type guards as capabilities are now embedded directly in ModelDto

/**
 * Type guard for provider mapping
 */
export function isProviderMapping(obj: unknown): obj is {
  id: number;
  modelAlias: string;
  providerModelId: string;
  providerId: number;
  modelId: number;
  isEnabled: boolean;
  provider?: {
    id: number;
    providerType: number;
    providerName: string;
  };
} {
  if (!obj || typeof obj !== 'object') return false;
  
  const mapping = obj as Record<string, unknown>;
  
  return typeof mapping.id === 'number' &&
         typeof mapping.modelAlias === 'string' &&
         typeof mapping.providerModelId === 'string' &&
         typeof mapping.providerId === 'number' &&
         typeof mapping.modelId === 'number' &&
         typeof mapping.isEnabled === 'boolean';
}

/**
 * Safely extract capabilities from a model
 * Capabilities are now directly embedded in the model entity (flat structure)
 */
export function extractCapabilities(model: ModelDto) {
  return {
    supportsChat: model.supportsChat ?? false,
    supportsVision: model.supportsVision ?? false,
    supportsImageGeneration: model.supportsImageGeneration ?? false,
    supportsVideoGeneration: model.supportsVideoGeneration ?? false,
    supportsEmbeddings: model.supportsEmbeddings ?? false,
    supportsFunctionCalling: model.supportsFunctionCalling ?? false,
    supportsStreaming: model.supportsStreaming ?? true,
    maxInputTokens: model.maxInputTokens ?? undefined,
    maxOutputTokens: model.maxOutputTokens ?? undefined,
  };
}

/**
 * Type guard to check if a value is a valid model ID
 */
export function isValidModelId(id: unknown): id is number {
  return typeof id === 'number' && id > 0 && Number.isInteger(id);
}

/**
 * Type guard to check if error has a message property
 */
export function isErrorWithMessage(error: unknown): error is { message: string } {
  return (
    typeof error === 'object' &&
    error !== null &&
    'message' in error &&
    typeof (error as Record<string, unknown>).message === 'string'
  );
}

/**
 * Get error message from unknown error type
 */
export function getErrorMessage(error: unknown): string {
  if (isErrorWithMessage(error)) {
    return error.message;
  }
  
  if (typeof error === 'string') {
    return error;
  }
  
  return 'An unknown error occurred';
}