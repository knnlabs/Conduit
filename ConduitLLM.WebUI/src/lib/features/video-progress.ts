/**
 * Feature flag for video progress tracking
 * This allows gradual rollout of the new SignalR-based progress tracking
 */

const FEATURE_FLAG_KEY = 'features.videoProgressTracking';

/**
 * Check if video progress tracking is enabled
 */
export function isVideoProgressTrackingEnabled(): boolean {
  // Check localStorage for user preference
  if (typeof window !== 'undefined') {
    const stored = localStorage.getItem(FEATURE_FLAG_KEY);
    if (stored !== null) {
      return stored === 'true';
    }
  }
  
  // Check environment variable
  if (process.env.NEXT_PUBLIC_ENABLE_VIDEO_PROGRESS_TRACKING) {
    return process.env.NEXT_PUBLIC_ENABLE_VIDEO_PROGRESS_TRACKING === 'true';
  }
  
  // Default to false for safety (opt-in)
  return false;
}

/**
 * Enable video progress tracking
 */
export function enableVideoProgressTracking(): void {
  if (typeof window !== 'undefined') {
    localStorage.setItem(FEATURE_FLAG_KEY, 'true');
  }
}

/**
 * Disable video progress tracking
 */
export function disableVideoProgressTracking(): void {
  if (typeof window !== 'undefined') {
    localStorage.setItem(FEATURE_FLAG_KEY, 'false');
  }
}

/**
 * Toggle video progress tracking
 */
export function toggleVideoProgressTracking(): boolean {
  const current = isVideoProgressTrackingEnabled();
  if (current) {
    disableVideoProgressTracking();
  } else {
    enableVideoProgressTracking();
  }
  return !current;
}