import { renderHook, act } from '@testing-library/react';
import { useEnhancedVideoGeneration } from '../useEnhancedVideoGeneration';
import { setupMocks, mockGenerateVideoWithProgress, type VideoProgress } from './useEnhancedVideoGeneration.setup';
import type { VideoTask, VideoGenerationResult } from '../../types';

describe('useEnhancedVideoGeneration - Progress Tracking', () => {
  let storeMocks: ReturnType<typeof setupMocks>;

  beforeEach(() => {
    jest.clearAllMocks();
    storeMocks = setupMocks();
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

      expect(storeMocks.mockAddTask).toHaveBeenCalledWith(
        expect.objectContaining({
          prompt: 'Test video with progress',
          status: 'pending',
          progress: 0,
        }) as VideoTask
      );

      expect(mockGenerateVideoWithProgress).toHaveBeenCalled();
      const [requestArg, callbacksArg] = mockGenerateVideoWithProgress.mock.calls[0] as [
        Parameters<typeof mockGenerateVideoWithProgress>[0],
        Parameters<typeof mockGenerateVideoWithProgress>[1]
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
        useEnhancedVideoGeneration()
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

      expect(storeMocks.mockUpdateTask).toHaveBeenLastCalledWith(
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

      expect(storeMocks.mockUpdateTask).toHaveBeenLastCalledWith(
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
        useEnhancedVideoGeneration()
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

      expect(storeMocks.mockUpdateTask).toHaveBeenCalledWith(
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
        useEnhancedVideoGeneration()
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

      expect(storeMocks.mockUpdateTask).toHaveBeenCalledWith(
        expect.any(String),
        expect.objectContaining({
          status: 'failed',
          error: errorMessage,
        }) as Partial<VideoTask>
      );

      expect(storeMocks.mockSetError).toHaveBeenCalledWith(errorMessage);
    });
  });
});