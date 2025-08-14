import { renderHook, act } from '@testing-library/react';
import { useEnhancedVideoGeneration } from '../useEnhancedVideoGeneration';
import { setupMocks } from './useEnhancedVideoGeneration.setup';

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
    it('should validate required settings', async () => {
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

      expect(storeMocks.mockSetError).toHaveBeenCalledWith(
        expect.stringContaining('prompt')
      );
    });

    it('should validate duration limits', async () => {
      const hook = renderHook(() =>
        useEnhancedVideoGeneration()
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

      expect(storeMocks.mockSetError).toHaveBeenCalledWith(
        expect.stringContaining('duration')
      );
    });
  });
});