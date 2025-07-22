/**
 * Audio configuration models and types for the Conduit Admin API
 */

import { AudioConfigMetadata } from './metadata';

/**
 * Request for creating or updating an audio provider configuration
 */
export interface AudioProviderConfigRequest {
  /** The name of the audio provider */
  name: string;
  
  /** The base URL for the audio provider API */
  baseUrl: string;
  
  /** The API key for authentication */
  apiKey: string;
  
  /** Whether this provider is enabled */
  isEnabled?: boolean;
  
  /** The supported operation types */
  supportedOperations?: string[];
  
  /** Additional configuration settings */
  settings?: AudioConfigMetadata;
  
  /** The priority/weight of this provider */
  priority?: number;
  
  /** The timeout in seconds for requests to this provider */
  timeoutSeconds?: number;
}

/**
 * Audio provider configuration
 */
export interface AudioProviderConfigDto extends AudioProviderConfigRequest {
  /** The unique identifier for the provider configuration */
  id: string;
  
  /** When the configuration was created */
  createdAt: string;
  
  /** When the configuration was last updated */
  updatedAt: string;
  
  /** The last time the provider was tested */
  lastTestedAt?: string;
  
  /** Whether the last test was successful */
  lastTestSuccessful?: boolean;
  
  /** The result message from the last test */
  lastTestMessage?: string;
}

/**
 * Request for creating or updating audio cost configuration
 */
export interface AudioCostConfigRequest {
  /** The audio provider identifier */
  providerId: string;
  
  /** The operation type (e.g., "speech-to-text", "text-to-speech") */
  operationType: string;
  
  /** The model name */
  modelName?: string;
  
  /** The cost per unit */
  costPerUnit: number;
  
  /** The unit type (e.g., "minute", "character", "request") */
  unitType: string;
  
  /** The currency code */
  currency?: string;
  
  /** Whether this cost configuration is active */
  isActive?: boolean;
  
  /** When this cost configuration becomes effective */
  effectiveFrom?: string;
  
  /** When this cost configuration expires */
  effectiveTo?: string;
}

/**
 * Audio cost configuration
 */
export interface AudioCostConfigDto extends AudioCostConfigRequest {
  /** The unique identifier for the cost configuration */
  id: string;
  
  /** When the configuration was created */
  createdAt: string;
  
  /** When the configuration was last updated */
  updatedAt: string;
}

/**
 * Audio usage information
 */
export interface AudioUsageDto {
  /** The unique identifier for the usage entry */
  id: string;
  
  /** The virtual key that was used */
  virtualKey: string;
  
  /** The audio provider that was used */
  provider: string;
  
  /** The operation type */
  operationType: string;
  
  /** The model that was used */
  model?: string;
  
  /** The number of units consumed */
  unitsConsumed: number;
  
  /** The unit type */
  unitType: string;
  
  /** The cost incurred */
  cost: number;
  
  /** The currency */
  currency: string;
  
  /** When the usage occurred */
  timestamp: string;
  
  /** The duration of the audio processing in seconds */
  durationSeconds?: number;
  
  /** The size of the audio file in bytes */
  fileSizeBytes?: number;
  
  /** Additional metadata about the usage */
  metadata?: AudioConfigMetadata;
}

/**
 * Audio usage summary information
 */
export interface AudioUsageSummaryDto {
  /** The start date of the summary period */
  startDate: string;
  
  /** The end date of the summary period */
  endDate: string;
  
  /** The total number of requests */
  totalRequests: number;
  
  /** The total cost */
  totalCost: number;
  
  /** The currency */
  currency: string;
  
  /** The total duration processed in seconds */
  totalDurationSeconds: number;
  
  /** The total file size processed in bytes */
  totalFileSizeBytes: number;
  
  /** Usage breakdown by virtual key */
  usageByKey: AudioKeyUsageDto[];
  
  /** Usage breakdown by provider */
  usageByProvider: AudioProviderUsageDto[];
  
  /** Usage breakdown by operation type */
  usageByOperation: AudioOperationUsageDto[];
}

/**
 * Audio usage breakdown by virtual key
 */
export interface AudioKeyUsageDto {
  /** The virtual key */
  virtualKey: string;
  
  /** The number of requests */
  requestCount: number;
  
  /** The total cost */
  totalCost: number;
  
  /** The total duration in seconds */
  totalDurationSeconds: number;
}

/**
 * Audio usage breakdown by provider
 */
export interface AudioProviderUsageDto {
  /** The provider name */
  provider: string;
  
  /** The number of requests */
  requestCount: number;
  
  /** The total cost */
  totalCost: number;
  
  /** The total duration in seconds */
  totalDurationSeconds: number;
}

/**
 * Audio usage breakdown by operation type
 */
export interface AudioOperationUsageDto {
  /** The operation type */
  operationType: string;
  
  /** The number of requests */
  requestCount: number;
  
  /** The total cost */
  totalCost: number;
  
  /** The total duration in seconds */
  totalDurationSeconds: number;
}

/**
 * Real-time audio session
 */
export interface RealtimeSessionDto {
  /** The unique session identifier */
  sessionId: string;
  
  /** The virtual key being used */
  virtualKey: string;
  
  /** The provider being used */
  provider: string;
  
  /** The operation type */
  operationType: string;
  
  /** The model being used */
  model?: string;
  
  /** When the session started */
  startedAt: string;
  
  /** The current status of the session */
  status: string;
  
  /** The current metrics for the session */
  metrics?: RealtimeSessionMetricsDto;
}

/**
 * Real-time session metrics
 */
export interface RealtimeSessionMetricsDto {
  /** The duration of the session in seconds */
  durationSeconds: number;
  
  /** The number of requests processed */
  requestsProcessed: number;
  
  /** The total cost so far */
  totalCost: number;
  
  /** The average response time in milliseconds */
  averageResponseTimeMs: number;
  
  /** The current throughput in requests per minute */
  throughputRpm: number;
}

/**
 * Result of testing an audio provider
 */
export interface AudioProviderTestResult {
  /** Whether the test was successful */
  success: boolean;
  
  /** The test result message */
  message: string;
  
  /** The response time in milliseconds */
  responseTimeMs?: number;
  
  /** When the test was performed */
  testedAt: string;
  
  /** Additional test details */
  details?: {
    capabilities?: string[];
    models?: string[];
    features?: string[];
    [key: string]: string[] | string | undefined;
  };
}

/**
 * Parameters for filtering audio usage data
 */
export interface AudioUsageFilters {
  /** Optional start date filter */
  startDate?: string;
  
  /** Optional end date filter */
  endDate?: string;
  
  /** Optional virtual key filter */
  virtualKey?: string;
  
  /** Optional provider filter */
  provider?: string;
  
  /** Optional operation type filter */
  operationType?: string;
  
  /** Page number for pagination (1-based) */
  page?: number;
  
  /** Number of items per page */
  pageSize?: number;
}

/**
 * Parameters for filtering audio usage summary
 */
export interface AudioUsageSummaryFilters {
  /** Start date for the summary */
  startDate: string;
  
  /** End date for the summary */
  endDate: string;
  
  /** Optional virtual key filter */
  virtualKey?: string;
  
  /** Optional provider filter */
  provider?: string;
  
  /** Optional operation type filter */
  operationType?: string;
}

/**
 * Common audio operation types
 */
export const AudioOperationTypes = {
  /** Speech-to-text operation */
  SPEECH_TO_TEXT: 'speech-to-text',
  
  /** Text-to-speech operation */
  TEXT_TO_SPEECH: 'text-to-speech',
  
  /** Audio transcription operation */
  TRANSCRIPTION: 'transcription',
  
  /** Audio translation operation */
  TRANSLATION: 'translation',
} as const;

export type AudioOperationType = typeof AudioOperationTypes[keyof typeof AudioOperationTypes];

/**
 * Common audio unit types
 */
export const AudioUnitTypes = {
  /** Cost per minute of audio */
  MINUTE: 'minute',
  
  /** Cost per second of audio */
  SECOND: 'second',
  
  /** Cost per character processed */
  CHARACTER: 'character',
  
  /** Cost per request */
  REQUEST: 'request',
  
  /** Cost per byte processed */
  BYTE: 'byte',
} as const;

export type AudioUnitType = typeof AudioUnitTypes[keyof typeof AudioUnitTypes];

/**
 * Common currencies
 */
export const AudioCurrencies = {
  /** US Dollar */
  USD: 'USD',
  
  /** Euro */
  EUR: 'EUR',
  
  /** British Pound */
  GBP: 'GBP',
  
  /** Japanese Yen */
  JPY: 'JPY',
} as const;

export type AudioCurrency = typeof AudioCurrencies[keyof typeof AudioCurrencies];

/**
 * Validates an audio provider configuration request
 */
export function validateAudioProviderRequest(request: AudioProviderConfigRequest): void {
  if (!request.name || request.name.trim().length === 0) {
    throw new Error('Provider name is required');
  }
  
  if (!request.baseUrl || request.baseUrl.trim().length === 0) {
    throw new Error('Base URL is required');
  }
  
  if (!request.apiKey || request.apiKey.trim().length === 0) {
    throw new Error('API key is required');
  }
  
  try {
    const url = new URL(request.baseUrl);
    if (!['http:', 'https:'].includes(url.protocol)) {
      throw new Error('Base URL must be a valid HTTP or HTTPS URL');
    }
  } catch {
    throw new Error('Base URL must be a valid HTTP or HTTPS URL');
  }
  
  if (request.timeoutSeconds !== undefined && 
      (request.timeoutSeconds <= 0 || request.timeoutSeconds > 300)) {
    throw new Error('Timeout must be between 1 and 300 seconds');
  }
  
  if (request.priority !== undefined && request.priority < 1) {
    throw new Error('Priority must be at least 1');
  }
}

/**
 * Validates an audio cost configuration request
 */
export function validateAudioCostConfigRequest(request: AudioCostConfigRequest): void {
  if (!request.providerId || request.providerId.trim().length === 0) {
    throw new Error('Provider ID is required');
  }
  
  if (!request.operationType || request.operationType.trim().length === 0) {
    throw new Error('Operation type is required');
  }
  
  if (!request.unitType || request.unitType.trim().length === 0) {
    throw new Error('Unit type is required');
  }
  
  if (request.costPerUnit < 0) {
    throw new Error('Cost per unit cannot be negative');
  }
  
  if (request.effectiveFrom && request.effectiveTo &&
      new Date(request.effectiveFrom) >= new Date(request.effectiveTo)) {
    throw new Error('Effective from date must be before effective to date');
  }
}

/**
 * Validates audio usage filters
 */
export function validateAudioUsageFilters(filters: AudioUsageFilters): void {
  if (filters.page !== undefined && filters.page < 1) {
    throw new Error('Page number must be at least 1');
  }
  
  if (filters.pageSize !== undefined && 
      (filters.pageSize < 1 || filters.pageSize > 1000)) {
    throw new Error('Page size must be between 1 and 1000');
  }
  
  if (filters.startDate && filters.endDate &&
      new Date(filters.startDate) >= new Date(filters.endDate)) {
    throw new Error('Start date must be before end date');
  }
}