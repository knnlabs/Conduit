export interface ImageGenerationSettings {
  model: string;
  size: string;
  quality?: 'standard' | 'hd';
  style?: 'vivid' | 'natural';
  n: number;
  response_format: 'url' | 'b64_json';
}

export interface GeneratedImage {
  url?: string;
  b64_json?: string;
  revised_prompt?: string;
  id?: string;
}

export interface ImageGenerationResponse {
  created: number;
  data: GeneratedImage[];
}

export type ImageGenerationStatus = 'idle' | 'generating' | 'completed' | 'error';

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