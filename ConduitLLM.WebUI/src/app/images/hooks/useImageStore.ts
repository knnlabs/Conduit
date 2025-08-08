import { create } from 'zustand';
import { 
  ImageGenerationState, 
  ImageGenerationActions,
  type ImageGenerationResponse,
  type ErrorResponse
} from '../types';
import { 
  createToastErrorHandler, 
  shouldShowBalanceWarning,
  handleApiError
} from '@knn_labs/conduit-core-client';
import { notifications } from '@mantine/notifications';
import { ephemeralKeyClient } from '@/lib/client/ephemeralKeyClient';

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

    // Create error handler
    const handleError = createToastErrorHandler(notifications.show);

    try {
      // Use direct API with ephemeral keys
      const response = await ephemeralKeyClient.makeDirectRequest(
        '/v1/images/generate',
        {
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
        }
      );

      if (!response.ok) {
        let errorData: ErrorResponse | { error: string };
        try {
          errorData = await response.json() as ErrorResponse;
        } catch {
          errorData = { error: response.statusText };
        }
        
        // Create a mock HTTP error that the SDK can handle properly
        const httpError = {
          response: {
            status: response.status,
            data: errorData,
            headers: Object.fromEntries(response.headers.entries())
          },
          message: response.statusText,
          request: { url: '/v1/images/generate', method: 'POST' }
        };
        
        // This will automatically throw the appropriate ConduitError subclass
        handleApiError(httpError, '/v1/images/generate', 'POST');
      }

      const result = await response.json() as ImageGenerationResponse;
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