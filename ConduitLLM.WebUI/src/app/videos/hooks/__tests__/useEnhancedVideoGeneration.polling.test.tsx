import { renderHook, act } from '@testing-library/react';
import { useEnhancedVideoGeneration } from '../useEnhancedVideoGeneration';
import { setupMocks } from './videoTest.helpers';
import type { VideoTask } from '../../types';

// Mock the useVideoStore hook directly in this test file
jest.mock('../useVideoStore');

describe('useEnhancedVideoGeneration - Fallback Polling', () => {
  let storeMocks: ReturnType<typeof setupMocks>;

  beforeEach(() => {
    jest.clearAllMocks();
    storeMocks = setupMocks();
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
          prompt: 'Polling test',
          status: 'pending',
          progress: 0,
        }) as VideoTask
      );

      expect(global.fetch).toHaveBeenCalledWith(
        '/api/videos/generate',
        expect.any(Object)
      );
    });
  });
});