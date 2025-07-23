import { create } from 'zustand';
import { ImageGenerationState, ImageGenerationActions } from '../types';
import type { ImageGenerationResponse } from '@knn_labs/conduit-core-client';

type ImageStore = ImageGenerationState & ImageGenerationActions;

export const useImageStore = create<ImageStore>((set, get) => ({
  // Initial state
  prompt: '',
  settings: {
    model: '',
    size: '1024x1024',
    quality: 'standard',
    style: 'vivid',
    n: 1,
    responseFormat: 'url',
  },
  status: 'idle',
  results: [],
  error: undefined,
  settingsVisible: false,

  // Actions
  setPrompt: (prompt: string) => set({ prompt }),

  updateSettings: (newSettings) =>
    set((state) => ({
      settings: { ...state.settings, ...newSettings },
    })),

  generateImages: async () => {
    const { prompt, settings } = get();
    
    if (!prompt.trim()) {
      set({ error: 'Please enter a prompt for image generation' });
      return;
    }

    if (!settings.model) {
      set({ error: 'Please select a model for image generation' });
      return;
    }

    set({ status: 'generating', results: [], error: undefined });

    try {
      const response = await fetch('/api/images/generate', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          prompt,
          model: settings.model,
          size: settings.size,
          quality: settings.quality,
          style: settings.style,
          n: settings.n,
          response_format: settings.responseFormat,
        }),
      });

      if (!response.ok) {
        let errorMessage = `HTTP ${response.status}: ${response.statusText}`;
        
        try {
          const errorData = await response.json() as { error?: string };
          if (errorData?.error) {
            errorMessage = errorData.error;
          }
        } catch (parseError) {
          console.error('Failed to parse error response:', parseError);
        }
        
        // Provide more helpful error messages based on status
        if (response.status === 500) {
          errorMessage = `Server error: ${errorMessage}. This might indicate missing image generation models or provider configuration issues.`;
        } else if (response.status === 401) {
          errorMessage = 'Authentication failed. Please check your login status.';
        } else if (response.status === 403) {
          errorMessage = 'Access denied. You may not have permission to generate images.';
        }
        
        throw new Error(errorMessage);
      }

      const result = await response.json() as ImageGenerationResponse;
      console.warn('Image generation response:', result);
      set({ 
        status: 'completed', 
        results: result.data.map(img => ({
          url: img.url,
          b64Json: img.b64_json,
          revisedPrompt: img.revised_prompt
        })),
        error: undefined 
      });
    } catch (error) {
      console.error('Image generation error:', error);
      set({ 
        status: 'error', 
        error: error instanceof Error ? error.message : 'Failed to generate images'
      });
    }
  },

  clearResults: () => set({ results: [], status: 'idle', error: undefined }),

  setError: (error) => set({ error }),

  toggleSettings: () => set((state) => ({ settingsVisible: !state.settingsVisible })),
}));