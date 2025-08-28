import { renderHook, act } from '@testing-library/react';
import { useEnhancedVideoGeneration } from '../useEnhancedVideoGeneration';
import { setupMocks } from './videoTest.helpers';
import type { VideoTask } from '../../types';

describe('useEnhancedVideoGeneration - Fallback Polling', () => {
  let storeMocks: ReturnType<typeof setupMocks>;

  beforeEach(() => {
    jest.clearAllMocks();
    storeMocks = setupMocks();
  });

  afterEach(() => {
    jest.restoreAllMocks();
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

      expect(storeMocks.mockUpdateTask).toHaveBeenCalledWith(
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

      expect(storeMocks.mockUpdateTask).toHaveBeenCalledWith(
        expect.any(String),
        expect.objectContaining({
          status: 'failed',
          error: expect.stringContaining('Server error') as string,
        }) as Partial<VideoTask>
      );
    });
  });
});