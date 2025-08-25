// Local type definitions to avoid broken SDK imports

export interface ImageGenerationRequest {
  prompt: string;
  model?: string;
  n?: number;
  quality?: 'standard' | 'hd';
  response_format?: 'url' | 'b64_json';
  size?: '256x256' | '512x512' | '1024x1024' | '1792x1024' | '1024x1792';
  style?: 'vivid' | 'natural';
  user?: string;
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
  size: '256x256' | '512x512' | '1024x1024' | '1792x1024' | '1024x1792';
  quality: 'standard' | 'hd';
  style: 'vivid' | 'natural';
  n: number;
  responseFormat: 'url' | 'b64_json';
}

// UI-specific status type
export type ImageGenerationStatus = 'idle' | 'generating' | 'completed' | 'error';

// Extend ImageData with UI-specific properties
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
  generateImages: (dynamicParameters?: Record<string, unknown>) => Promise<void>;
  clearResults: () => void;
  setError: (error: string | undefined) => void;
  toggleSettings: () => void;
}