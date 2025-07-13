/**
 * API response types for the WebUI
 * 
 * NOTE: We're duplicating some SDK types here for convenience, but you can also use:
 * import type { SomeType } from '@conduitllm/admin-client';
 * 
 * The 'import type' syntax ensures only types are imported, not runtime code.
 * This is safe for client components and won't affect bundle size.
 * 
 * TODO: Consider removing these duplicates and using type-only imports from SDK
 */

// Virtual Key types
export interface VirtualKeyDto {
  id: number;
  keyName: string;
  keyPrefix: string;
  apiKey?: string;
  isEnabled: boolean;
  maxBudget?: number;
  budgetDuration?: 'Daily' | 'Monthly' | 'Total';
  currentSpend: number;
  lastResetDate?: string;
  rateLimitPerMinute?: number;
  requestCount: number;
  allowedModels?: string;
  allowedEndpoints?: string;
  allowedIpAddresses?: string;
  metadata?: string;
  createdAt: string;
  updatedAt: string;
  lastUsedAt?: string;
  expiresAt?: string;
}

// Provider types
export interface ProviderCredentialDto {
  id: number;
  providerName: string;
  apiKey?: string;
  apiEndpoint?: string;
  organizationId?: string;
  additionalConfig?: string;
  isEnabled: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface ProviderHealthStatusDto {
  providerId: string;
  isHealthy: boolean;
  lastCheckTime: string;
  responseTimeMs?: number;
  errorMessage?: string;
}

// Model Mapping types
export interface ModelProviderMappingDto {
  id: number;
  modelId: string;
  providerId: string;
  providerName?: string;
  providerModelId: string;
  priority: number;
  isEnabled: boolean;
  metadata?: string;
  createdAt: string;
  updatedAt: string;
  
  // Capability flags
  supportsVision: boolean;
  supportsImageGeneration: boolean;
  supportsAudioTranscription: boolean;
  supportsTextToSpeech: boolean;
  supportsRealtimeAudio: boolean;
  supportsFunctionCalling: boolean;
  supportsStreaming: boolean;
  
  // Extended metadata
  capabilities?: string;
  maxContextLength?: number;
  maxOutputTokens?: number;
  supportedLanguages?: string;
  supportedVoices?: string;
  supportedFormats?: string;
  tokenizerType?: string;
  
  // Advanced routing
  isDefault: boolean;
  defaultCapabilityType?: string;
}

export interface CreateModelProviderMappingDto {
  modelId: string;
  providerId: string;
  providerModelId: string;
  priority?: number;
  isEnabled?: boolean;
  metadata?: string;
  
  // Capability flags (all optional for creation)
  supportsVision?: boolean;
  supportsImageGeneration?: boolean;
  supportsAudioTranscription?: boolean;
  supportsTextToSpeech?: boolean;
  supportsRealtimeAudio?: boolean;
  supportsFunctionCalling?: boolean;
  supportsStreaming?: boolean;
  
  // Extended metadata
  capabilities?: string;
  maxContextLength?: number;
  maxOutputTokens?: number;
  supportedLanguages?: string;
  supportedVoices?: string;
  supportedFormats?: string;
  tokenizerType?: string;
  
  // Advanced routing
  isDefault?: boolean;
  defaultCapabilityType?: string;
}

export interface UpdateModelProviderMappingDto {
  providerId?: string;
  providerModelId?: string;
  priority?: number;
  isEnabled?: boolean;
  metadata?: string;
  
  // Capability flags (all optional for update)
  supportsVision?: boolean;
  supportsImageGeneration?: boolean;
  supportsAudioTranscription?: boolean;
  supportsTextToSpeech?: boolean;
  supportsRealtimeAudio?: boolean;
  supportsFunctionCalling?: boolean;
  supportsStreaming?: boolean;
  
  // Extended metadata
  capabilities?: string;
  maxContextLength?: number;
  maxOutputTokens?: number;
  supportedLanguages?: string;
  supportedVoices?: string;
  supportedFormats?: string;
  tokenizerType?: string;
  
  // Advanced routing
  isDefault?: boolean;
  defaultCapabilityType?: string;
}

// Discovery types
export interface DiscoveredModel {
  modelId: string;
  provider: string;
  displayName: string;
  capabilities: {
    chat?: boolean;
    chatStream?: boolean;
    embeddings?: boolean;
    imageGeneration?: boolean;
    vision?: boolean;
    videoGeneration?: boolean;
    functionCalling?: boolean;
    maxTokens?: number;
  };
  metadata?: Record<string, unknown>;
  lastVerified?: string;
}

// Other types that pages might need
export type BudgetDuration = 'Daily' | 'Monthly' | 'Total';

export interface ModelCapability {
  name: string;
  description?: string;
}