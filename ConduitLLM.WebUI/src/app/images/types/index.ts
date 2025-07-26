import type { ImageGenerationRequest, ImageData } from '@knn_labs/conduit-core-client';

// Extend SDK interface with UI-specific properties
export interface ImageGenerationSettings extends Pick<ImageGenerationRequest, 'model' | 'size' | 'quality' | 'style' | 'n'> {
  responseFormat: 'url' | 'b64_json';
}

// UI-specific status type (no SDK equivalent)
export type ImageGenerationStatus = 'idle' | 'generating' | 'completed' | 'error';

// Extend SDK ImageData with UI-specific properties
export interface GeneratedImage extends ImageData {
  id?: string; // UI-specific property for tracking
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
  generateImages: () => Promise<void>;
  clearResults: () => void;
  setError: (error: string | undefined) => void;
  toggleSettings: () => void;
}