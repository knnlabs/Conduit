import { renderHook, act } from '@testing-library/react';
import { useEnhancedVideoGeneration } from '../useEnhancedVideoGeneration';
import { setupMocks } from './videoTest.helpers';
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
      // Mock different task IDs for each call
      (global.fetch as jest.Mock)
        .mockResolvedValueOnce({
          ok: true,
          json: jest.fn().mockResolvedValue({
            task_id: 'task_unique_123',
            message: 'First video generation started',
            estimated_time_to_completion: 30
          }),
          headers: new Headers(),
          status: 200,
          statusText: 'OK'
        })
        .mockResolvedValueOnce({
          ok: true,
          json: jest.fn().mockResolvedValue({
            task_id: 'task_unique_456',
            message: 'Second video generation started',
            estimated_time_to_completion: 30
          }),
          headers: new Headers(),
          status: 200,
          statusText: 'OK'
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

      const firstCall = storeMocks.mockAddTask.mock.calls[0] as [VideoTask] | undefined;
      const firstTaskCall = firstCall?.[0];
      
      const secondCall = storeMocks.mockAddTask.mock.calls[1] as [VideoTask] | undefined;
      const secondTaskCall = secondCall?.[0];
      
      expect(firstTaskCall?.id).toBe('task_unique_123');
      expect(secondTaskCall?.id).toBe('task_unique_456');
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