// Local type definitions to avoid broken SDK imports

export interface ImageGenerationRequest {
  prompt: string;
  model?: string;
  quality?: 'standard' | 'hd';  // Only for DALL-E models
  style?: 'vivid' | 'natural';   // Only for DALL-E models
  user?: string;
  // Size, N, and ResponseFormat removed - now handled by custom parameters
  // or hardcoded defaults (n=1, response_format='url')
}

export interface ImageData {
  b64_json?: string;
  url?: string;
  revised_prompt?: string;
}

export interface ImageGenerationResponse {
  created: number;
  data: ImageData[];
}

export interface ErrorResponse {
  error: {
    message: string;
    type: string;
    param?: string | null;
    code?: string | null;
  };
}

// UI-specific interface
export interface ImageGenerationSettings {
  model: string;
  quality: 'standard' | 'hd';  // Only for DALL-E models
  style: 'vivid' | 'natural';   // Only for DALL-E models
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