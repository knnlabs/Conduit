import { VideoProgressTracker } from '../VideoProgressTracker';
import { VideoTaskStatus } from '../../models/videos';
import { createMocks, type VideoProgressTrackerTestable } from './VideoProgressTracker.setup.test';

describe('VideoProgressTracker - Progress deduplication', () => {
  let mocks: ReturnType<typeof createMocks>;

  beforeEach(() => {
    jest.clearAllMocks();
    jest.useFakeTimers();
    mocks = createMocks();
  });

  afterEach(() => {
    jest.useRealTimers();
  });

  describe('Progress deduplication', () => {
    it('should deduplicate identical progress updates within time window', async () => {
      const tracker = new VideoProgressTracker(
        'task_123',
        mocks.mockVideosService,
        mocks.mockSignalRService,
        mocks.mockVideoHubClient,
        mocks.mockCallbacks,
        { deduplicationWindowMs: 500 }
      );

      // Mock multiple identical status responses
      mocks.mockVideosService.getTaskStatus.mockResolvedValue({
        task_id: 'task_123',
        status: VideoTaskStatus.Running,
        progress: 50,
        message: 'Processing',
      });

      // Start tracking
      void tracker.track();

      // Trigger multiple polls quickly
      await jest.advanceTimersByTimeAsync(100);
      await jest.advanceTimersByTimeAsync(100);
      await jest.advanceTimersByTimeAsync(100);

      // Should only call onProgress once due to deduplication
      expect(mocks.mockCallbacks.onProgress).toHaveBeenCalledTimes(1);
      expect(mocks.mockCallbacks.onProgress).toHaveBeenCalledWith({
        percentage: 50,
        status: VideoTaskStatus.Running,
        message: 'Processing',
      });

      // Clean up
      (tracker as VideoProgressTrackerTestable).cleanup();
    });

    it('should allow different progress updates', async () => {
      const tracker = new VideoProgressTracker(
        'task_123',
        mocks.mockVideosService,
        mocks.mockSignalRService,
        mocks.mockVideoHubClient,
        mocks.mockCallbacks,
        { initialPollIntervalMs: 100 }
      );

      const statusResponses = [
        { task_id: 'task_123', status: VideoTaskStatus.Running, progress: 30 },
        { task_id: 'task_123', status: VideoTaskStatus.Running, progress: 60 },
        { task_id: 'task_123', status: VideoTaskStatus.Running, progress: 90 },
      ];

      let callCount = 0;
      mocks.mockVideosService.getTaskStatus.mockImplementation(() => {
        if (callCount < statusResponses.length) {
          return Promise.resolve(statusResponses[callCount++]);
        }
        return Promise.resolve(statusResponses[statusResponses.length - 1]);
      });

      // Start tracking
      void tracker.track();

      // Advance through multiple polls
      await jest.advanceTimersByTimeAsync(100);
      await jest.advanceTimersByTimeAsync(100);
      await jest.advanceTimersByTimeAsync(100);

      expect(mocks.mockCallbacks.onProgress).toHaveBeenCalledTimes(3);
      expect(mocks.mockCallbacks.onProgress).toHaveBeenNthCalledWith(1, {
        percentage: 30,
        status: VideoTaskStatus.Running,
        message: undefined,
      });
      expect(mocks.mockCallbacks.onProgress).toHaveBeenNthCalledWith(2, {
        percentage: 60,
        status: VideoTaskStatus.Running,
        message: undefined,
      });
      expect(mocks.mockCallbacks.onProgress).toHaveBeenNthCalledWith(3, {
        percentage: 90,
        status: VideoTaskStatus.Running,
        message: undefined,
      });

      // Clean up
      (tracker as VideoProgressTrackerTestable).cleanup();
    });
  });
});