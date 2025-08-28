import { renderHook, act } from '@testing-library/react';
import { useEnhancedVideoGeneration } from '../useEnhancedVideoGeneration';
import { setupMocks } from './videoTest.helpers';
import type { VideoTask } from '../../types';

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
          id: 'mock_task_id',
        }) as VideoTask
      );

      // Verify the fetch API was called correctly
      expect(global.fetch).toHaveBeenCalledWith('/api/videos/generate', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          prompt: 'Test video with progress',
          model: 'minimax-video',
          duration: 5,
          size: '1280x720',
          fps: 30,
          style: 'natural',
          response_format: 'url',
        }),
      });
    });

    it('should handle progress updates correctly', async () => {
      // Mock the video SignalR client to simulate progress callbacks
      const mockVideoSignalRClient = {
        connect: jest.fn().mockImplementation(async (
          taskId: string, 
          ephemeralKey?: string, 
          callbacks?: {
            onProgress?: (update: { taskId: string; status: string; progress?: number; message?: string }) => void;
            onCompleted?: (videoUrl: string) => void;
            onFailed?: (error: string) => void;
          }
        ) => {
          // Simulate immediate progress update
          setTimeout(() => {
            if (callbacks?.onProgress) {
              callbacks.onProgress({
                taskId,
                percentage: 25,
                status: 'Processing',
                message: 'Initializing',
              } as unknown as { taskId: string; status: string; progress?: number; message?: string });
            }
          }, 0);
          return Promise.resolve();
        }),
        disconnect: jest.fn(),
      };

      // Mock the videoSignalRClient import
      jest.doMock('@/lib/client/videoSignalRClient', () => ({
        videoSignalRClient: mockVideoSignalRClient,
      }));

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

      // Wait for the progress callback to be called
      await act(async () => {
        await new Promise(resolve => setTimeout(resolve, 10));
      });

      expect(storeMocks.mockUpdateTask).toHaveBeenCalledWith(
        'mock_task_id',
        expect.objectContaining({
          progress: 25,
          status: 'running',
          message: 'Initializing',
        }) as unknown as Partial<VideoTask>
      );
    });

    it('should handle successful completion', async () => {
      // Mock the video SignalR client to simulate completion callback
      const mockVideoSignalRClient = {
        connect: jest.fn().mockImplementation(async (
          taskId: string, 
          ephemeralKey?: string, 
          callbacks?: {
            onProgress?: (update: { taskId: string; status: string; progress?: number; message?: string }) => void;
            onCompleted?: (videoUrl: string) => void;
            onFailed?: (error: string) => void;
          }
        ) => {
          // Simulate completion
          setTimeout(() => {
            if (callbacks?.onCompleted) {
              callbacks.onCompleted('https://example.com/video.mp4');
            }
          }, 0);
          return Promise.resolve();
        }),
        disconnect: jest.fn(),
      };

      // Mock the videoSignalRClient import
      jest.doMock('@/lib/client/videoSignalRClient', () => ({
        videoSignalRClient: mockVideoSignalRClient,
      }));

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

      // Wait for the completion callback to be called
      await act(async () => {
        await new Promise(resolve => setTimeout(resolve, 10));
      });

      expect(storeMocks.mockUpdateTask).toHaveBeenCalledWith(
        'mock_task_id',
        expect.objectContaining({
          status: 'completed',
          progress: 100,
          result: {
            created: expect.any(Number) as unknown as number,
            data: [{ url: 'https://example.com/video.mp4' }]
          },
        }) as unknown as Partial<VideoTask>
      );
    });

    it('should handle failures', async () => {
      // Mock the video SignalR client to simulate failure callback
      const mockVideoSignalRClient = {
        connect: jest.fn().mockImplementation(async (
          taskId: string, 
          ephemeralKey?: string, 
          callbacks?: {
            onProgress?: (update: { taskId: string; status: string; progress?: number; message?: string }) => void;
            onCompleted?: (videoUrl: string) => void;
            onFailed?: (error: string) => void;
          }
        ) => {
          // Simulate failure
          setTimeout(() => {
            if (callbacks?.onFailed) {
              callbacks.onFailed('Insufficient GPU resources');
            }
          }, 0);
          return Promise.resolve();
        }),
        disconnect: jest.fn(),
      };

      // Mock the videoSignalRClient import
      jest.doMock('@/lib/client/videoSignalRClient', () => ({
        videoSignalRClient: mockVideoSignalRClient,
      }));

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

      // Wait for the failure callback to be called
      await act(async () => {
        await new Promise(resolve => setTimeout(resolve, 10));
      });

      expect(storeMocks.mockUpdateTask).toHaveBeenCalledWith(
        'mock_task_id',
        expect.objectContaining({
          status: 'failed',
          error: 'Insufficient GPU resources',
        }) as unknown as Partial<VideoTask>
      );

      expect(storeMocks.mockSetError).toHaveBeenCalledWith('Insufficient GPU resources');
    });
  });
});