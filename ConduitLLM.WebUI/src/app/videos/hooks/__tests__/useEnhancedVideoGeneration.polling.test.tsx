import { renderHook, act } from '@testing-library/react';
import { useEnhancedVideoGeneration } from '../useEnhancedVideoGeneration';
import { setupMocks } from './videoTest.helpers';
import type { VideoTask } from '../../types';
import * as browserClientModule from '@/lib/client/browserCoreClient';
import type { VideoProgressCallbacks } from '@knn_labs/conduit-core-client';
import { MediaGenerationStatus } from '@/app/types/media';

// Mock the browser client module
jest.mock('@/lib/client/browserCoreClient');

// Mock the useVideoStore hook directly in this test file
jest.mock('../useVideoStore');

interface MockVideoClient {
  videos: {
    generateWithProgress: jest.Mock;
    cancelTask: jest.Mock;
  };
}

describe('useEnhancedVideoGeneration - Fallback Polling', () => {
  let storeMocks: ReturnType<typeof setupMocks>;
  let mockClient: MockVideoClient;
  let mockGenerateWithProgress: jest.Mock;

  beforeEach(() => {
    jest.clearAllMocks();
    storeMocks = setupMocks();

    // Setup mock client
    mockGenerateWithProgress = jest.fn();
    mockClient = {
      videos: {
        generateWithProgress: mockGenerateWithProgress,
        cancelTask: jest.fn().mockResolvedValue(undefined),
      },
    };

    // Mock the getBrowserCoreClient function
    (browserClientModule.getBrowserCoreClient as jest.Mock).mockResolvedValue(mockClient);

    // Setup default success response
    mockGenerateWithProgress.mockImplementation((request: unknown, callbacks?: VideoProgressCallbacks) => {
      if (callbacks?.onStarted) {
        callbacks.onStarted('mock_task_id', 30);
      }
      return Promise.resolve({
        taskId: 'mock_task_id',
        result: Promise.resolve({
          created: Date.now(),
          data: [{
            url: 'https://example.com/video.mp4',
          }],
          model: (request as { model: string }).model,
        }),
      });
    });
  });

  afterEach(() => {
    jest.restoreAllMocks();
  });

  describe('Fallback polling configuration', () => {
    it('should initialize with fallback polling enabled', () => {
      const hook = renderHook(() =>
        useEnhancedVideoGeneration({
          fallbackToPolling: true,
        })
      );

      expect(hook.result.current.generateVideo).toBeDefined();
      expect(hook.result.current.isGenerating).toBe(false);
      expect(hook.result.current.signalRConnected).toBe(false);
    });

    it('should call addTask when generation starts', async () => {
      const hook = renderHook(() =>
        useEnhancedVideoGeneration({
          fallbackToPolling: true,
        })
      );

      await act(async () => {
        await hook.result.current.generateVideo({
          prompt: 'Polling test',
          settings: {
            model: 'minimax-video',
          },
        });
      });

      expect(storeMocks.mockAddTask).toHaveBeenCalledWith(
        expect.objectContaining({
          prompt: 'Polling test',
          status: MediaGenerationStatus.Pending,
          progress: 0,
        }) as VideoTask
      );

      expect(mockGenerateWithProgress).toHaveBeenCalledWith(
        expect.objectContaining({
          prompt: 'Polling test',
          model: 'minimax-video',
        }),
        expect.any(Object)
      );
    });

    it('should simulate polling behavior through SDK callbacks', async () => {
      // Simulate polling-like behavior through SDK progress callbacks
      let progressUpdateCount = 0;
      mockGenerateWithProgress.mockImplementation((request: unknown, callbacks?: VideoProgressCallbacks) => {
        // Simulate initial start
        if (callbacks?.onStarted) {
          callbacks.onStarted('polling_task_id', 60);
        }
        
        // Simulate periodic progress updates (like polling)
        const interval = setInterval(() => {
          progressUpdateCount++;
          if (callbacks?.onProgress) {
            callbacks.onProgress({
              percentage: Math.min(progressUpdateCount * 20, 100),
              status: progressUpdateCount < 5 ? 'processing' : 'completed',
              message: `Processing: ${progressUpdateCount * 20}%`,
            });
          }
          
          if (progressUpdateCount >= 5) {
            clearInterval(interval);
            if (callbacks?.onCompleted) {
              callbacks.onCompleted({
                created: Date.now(),
                data: [{
                  url: 'https://example.com/final-video.mp4',
                }],
                model: (request as { model: string }).model,
              });
            }
          }
        }, 100);

        return Promise.resolve({
          taskId: 'polling_task_id',
          result: new Promise(resolve => {
            setTimeout(() => {
              resolve({
                created: Date.now(),
                data: [{
                  url: 'https://example.com/final-video.mp4',
                }],
                model: (request as { model: string }).model,
              });
            }, 600);
          }),
        });
      });

      const hook = renderHook(() =>
        useEnhancedVideoGeneration({
          fallbackToPolling: true,
        })
      );

      await act(async () => {
        const promise = hook.result.current.generateVideo({
          prompt: 'Simulated polling test',
          settings: {
            model: 'minimax-video',
          },
        });
        
        // Wait for completion
        await promise;
        await new Promise(resolve => setTimeout(resolve, 700));
      });

      // Verify task was created
      expect(storeMocks.mockAddTask).toHaveBeenCalledWith(
        expect.objectContaining({
          id: 'polling_task_id',
          prompt: 'Simulated polling test',
        })
      );

      // Verify progress updates occurred
      expect(storeMocks.mockUpdateTask).toHaveBeenCalledWith(
        'polling_task_id',
        expect.objectContaining({
          status: MediaGenerationStatus.Completed,
          progress: 100,
        })
      );
    });
  });
});