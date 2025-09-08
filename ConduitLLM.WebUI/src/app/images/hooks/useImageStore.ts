import { create } from 'zustand';
import { createMediaStore, type MediaStore } from '@/app/hooks/createMediaStore';
import { 
  ImageTask, 
  ImageGenerationSettings,
  ImageGenerationResponse,
  GeneratedImage
} from '../types';
import { 
  createToastErrorHandler, 
  shouldShowBalanceWarning
} from '@knn_labs/conduit-core-client';
import { notifications } from '@mantine/notifications';

const LOCAL_STORAGE_KEY = 'conduit-image-generation';

// Create the base store configuration
const imageStoreConfig = createMediaStore<ImageTask, ImageGenerationSettings>({
  name: LOCAL_STORAGE_KEY,
  initialSettings: {
    model: '',
    quality: 'standard',
    style: 'vivid',
  },
  maxHistorySize: 20,
  persistHistory: true,
  partializeState: (state: MediaStore<ImageTask, ImageGenerationSettings>) => ({
    settings: state.settings,
    taskHistory: state.taskHistory.filter(
      (task: ImageTask) => task.status === 'completed' || task.status === 'failed' || task.status === 'error'
    ).slice(0, 10), // Keep only last 10 completed/failed images in storage
  }),
});

// Extend with image-specific state and actions
interface ImageStoreExtensions {
  // Additional state
  prompt: string;
  status: 'idle' | 'generating' | 'completed' | 'error';
  currentResults: GeneratedImage[];
  settingsVisible: boolean;
  
  // Additional actions
  setPrompt: (prompt: string) => void;
  generateImages: (dynamicParameters?: Record<string, unknown>) => Promise<void>;
  clearResults: () => void;
  toggleSettings: () => void;
  getLatestResults: () => GeneratedImage[];
}

// Complete store type
export type ImageStore = MediaStore<ImageTask, ImageGenerationSettings> & ImageStoreExtensions;

// Create the actual store with extensions
export const useImageStore = create<ImageStore>()((set, get, api) => ({
  // Base store functionality
  ...imageStoreConfig(set, get, api),
  
  // Additional state
  prompt: '',
  status: 'idle',
  currentResults: [],
  settingsVisible: false,
  
  // Additional actions
  setPrompt: (prompt: string) => set({ prompt }),
  
  generateImages: async (dynamicParameters?: Record<string, unknown>) => {
    const state = get();
    const { prompt, settings } = state;
    
    if (!prompt.trim()) {
      set({ error: 'Please enter a prompt for image generation', status: 'error' });
      return;
    }

    if (!settings.model) {
      set({ error: 'Please select a model for image generation', status: 'error' });
      return;
    }

    // Create a new task
    const taskId = `img-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;
    const newTask: ImageTask = {
      id: taskId,
      prompt,
      status: 'generating',
      progress: 0,
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
      settings,
      retryCount: 0,
      retryHistory: [],
    };

    // Add task to history and set as current
    state.addTask(newTask);
    set({ status: 'generating', currentResults: [], error: null });

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

      // Update task with results
      state.updateTask(taskId, {
        status: 'completed',
        progress: 100,
        result: result as ImageGenerationResponse,
      });

      set({ 
        status: 'completed', 
        currentResults: result.data,
        error: null 
      });
    } catch (error) {
      // Use enhanced error handler with toast notifications
      const errorMessage = handleError(error, 'generate images');
      
      // Update task with error
      state.updateTask(taskId, {
        status: 'error',
        error: errorMessage,
      });
      
      set({ 
        status: 'error', 
        error: errorMessage,
        currentResults: []
      });
      
      // Special handling for balance errors
      if (shouldShowBalanceWarning(error)) {
        set({ 
          error: 'Please add credits to your account to generate images.'
        });
      }
    }
  },

  clearResults: () => set({ 
    currentResults: [], 
    status: 'idle', 
    error: null,
    currentTask: null 
  }),

  toggleSettings: () => set((state) => ({ settingsVisible: !state.settingsVisible })),

  getLatestResults: () => {
    const state = get();
    // Return current results if available, otherwise get from latest completed task
    if (state.currentResults.length > 0) {
      return state.currentResults;
    }
    
    const latestCompleted = state.taskHistory
      .filter(task => task.status === 'completed' && task.result)
      .sort((a, b) => new Date(b.updatedAt).getTime() - new Date(a.updatedAt).getTime())[0];
    
    return latestCompleted?.result?.data ?? [];
  },
}));