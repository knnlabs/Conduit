import { create } from 'zustand';
import { 
  ImageGenerationState, 
  ImageGenerationActions
} from '../types';
import { 
  createToastErrorHandler, 
  shouldShowBalanceWarning
} from '@knn_labs/conduit-core-client';
import { notifications } from '@mantine/notifications';

type ImageStore = ImageGenerationState & ImageGenerationActions;

export const useImageStore = create<ImageStore>((set, get) => ({
  // Initial state
  prompt: '',
  settings: {
    model: '',
    quality: 'standard',
    style: 'vivid',
    // Size, N, and ResponseFormat removed - hardcoded defaults used
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

  generateImages: async (dynamicParameters?: Record<string, unknown>) => {
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

    // Create error handler
    const handleError = createToastErrorHandler(notifications.show);

    try {
      // Get SDK client and use it directly
      const { getBrowserCoreClient } = await import('@/lib/client/browserCoreClient');
      const client = await getBrowserCoreClient();
      
      // Use SDK to generate image with hardcoded defaults
      const result = await client.images.generate({
        prompt,
        model: settings.model,
        quality: settings.quality,
        style: settings.style,
        n: 1,  // Hardcoded default
        response_format: 'url',  // Always use URL for CDN storage
        // Include dynamic parameters if provided (overrides defaults)
        ...dynamicParameters,
      });
      set({ 
        status: 'completed', 
        results: result.data,
        error: undefined 
      });
    } catch (error) {
      // Use enhanced error handler with toast notifications
      const errorMessage = handleError(error, 'generate images');
      
      set({ 
        status: 'error', 
        error: errorMessage
      });
      
      // Special handling for balance errors
      if (shouldShowBalanceWarning(error)) {
        set({ 
          error: 'Please add credits to your account to generate images.'
        });
      }
    }
  },

  clearResults: () => set({ results: [], status: 'idle', error: undefined }),

  setError: (error) => set({ error }),

  toggleSettings: () => set((state) => ({ settingsVisible: !state.settingsVisible })),
}));