/**
 * Response normalization utilities for consistent data structures
 */

import type { VideoData, VideoGenerationResult } from '@/app/videos/types';
import type { MediaUsage } from '@/app/types/media';
import { 
  identifyVideoResponse, 
  type StandardVideoResponse,
  isDefined
} from './typeGuards';
import { 
  normalizeBackendVideoResponse,
  type BackendVideoResponse 
} from './metadata';

/**
 * Normalize any video response to a consistent VideoGenerationResult
 */
export function normalizeVideoResponse(response: unknown): VideoGenerationResult | null {
  const identified = identifyVideoResponse(response);
  
  switch (identified.type) {
    case 'standard':
      return normalizeStandardResponse(identified.value);
      
    case 'backend':
      return normalizeBackendResponse(identified.value);
      
    case 'unknown':
      console.warn('Unknown video response format:', identified.value);
      return null;
  }
}

/**
 * Normalize standard response format
 */
function normalizeStandardResponse(response: StandardVideoResponse): VideoGenerationResult {
  return {
    created: Date.now(),
    data: response.data,
    // Map usage if it has the required fields
    usage: response.usage && 'total_tokens' in response.usage
      ? ({
          prompt_tokens: 0, // Default to 0 if not provided
          total_tokens: response.usage.total_tokens ?? 0,
          ...response.usage
        } as MediaUsage)
      : undefined
  };
}

/**
 * Normalize backend response format to VideoGenerationResult
 */
function normalizeBackendResponse(response: BackendVideoResponse): VideoGenerationResult {
  const videoData = normalizeBackendVideoResponse(response);
  
  return {
    created: Date.now(),
    data: [videoData]
  };
}

/**
 * Extract video data from a normalized result
 */
export function extractVideoData(result: VideoGenerationResult | null | undefined): VideoData | null {
  if (!result?.data || !Array.isArray(result.data) || result.data.length === 0) {
    return null;
  }
  
  return result.data[0] ?? null;
}

/**
 * Safely extract video from various response formats
 * This replaces the complex parsing logic in VideoGallery
 */
export function extractVideoFromTaskResult(
  taskResult: unknown
): VideoData | null {
  // Handle null/undefined
  if (!isDefined(taskResult)) {
    return null;
  }
  
  // First, check if it's already a VideoGenerationResult with data
  if (
    typeof taskResult === 'object' && 
    taskResult !== null &&
    'data' in taskResult && 
    Array.isArray((taskResult as Record<string, unknown>).data)
  ) {
    const directResult = taskResult as VideoGenerationResult;
    return extractVideoData(directResult);
  }
  
  // Try to normalize the response
  const normalized = normalizeVideoResponse(taskResult);
  if (!normalized) {
    return null;
  }
  
  return extractVideoData(normalized);
}

/**
 * Batch process multiple task results for efficiency
 */
export function extractVideosFromTasks(
  tasks: Array<{ result?: unknown }>
): Array<VideoData | null> {
  return tasks.map(task => extractVideoFromTaskResult(task.result));
}

/**
 * Validate and clean video data
 */
export function validateVideoData(video: VideoData): boolean {
  // Must have either url or b64_json
  if (!video.url && !video.b64_json) {
    return false;
  }
  
  // If URL exists, it should be a valid string
  if (video.url && typeof video.url !== 'string') {
    return false;
  }
  
  // If b64_json exists, it should be a valid string
  if (video.b64_json && typeof video.b64_json !== 'string') {
    return false;
  }
  
  return true;
}

/**
 * Create a cache key for a video
 */
export function createVideoCacheKey(video: VideoData, fallbackId?: string): string {
  return video.url ?? video.b64_json ?? fallbackId ?? 'unknown';
}