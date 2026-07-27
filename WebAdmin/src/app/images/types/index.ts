// Local type definitions to avoid broken SDK imports

import {
  MediaData,
  Quality,
  Style
} from '@/app/types/media';
import type { MediaTask, MediaSettings } from '@/app/hooks/createMediaStore';

// Re-export for components that use ErrorResponse
export type { ErrorResponse } from '@/app/types/media';

export interface ImageGenerationRequest {
  prompt: string;
  model?: string;
  quality?: Quality;  // Only for DALL-E models
  style?: Style;   // Only for DALL-E models
  user?: string;
  // Size, N, and ResponseFormat removed - now handled by custom parameters
  // or hardcoded defaults (n=1, response_format='url')
}

// ImageData is the same as MediaData - use MediaData directly or create alias
export type ImageData = MediaData;

export interface ImageGenerationResponse {
  created: number;
  data: ImageData[];
}

// ErrorResponse is now imported from shared media types

// UI-specific interface — same shape as the shared MediaSettings
// (Size, N, and ResponseFormat removed - now handled by custom parameters)
export type ImageGenerationSettings = MediaSettings;

// UI-specific status type
export type ImageGenerationStatus = 'idle' | 'generating' | 'completed' | 'error';

// Extend ImageData with UI-specific properties
export interface GeneratedImage extends ImageData {
  id?: string; // UI-specific property for tracking
  width?: number; // Image width in pixels
  height?: number; // Image height in pixels
  sizeBytes?: number; // File size in bytes
  format?: string; // Image format (png, jpeg, etc.)
}

// Image task for history tracking — shared media task shape plus image settings
export interface ImageTask extends MediaTask<ImageGenerationResponse> {
  settings: ImageGenerationSettings;
}



// Legacy interfaces for backward compatibility - components should use the store directly
export interface ImageGenerationState {
  prompt: string;
  settings: ImageGenerationSettings;
  status: ImageGenerationStatus;
  currentResults: GeneratedImage[];
  error?: string;
  settingsVisible: boolean;
}

export interface ImageGenerationActions {
  setPrompt: (prompt: string) => void;
  updateSettings: (settings: Partial<ImageGenerationSettings>) => void;
  generateImages: (dynamicParameters?: Record<string, unknown>) => Promise<void>;
  clearResults: () => void;
  setError: (error: string | null) => void;
  toggleSettings: () => void;
}
