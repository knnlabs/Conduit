/**
 * Type guards for runtime type checking and type narrowing
 */

import type { ModelDto } from '@/lib/admin-api';
import {
  getErrorMessage as getCanonicalErrorMessage,
  isErrorLike,
} from '@/lib/conduit-common';

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
    supportsImageInput: model.supportsImageInput ?? false,
    supportsVideoInput: model.supportsVideoInput ?? false,
    supportsAudioInput: model.supportsAudioInput ?? false,
    supportsFileInput: model.supportsFileInput ?? false,
    supportsVideoUnderstanding: model.supportsVideoUnderstanding ?? false,
    inputModalities: model.inputModalities ?? null,
    outputModalities: model.outputModalities ?? null,
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
 * (alias of the canonical `isErrorLike` in conduit-common)
 */
export const isErrorWithMessage = isErrorLike;

/** Canonical error-message extraction shared by all API clients. */
export const getErrorMessage = getCanonicalErrorMessage;
