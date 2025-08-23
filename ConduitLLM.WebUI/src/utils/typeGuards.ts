/**
 * Type guards for runtime type checking and type narrowing
 */

import type { ModelDto } from '@knn_labs/conduit-admin-client';

/**
 * Type guard to check if capabilities is a valid object
 */
export function isCapabilitiesObject(capabilities: unknown): capabilities is {
  supportsChat?: boolean;
  supportsVision?: boolean;
  supportsImageGeneration?: boolean;
  supportsVideoGeneration?: boolean;
  supportsEmbeddings?: boolean;
  supportsFunctionCalling?: boolean;
  supportsStreaming?: boolean;
  supportsAudioTranscription?: boolean;
  supportsTextToSpeech?: boolean;
  supportsRealtimeAudio?: boolean;
  maxTokens?: number;
} {
  return capabilities !== null && 
         capabilities !== undefined && 
         typeof capabilities === 'object' &&
         !Array.isArray(capabilities);
}

/**
 * Type guard to check if a model has valid capabilities
 */
export function hasValidCapabilities(model: ModelDto): boolean {
  return isCapabilitiesObject(model.capabilities);
}

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
 */
export function extractCapabilities(model: ModelDto) {
  const capabilities = model.capabilities;
  
  if (!isCapabilitiesObject(capabilities)) {
    return {
      supportsChat: false,
      supportsVision: false,
      supportsImageGeneration: false,
      supportsVideoGeneration: false,
      supportsEmbeddings: false,
      supportsFunctionCalling: false,
      supportsStreaming: true,
      supportsAudioTranscription: false,
      supportsTextToSpeech: false,
      supportsRealtimeAudio: false,
      maxTokens: undefined,
    };
  }
  
  return {
    supportsChat: capabilities.supportsChat ?? false,
    supportsVision: capabilities.supportsVision ?? false,
    supportsImageGeneration: capabilities.supportsImageGeneration ?? false,
    supportsVideoGeneration: capabilities.supportsVideoGeneration ?? false,
    supportsEmbeddings: capabilities.supportsEmbeddings ?? false,
    supportsFunctionCalling: capabilities.supportsFunctionCalling ?? false,
    supportsStreaming: capabilities.supportsStreaming ?? true,
    supportsAudioTranscription: capabilities.supportsAudioTranscription ?? false,
    supportsTextToSpeech: capabilities.supportsTextToSpeech ?? false,
    supportsRealtimeAudio: capabilities.supportsRealtimeAudio ?? false,
    maxTokens: capabilities.maxTokens,
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