import { renderHook, act, waitFor } from '@testing-library/react';
import { useImageStore } from '../useImageStore';
import { MediaGenerationStatus } from '@/app/types/media';
import type { ImageTask } from '../../types';

// Mock the browser client
jest.mock('@/lib/client/browserCoreClient', () => ({
  getBrowserCoreClient: jest.fn().mockResolvedValue({
    images: {
      generate: jest.fn()
    }
  })
}));

// Mock notifications
jest.mock('@mantine/notifications', () => ({
  notifications: {
    show: jest.fn()
  }
}));

// Mock the createMediaStore
jest.mock('@/app/hooks/createMediaStore', () => ({
  createMediaStore: jest.fn((options: { initialSettings: unknown }) => (set: (fn: (state: unknown) => unknown) => void) => ({
    error: null,
    settings: options.initialSettings,
    currentTask: null,
    taskHistory: [],
    maxHistorySize: 20,
    persistHistory: true,
    
    updateSettings: (updates: Record<string, unknown>) => set((state: { settings: Record<string, unknown> }) => ({
      settings: { ...state.settings, ...updates }
    })),
    
    setError: (error: string | null) => set({ error }),
    
    addTask: (task: unknown) => set((state: { taskHistory: unknown[]; maxHistorySize: number }) => ({
      taskHistory: [task, ...state.taskHistory].slice(0, state.maxHistorySize),
      currentTask: task
    })),
    
    updateTask: (taskId: string, updates: Record<string, unknown>) => set((state: { taskHistory: Array<{ id: string; [key: string]: unknown }>; currentTask: { id: string; [key: string]: unknown } | null }) => ({
      taskHistory: state.taskHistory.map((t: { id: string; [key: string]: unknown }) =>
        t.id === taskId ? { ...t, ...updates, updatedAt: new Date().toISOString() } : t
      ),
      currentTask: state.currentTask?.id === taskId 
        ? { ...state.currentTask, ...updates, updatedAt: new Date().toISOString() }
        : state.currentTask
    })),
    
    removeTask: (taskId: string) => set((state: { taskHistory: Array<{ id: string; [key: string]: unknown }>; currentTask: { id: string; [key: string]: unknown } | null }) => ({
      taskHistory: state.taskHistory.filter((t: { id: string }) => t.id !== taskId),
      currentTask: state.currentTask?.id === taskId ? null : state.currentTask
    })),
    
    clearHistory: () => set({ taskHistory: [], currentTask: null })
  }))
}));

// Mock error handler
jest.mock('@knn_labs/conduit-core-client', () => ({
  createToastErrorHandler: jest.fn(() => jest.fn((error: { message?: string } | string) => {
    if (typeof error === 'object' && error?.message) return error.message;
    if (typeof error === 'string') return error;
    return 'An error occurred';
  })),
  shouldShowBalanceWarning: jest.fn((error: { code?: string }) => {
    return error?.code === 'insufficient_balance';
  })
}));

describe('useImageStore', () => {
  beforeEach(() => {
    // Clear store state between tests
    jest.clearAllMocks();
    localStorage.clear();
    // Reset the store state completely
    const { getState } = useImageStore;
    if (getState) {
      useImageStore.setState({
        prompt: '',
        status: MediaGenerationStatus.Idle,
        currentResults: [],
        settingsVisible: false,
        error: null,
        settings: {
          model: '',
          quality: 'standard',
          style: 'vivid'
        },
        currentTask: null,
        taskHistory: []
      });
    }
  });

  describe('Initial State', () => {
    it('should have correct initial state', () => {
      const { result } = renderHook(() => useImageStore());

      expect(result.current.prompt).toBe('');
      expect(result.current.status).toBe(MediaGenerationStatus.Idle);
      expect(result.current.currentResults).toEqual([]);
      expect(result.current.settingsVisible).toBe(false);
      expect(result.current.error).toBeNull();
      expect(result.current.settings).toEqual({
        model: '',
        quality: 'standard',
        style: 'vivid'
      });
    });
  });

  describe('Prompt Management', () => {
    it('should update prompt', () => {
      const { result } = renderHook(() => useImageStore());

      act(() => {
        result.current.setPrompt('A beautiful landscape');
      });

      expect(result.current.prompt).toBe('A beautiful landscape');
    });

    it('should clear prompt', () => {
      const { result } = renderHook(() => useImageStore());

      act(() => {
        result.current.setPrompt('Test prompt');
      });

      act(() => {
        result.current.setPrompt('');
      });

      expect(result.current.prompt).toBe('');
    });
  });

  describe('Image Generation', () => {
    it('should not generate with empty prompt', async () => {
      const { result } = renderHook(() => useImageStore());

      await act(async () => {
        await result.current.generateImages();
      });

      expect(result.current.error).toBe('Please enter a prompt for image generation');
      expect(result.current.status).toBe(MediaGenerationStatus.Failed);
    });

    it('should not generate without model selected', async () => {
      const { result } = renderHook(() => useImageStore());

      act(() => {
        result.current.setPrompt('Test prompt');
      });

      await act(async () => {
        await result.current.generateImages();
      });

      expect(result.current.error).toBe('Please select a model for image generation');
      expect(result.current.status).toBe(MediaGenerationStatus.Failed);
    });

    it('should generate images successfully', async () => {
      const mockClient = {
        images: {
          generate: jest.fn().mockResolvedValue({
            data: [
              { url: 'https://example.com/image1.jpg' },
              { url: 'https://example.com/image2.jpg' }
            ]
          })
        }
      };

      const { getBrowserCoreClient } = jest.requireMock('@/lib/client/browserCoreClient') as {
        getBrowserCoreClient: jest.MockedFunction<() => Promise<typeof mockClient>>;
      };
      getBrowserCoreClient.mockResolvedValue(mockClient);

      const { result } = renderHook(() => useImageStore());

      // Set up valid state
      act(() => {
        result.current.setPrompt('Generate a sunset');
        result.current.updateSettings({ model: 'dall-e-3' });
      });

      await act(async () => {
        await result.current.generateImages();
      });

      await waitFor(() => {
        expect(result.current.status).toBe(MediaGenerationStatus.Completed);
        expect(result.current.currentResults).toHaveLength(2);
        expect(result.current.currentResults[0].url).toBe('https://example.com/image1.jpg');
      });

      expect(mockClient.images.generate).toHaveBeenCalledWith({
        prompt: 'Generate a sunset',
        model: 'dall-e-3',
        quality: 'standard',
        style: 'vivid',
        n: 1,
        response_format: 'url'
      });
    });

    it('should handle generation errors', async () => {
      const mockError = new Error('API Error');
      const mockClient = {
        images: {
          generate: jest.fn().mockRejectedValue(mockError)
        }
      };

      const { getBrowserCoreClient } = jest.requireMock('@/lib/client/browserCoreClient') as {
        getBrowserCoreClient: jest.MockedFunction<() => Promise<typeof mockClient>>;
      };
      getBrowserCoreClient.mockResolvedValue(mockClient);

      const { result } = renderHook(() => useImageStore());

      act(() => {
        result.current.setPrompt('Generate image');
        result.current.updateSettings({ model: 'dall-e-3' });
      });

      await act(async () => {
        await result.current.generateImages();
      });

      await waitFor(() => {
        expect(result.current.status).toBe(MediaGenerationStatus.Failed);
        expect(result.current.error).toBe('API Error');
        expect(result.current.currentResults).toEqual([]);
      });
    });

    it('should handle balance warning errors', async () => {
      const mockError = { code: 'insufficient_balance', message: 'Insufficient balance' };
      const mockClient = {
        images: {
          generate: jest.fn().mockRejectedValue(mockError)
        }
      };

      const { getBrowserCoreClient } = jest.requireMock('@/lib/client/browserCoreClient') as {
        getBrowserCoreClient: jest.MockedFunction<() => Promise<typeof mockClient>>;
      };
      getBrowserCoreClient.mockResolvedValue(mockClient);

      const { result } = renderHook(() => useImageStore());

      act(() => {
        result.current.setPrompt('Generate image');
        result.current.updateSettings({ model: 'dall-e-3' });
      });

      await act(async () => {
        await result.current.generateImages();
      });

      await waitFor(() => {
        expect(result.current.error).toBe('Please add credits to your account to generate images.');
      });
    });

    it('should support dynamic parameters', async () => {
      const mockClient = {
        images: {
          generate: jest.fn().mockResolvedValue({
            data: [{ url: 'https://example.com/image.jpg' }]
          })
        }
      };

      const { getBrowserCoreClient } = jest.requireMock('@/lib/client/browserCoreClient') as {
        getBrowserCoreClient: jest.MockedFunction<() => Promise<typeof mockClient>>;
      };
      getBrowserCoreClient.mockResolvedValue(mockClient);

      const { result } = renderHook(() => useImageStore());

      act(() => {
        result.current.setPrompt('Test');
        result.current.updateSettings({ model: 'dall-e-3' });
      });

      await act(async () => {
        await result.current.generateImages({ size: '1024x1024' });
      });

      expect(mockClient.images.generate).toHaveBeenCalledWith(
        expect.objectContaining({
          size: '1024x1024'
        })
      );
    });
  });

  describe('Results Management', () => {
    it('should clear results', () => {
      const { result } = renderHook(() => useImageStore());

      // Set some results first
      act(() => {
        (result.current as unknown as { currentResults: Array<{ url: string }> }).currentResults = [
          { url: 'https://example.com/image.jpg' }
        ];
      });

      act(() => {
        result.current.clearResults();
      });

      expect(result.current.currentResults).toEqual([]);
      expect(result.current.status).toBe(MediaGenerationStatus.Idle);
      expect(result.current.error).toBeNull();
      expect(result.current.currentTask).toBeNull();
    });

    it('should get latest results from history', () => {
      const { result } = renderHook(() => useImageStore());

      const mockTask: ImageTask = {
        id: 'task-1',
        prompt: 'Test',
        status: MediaGenerationStatus.Completed,
        progress: 100,
        createdAt: new Date().toISOString(),
        updatedAt: new Date().toISOString(),
        settings: { model: 'dall-e-3', quality: 'standard', style: 'vivid' },
        retryCount: 0,
        retryHistory: [],
        result: {
          created: Date.now(),
          data: [{ url: 'https://example.com/result.jpg' }]
        }
      };

      act(() => {
        result.current.addTask(mockTask);
      });

      const latestResults = result.current.getLatestResults();
      expect(latestResults).toHaveLength(1);
      expect(latestResults[0].url).toBe('https://example.com/result.jpg');
    });
  });

  describe('Settings Management', () => {
    it('should toggle settings visibility', () => {
      const { result } = renderHook(() => useImageStore());

      expect(result.current.settingsVisible).toBe(false);

      act(() => {
        result.current.toggleSettings();
      });

      expect(result.current.settingsVisible).toBe(true);

      act(() => {
        result.current.toggleSettings();
      });

      expect(result.current.settingsVisible).toBe(false);
    });

    it('should update settings', () => {
      const { result } = renderHook(() => useImageStore());

      act(() => {
        result.current.updateSettings({
          model: 'dall-e-2',
          quality: 'hd'
        });
      });

      expect(result.current.settings.model).toBe('dall-e-2');
      expect(result.current.settings.quality).toBe('hd');
      expect(result.current.settings.style).toBe('vivid'); // unchanged
    });
  });

  describe('Task History', () => {
    it('should add tasks to history', () => {
      const { result } = renderHook(() => useImageStore());

      act(() => {
        result.current.setPrompt('Test');
        result.current.updateSettings({ model: 'dall-e-3' });
      });

      expect(result.current.taskHistory).toHaveLength(0);

      // Simulate task creation (normally done in generateImages)
      const task: ImageTask = {
        id: 'test-1',
        prompt: 'Test',
        status: MediaGenerationStatus.Generating,
        progress: 0,
        createdAt: new Date().toISOString(),
        updatedAt: new Date().toISOString(),
        settings: result.current.settings,
        retryCount: 0,
        retryHistory: []
      };

      act(() => {
        result.current.addTask(task);
      });

      expect(result.current.taskHistory).toHaveLength(1);
      expect(result.current.currentTask).toEqual(task);
    });

    it('should clear history', () => {
      const { result } = renderHook(() => useImageStore());

      // Add some tasks
      const task: ImageTask = {
        id: 'test-1',
        prompt: 'Test',
        status: MediaGenerationStatus.Completed,
        progress: 100,
        createdAt: new Date().toISOString(),
        updatedAt: new Date().toISOString(),
        settings: { model: 'dall-e-3', quality: 'standard', style: 'vivid' },
        retryCount: 0,
        retryHistory: []
      };

      act(() => {
        result.current.addTask(task);
      });

      act(() => {
        result.current.clearHistory();
      });

      expect(result.current.taskHistory).toHaveLength(0);
      expect(result.current.currentTask).toBeNull();
    });
  });
});