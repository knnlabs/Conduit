import { renderHook, act } from '@testing-library/react';
import { useEnhancedVideoGeneration } from '../useEnhancedVideoGeneration';
import { setupMocks } from './videoTest.helpers';

describe('useEnhancedVideoGeneration - Settings Validation', () => {
  let storeMocks: ReturnType<typeof setupMocks>;

  beforeEach(() => {
    jest.clearAllMocks();
    storeMocks = setupMocks();
  });

  afterEach(() => {
    jest.restoreAllMocks();
  });

  describe('Settings validation', () => {
    it('should handle empty prompt gracefully', async () => {
      const hook = renderHook(() =>
        useEnhancedVideoGeneration()
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

      // Should still make API call with empty prompt - validation handled by backend
      expect(global.fetch).toHaveBeenCalledWith('/api/videos/generate', 
        expect.objectContaining({
          method: 'POST',
        })
      );
    });

    it('should handle long duration gracefully', async () => {
      const hook = renderHook(() =>
        useEnhancedVideoGeneration()
      );

      await act(async () => {
        await hook.result.current.generateVideo({
          prompt: 'Duration test',
          settings: {
            model: 'minimax-video',
            duration: 100, // Long duration
            size: '1280x720',
            fps: 30,
            style: 'natural',
            responseFormat: 'url',
          },
        });
      });

      // Should still make API call - validation handled by backend
      expect(global.fetch).toHaveBeenCalledWith('/api/videos/generate',
        expect.objectContaining({
          method: 'POST',
          body: expect.stringContaining('"duration":100'),
        })
      );
    });
  });
});