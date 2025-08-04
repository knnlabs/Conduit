import { renderHook, act } from '@testing-library/react';
import { useEnhancedVideoGeneration } from '../useEnhancedVideoGeneration';
import { useVideoStore } from '../useVideoStore';
import * as clientCore from '@/lib/client/coreClient';
import type { VideoProgress } from '@knn_labs/conduit-core-client';
import type { VideoStoreState, VideoTask, VideoGenerationResult } from '../../types';

// Mock dependencies
jest.mock('../useVideoStore');
jest.mock('@/lib/client/coreClient');

const mockUseVideoStore = jest.mocked(useVideoStore);
const mockGenerateVideoWithProgress = jest.mocked(clientCore.generateVideoWithProgress);

describe('useEnhancedVideoGeneration', () => {
  const mockAddTask = jest.fn();
  const mockUpdateTask = jest.fn();
  const mockSetError = jest.fn();

  beforeEach(() => {
    jest.clearAllMocks();
    
    const mockStore: VideoStoreState = {
      addTask: mockAddTask,
      updateTask: mockUpdateTask,
      setError: mockSetError,
      taskHistory: [],
      currentTask: null,
      error: null,
      settings: {
        model: 'minimax-video',
        duration: 5,
        size: '1280x720',
        fps: 30,
        style: 'natural',
        responseFormat: 'url',
      },
      settingsVisible: false,
      updateSettings: jest.fn(),
      toggleSettings: jest.fn(),
      removeTask: jest.fn(),
      clearHistory: jest.fn(),
    };
    
    mockUseVideoStore.mockReturnValue(mockStore);

    // Mock window.fetch for fallback polling
    global.fetch = jest.fn();
  });

  afterEach(() => {
    jest.restoreAllMocks();
  });

  describe('Enhanced video generation with progress tracking', () => {
    it('should use progress tracking when enabled', async () => {
      mockGenerateVideoWithProgress.mockResolvedValue({
        taskId: 'task_enhanced_123',
      });

      const hook = renderHook(() =>
        useEnhancedVideoGeneration({
          useProgressTracking: true,
          fallbackToPolling: true,
        })
      );

      await act(async () => {
        await hook.result.current.generateVideo({
          prompt: 'Test video with progress',
          settings: {
            model: 'minimax-video',
            duration: 5,
            size: '1280x720',
            fps: 30,
            style: 'natural',
            responseFormat: 'url',
          },
        });
      });

      expect(mockAddTask).toHaveBeenCalledWith(
        expect.objectContaining({
          prompt: 'Test video with progress',
          status: 'pending',
          progress: 0,
        }) as VideoTask
      );

      expect(mockGenerateVideoWithProgress).toHaveBeenCalled();
      const [requestArg, callbacksArg] = mockGenerateVideoWithProgress.mock.calls[0] as [
        Parameters<typeof clientCore.generateVideoWithProgress>[0],
        Parameters<typeof clientCore.generateVideoWithProgress>[1]
      ];
      
      expect(requestArg).toEqual({
        prompt: 'Test video with progress',
        model: 'minimax-video',
        duration: 5,
        size: '1280x720',
        fps: 30,
        style: 'natural',
      });
      
      expect(callbacksArg).toHaveProperty('onProgress');
      expect(callbacksArg).toHaveProperty('onStarted');
      expect(callbacksArg).toHaveProperty('onCompleted');
      expect(callbacksArg).toHaveProperty('onFailed');
      expect(typeof callbacksArg.onProgress).toBe('function');
      expect(typeof callbacksArg.onStarted).toBe('function');
      expect(typeof callbacksArg.onCompleted).toBe('function');
      expect(typeof callbacksArg.onFailed).toBe('function');
    });

    it('should handle progress updates correctly', async () => {
      let progressCallback: ((progress: VideoProgress) => void) | undefined;
      let startedCallback: ((taskId: string, estimatedSeconds?: number) => void) | undefined;

      mockGenerateVideoWithProgress.mockImplementation(async (request, callbacks) => {
        void request;
        const typedCallbacks = callbacks;
        progressCallback = typedCallbacks.onProgress;
        startedCallback = typedCallbacks.onStarted;
        
        // Simulate task started
        if (startedCallback) {
          startedCallback('task_progress_456', 30);
        }
        
        return { taskId: 'task_progress_456' };
      });

      const hook = renderHook(() =>
        useEnhancedVideoGeneration({
          useProgressTracking: true,
        })
      );

      await act(async () => {
        await hook.result.current.generateVideo({
          prompt: 'Progress update test',
          settings: {
            model: 'minimax-video',
            duration: 5,
            size: '1280x720',
            fps: 30,
            style: 'natural',
            responseFormat: 'url',
          },
        });
      });

      // Simulate progress updates
      act(() => {
        if (progressCallback) {
          progressCallback({
            percentage: 25,
            status: 'processing',
            message: 'Initializing',
          });
        }
      });

      expect(mockUpdateTask).toHaveBeenLastCalledWith(
        expect.any(String),
        expect.objectContaining({
          progress: 25,
          status: 'running',
          message: 'Initializing',
        }) as Partial<VideoTask>
      );

      act(() => {
        if (progressCallback) {
          progressCallback({
            percentage: 75,
            status: 'processing',
            message: 'Rendering frames',
          });
        }
      });

      expect(mockUpdateTask).toHaveBeenLastCalledWith(
        expect.any(String),
        expect.objectContaining({
          progress: 75,
          status: 'running',
          message: 'Rendering frames',
        }) as Partial<VideoTask>
      );
    });

    it('should handle successful completion', async () => {
      let completedCallback: ((result: VideoGenerationResult) => void) | undefined;

      mockGenerateVideoWithProgress.mockImplementation(async (request, callbacks) => {
        void request;
        const typedCallbacks = callbacks;
        completedCallback = typedCallbacks.onCompleted;
        return { taskId: 'task_complete_789' };
      });

      const hook = renderHook(() =>
        useEnhancedVideoGeneration({
          useProgressTracking: true,
        })
      );

      await act(async () => {
        await hook.result.current.generateVideo({
          prompt: 'Completion test',
          settings: {
            model: 'minimax-video',
            duration: 5,
            size: '1280x720',
            fps: 30,
            style: 'natural',
            responseFormat: 'url',
          },
        });
      });

      const mockResult = {
        created: Date.now(),
        data: [{
          url: 'https://example.com/video.mp4',
          metadata: { duration: 5 },
        }],
      };

      act(() => {
        if (completedCallback) {
          completedCallback(mockResult);
        }
      });

      expect(mockUpdateTask).toHaveBeenCalledWith(
        expect.any(String),
        expect.objectContaining({
          status: 'completed',
          progress: 100,
          result: mockResult,
        }) as Partial<VideoTask>
      );
    });

    it('should handle failures', async () => {
      let failedCallback: ((error: string, isRetryable: boolean) => void) | undefined;

      mockGenerateVideoWithProgress.mockImplementation(async (request, callbacks) => {
        void request;
        const typedCallbacks = callbacks;
        failedCallback = typedCallbacks.onFailed;
        return { taskId: 'task_fail_999' };
      });

      const hook = renderHook(() =>
        useEnhancedVideoGeneration({
          useProgressTracking: true,
        })
      );

      await act(async () => {
        await hook.result.current.generateVideo({
          prompt: 'Failure test',
          settings: {
            model: 'minimax-video', 
            duration: 5,
            size: '1280x720',
            fps: 30,
            style: 'natural',
            responseFormat: 'url',
          },
        });
      });

      const errorMessage = 'Insufficient GPU resources';

      act(() => {
        if (failedCallback) {
          failedCallback(errorMessage, true);
        }
      });

      expect(mockUpdateTask).toHaveBeenCalledWith(
        expect.any(String),
        expect.objectContaining({
          status: 'failed',
          error: errorMessage,
        }) as Partial<VideoTask>
      );

      expect(mockSetError).toHaveBeenCalledWith(errorMessage);
    });
  });

  describe('Fallback polling behavior', () => {
    it('should use polling when progress tracking is disabled', async () => {
      // Mock successful async video generation
      (global.fetch as jest.Mock).mockImplementation(async (url: string) => {
        if (url.includes('/v1/videos/generations/async')) {
          return {
            ok: true,
            json: async () => ({
              task_id: 'task_polling_123',
              status: 'pending',
              estimated_time_to_completion: 30,
            }),
          };
        }
        if (url.includes('/tasks/task_polling_123')) {
          return {
            ok: true,
            json: async () => ({
              task_id: 'task_polling_123',
              status: 'completed',
              progress: 100,
              result: {
                created: Date.now(),
                data: [{
                  url: 'https://example.com/video.mp4',
                  metadata: { duration: 5 },
                }],
              },
            }),
          };
        }
        throw new Error(`Unexpected URL: ${url}`);
      });

      const hook = renderHook(() =>
        useEnhancedVideoGeneration({
          useProgressTracking: false,
          fallbackToPolling: true,
        })
      );

      await act(async () => {
        await hook.result.current.generateVideo({
          prompt: 'Polling test',
          settings: {
            model: 'minimax-video',
            duration: 5,
            size: '1280x720',
            fps: 30,
            style: 'natural',
            responseFormat: 'url',
          },
        });
      });

      expect(global.fetch).toHaveBeenCalledWith(
        expect.stringContaining('/v1/videos/generations/async'),
        expect.any(Object)
      );

      // Simulate polling
      await act(async () => {
        await new Promise(resolve => setTimeout(resolve, 100));
      });

      expect(mockUpdateTask).toHaveBeenCalledWith(
        expect.any(String),
        expect.objectContaining({
          status: 'completed',
          progress: 100,
        }) as Partial<VideoTask>
      );
    });

    it('should handle polling errors gracefully', async () => {
      (global.fetch as jest.Mock).mockImplementation(async () => ({
        ok: false,
        status: 500,
        statusText: 'Internal Server Error',
        json: async () => ({
          error: 'Server error occurred',
        }),
      }));

      const hook = renderHook(() =>
        useEnhancedVideoGeneration({
          useProgressTracking: false,
          fallbackToPolling: true,
        })
      );

      await act(async () => {
        await hook.result.current.generateVideo({
          prompt: 'Error test',
          settings: {
            model: 'minimax-video',
            duration: 5,
            size: '1280x720',
            fps: 30,
            style: 'natural',
            responseFormat: 'url',
          },
        });
      });

      expect(mockUpdateTask).toHaveBeenCalledWith(
        expect.any(String),
        expect.objectContaining({
          status: 'failed',
          error: expect.stringContaining('Server error') as string,
        }) as Partial<VideoTask>
      );
    });
  });

  describe('Task management', () => {
    it('should generate unique task IDs', async () => {
      mockGenerateVideoWithProgress.mockResolvedValue({
        taskId: 'task_unique_123',
      });

      const hook = renderHook(() =>
        useEnhancedVideoGeneration({
          useProgressTracking: true,
        })
      );

      await act(async () => {
        await hook.result.current.generateVideo({
          prompt: 'First video',
          settings: {
            model: 'minimax-video',
            duration: 5,
            size: '1280x720',
            fps: 30,
            style: 'natural',
            responseFormat: 'url',
          },
        });
      });

      const firstCall = mockAddTask.mock.calls[0] as [VideoTask] | undefined;
      const firstTaskCall = firstCall?.[0];
      
      await act(async () => {
        await hook.result.current.generateVideo({
          prompt: 'Second video',
          settings: {
            model: 'minimax-video',
            duration: 5,
            size: '1280x720',
            fps: 30,
            style: 'natural',
            responseFormat: 'url',
          },
        });
      });

      const secondCall = mockAddTask.mock.calls[1] as [VideoTask] | undefined;
      const secondTaskCall = secondCall?.[0];
      
      expect(firstTaskCall?.id).not.toBe(secondTaskCall?.id);
    });

    it('should handle cancellation', async () => {
      const hook = renderHook(() =>
        useEnhancedVideoGeneration({
          useProgressTracking: true,
        })
      );

      // Mock the fetch call for cancellation
      (global.fetch as jest.Mock).mockResolvedValueOnce({
        ok: true,
      });

      await act(async () => {
        await hook.result.current.cancelGeneration('task_cancel_123');
      });

      expect(mockUpdateTask).toHaveBeenCalledWith(
        'task_cancel_123',
        expect.objectContaining({
          status: 'cancelled',
        }) as Partial<VideoTask>
      );

      expect(global.fetch).toHaveBeenCalledWith(
        '/api/videos/tasks/task_cancel_123',
        expect.objectContaining({
          method: 'DELETE',
        })
      );
    });

  });

  describe('Settings validation', () => {
    it('should validate required settings', async () => {
      const hook = renderHook(() =>
        useEnhancedVideoGeneration({
          useProgressTracking: true,
        })
      );

      await act(async () => {
        await hook.result.current.generateVideo({
          prompt: '',
          settings: {
            model: 'minimax-video',
            duration: 5,
            size: '1280x720',
            fps: 30,
            style: 'natural',
            responseFormat: 'url',
          },
        });
      });

      expect(mockSetError).toHaveBeenCalledWith(
        expect.stringContaining('prompt')
      );
    });

    it('should validate duration limits', async () => {
      const hook = renderHook(() =>
        useEnhancedVideoGeneration({
          useProgressTracking: true,
        })
      );

      await act(async () => {
        await hook.result.current.generateVideo({
          prompt: 'Duration test',
          settings: {
            model: 'minimax-video',
            duration: 100, // Too long
            size: '1280x720',
            fps: 30,
            style: 'natural',
            responseFormat: 'url',
          },
        });
      });

      expect(mockSetError).toHaveBeenCalledWith(
        expect.stringContaining('duration')
      );
    });
  });
});