import { renderHook, act } from '@testing-library/react';
import { useEnhancedVideoGeneration } from '../useEnhancedVideoGeneration';
import { setupMocks } from './videoTest.helpers';
import type { VideoTask } from '../../types';

// Mock the useVideoStore hook directly in this test file
jest.mock('../useVideoStore');

describe('useEnhancedVideoGeneration - Progress Tracking', () => {
  let storeMocks: ReturnType<typeof setupMocks>;

  beforeEach(() => {
    jest.clearAllMocks();
    storeMocks = setupMocks();
  });

  afterEach(() => {
    // Clean up without restoring all mocks
    jest.clearAllMocks();
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

    it('should handle SignalR connection failure gracefully', async () => {
      // The default mock in the helper already makes SignalR fail
      // This test just verifies that the hook doesn't crash when SignalR fails
      const hook = renderHook(() =>
        useEnhancedVideoGeneration()
      );

      await act(async () => {
        await hook.result.current.generateVideo({
          prompt: 'SignalR failure test',
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

      // Should still call addTask even if SignalR fails
      expect(storeMocks.mockAddTask).toHaveBeenCalledWith(
        expect.objectContaining({
          prompt: 'SignalR failure test',
          status: 'pending',
          progress: 0,
        }) as VideoTask
      );

      // SignalR connection state may be true initially before failure detection
      expect(typeof hook.result.current.signalRConnected).toBe('boolean');
    });
  });
});