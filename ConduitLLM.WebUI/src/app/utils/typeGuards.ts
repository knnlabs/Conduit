/**
 * Type guards for runtime type checking with proper validation
 */

import type { VideoData } from '@/app/videos/types';
import type { BackendVideoResponse } from './metadataExtractor';

/**
 * Standard video response format with data array
 */
export interface StandardVideoResponse {
  data: VideoData[];
  usage?: {
    total_tokens?: number;
  };
}

/**
 * Discriminated union for all possible video response types
 */
export type VideoResponse = 
  | { type: 'standard'; value: StandardVideoResponse }
  | { type: 'backend'; value: BackendVideoResponse }
  | { type: 'unknown'; value: unknown };

/**
 * Type guard for VideoData
 */
export function isVideoData(obj: unknown): obj is VideoData {
  if (typeof obj !== 'object' || obj === null) {
    return false;
  }
  
  const video = obj as Record<string, unknown>;
  
  // A VideoData must have either url or b64_json
  return typeof video.url === 'string' || typeof video.b64_json === 'string';
}

/**
 * Type guard for StandardVideoResponse
 */
export function isStandardVideoResponse(obj: unknown): obj is StandardVideoResponse {
  if (typeof obj !== 'object' || obj === null) {
    return false;
  }
  
  const response = obj as Record<string, unknown>;
  
  // Must have a data property that is an array
  if (!Array.isArray(response.data)) {
    return false;
  }
  
  // If data is empty, it's still valid
  if (response.data.length === 0) {
    return true;
  }
  
  // Check if first item looks like VideoData
  return isVideoData(response.data[0]);
}

/**
 * Type guard for BackendVideoResponse
 */
export function isBackendVideoResponse(obj: unknown): obj is BackendVideoResponse {
  if (typeof obj !== 'object' || obj === null) {
    return false;
  }
  
  const response = obj as Record<string, unknown>;
  
  // Must have VideoUrl property (the key identifier for backend format)
  return typeof response.VideoUrl === 'string';
}

/**
 * Safely parse JSON with type checking
 */
export function safeJsonParse(input: unknown): unknown {
  if (typeof input !== 'string') {
    return input;
  }
  
  try {
    return JSON.parse(input) as unknown;
  } catch {
    // Return the original string if parsing fails
    return input;
  }
}

/**
 * Identify the type of video response
 */
export function identifyVideoResponse(response: unknown): VideoResponse {
  // First, try to parse if it's a string
  const parsed = safeJsonParse(response);
  
  // Check for standard format
  if (isStandardVideoResponse(parsed)) {
    return { type: 'standard', value: parsed };
  }
  
  // Check for backend format
  if (isBackendVideoResponse(parsed)) {
    return { type: 'backend', value: parsed };
  }
  
  // Unknown format
  return { type: 'unknown', value: parsed };
}

/**
 * Type guard to check if a value is defined (not null or undefined)
 */
export function isDefined<T>(value: T | null | undefined): value is T {
  return value !== null && value !== undefined;
}

/**
 * Type guard for non-empty string
 */
export function isNonEmptyString(value: unknown): value is string {
  return typeof value === 'string' && value.length > 0;
}

/**
 * Type guard for positive number
 */
export function isPositiveNumber(value: unknown): value is number {
  return typeof value === 'number' && value > 0 && isFinite(value);
}