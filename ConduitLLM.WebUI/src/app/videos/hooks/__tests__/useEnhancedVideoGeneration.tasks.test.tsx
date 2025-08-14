import { renderHook, act } from '@testing-library/react';
import { useEnhancedVideoGeneration } from '../useEnhancedVideoGeneration';
import { setupMocks, mockGenerateVideoWithProgress } from './useEnhancedVideoGeneration.setup';
import type { VideoTask } from '../../types';

describe('useEnhancedVideoGeneration - Task Management', () => {
  let storeMocks: ReturnType<typeof setupMocks>;

  beforeEach(() => {
    jest.clearAllMocks();
    storeMocks = setupMocks();
  });

  afterEach(() => {
    jest.restoreAllMocks();
  });

  describe('Task management', () => {
    it('should generate unique task IDs', async () => {
      mockGenerateVideoWithProgress.mockResolvedValue({
        taskId: 'task_unique_123',
      });

      const hook = renderHook(() =>
        useEnhancedVideoGeneration()
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

      const firstCall = storeMocks.mockAddTask.mock.calls[0] as [VideoTask] | undefined;
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

      const secondCall = storeMocks.mockAddTask.mock.calls[1] as [VideoTask] | undefined;
      const secondTaskCall = secondCall?.[0];
      
      expect(firstTaskCall?.id).not.toBe(secondTaskCall?.id);
    });

    it('should handle cancellation', async () => {
      const hook = renderHook(() =>
        useEnhancedVideoGeneration()
      );

      // Mock the fetch call for cancellation
      (global.fetch as jest.Mock).mockResolvedValueOnce({
        ok: true,
      });

      await act(async () => {
        await hook.result.current.cancelGeneration('task_cancel_123');
      });

      expect(storeMocks.mockUpdateTask).toHaveBeenCalledWith(
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
});