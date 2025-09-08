// Local type definitions to avoid broken SDK imports

import { 
  MediaData,
  Quality,
  Style
} from '@/app/types/media';

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

// UI-specific interface
export interface ImageGenerationSettings {
  model: string;
  quality: Quality;  // Only for DALL-E models
  style: Style;   // Only for DALL-E models
  // Size, N, and ResponseFormat removed - now handled by custom parameters
}

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



export interface ImageGenerationState {
  prompt: string;
  settings: ImageGenerationSettings;
  status: ImageGenerationStatus;
  results: GeneratedImage[];
  error?: string;
  settingsVisible: boolean;
}

export interface ImageGenerationActions {
  setPrompt: (prompt: string) => void;
  updateSettings: (settings: Partial<ImageGenerationSettings>) => void;
  generateImages: (dynamicParameters?: Record<string, unknown>) => Promise<void>;
  clearResults: () => void;
  setError: (error: string | undefined) => void;
  toggleSettings: () => void;
}