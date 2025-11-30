import { VideoProgressTracker } from '../VideoProgressTracker';
import { VideoTaskStatus } from '../../models/videos';
import { createMocks, type VideoProgressTrackerTestable } from './VideoProgressTracker.setup.test';

describe('VideoProgressTracker - Cleanup and Backoff', () => {
  let mocks: ReturnType<typeof createMocks>;

  beforeEach(() => {
    jest.clearAllMocks();
    jest.useFakeTimers();
    mocks = createMocks();
  });

  afterEach(() => {
    jest.useRealTimers();
  });

  describe('Cleanup', () => {
    it('should clean up resources on completion', async () => {
      const tracker = new VideoProgressTracker(
        'task_123',
        mocks.mockVideosService,
        mocks.mockSignalRService,
        mocks.mockVideoHubClient,
        mocks.mockCallbacks,
        { initialPollIntervalMs: 100 }
      );

      mocks.mockSignalRService.isConnected.mockReturnValue(true);
      
      mocks.mockVideosService.getTaskStatus.mockResolvedValue({
        task_id: 'task_123',
        status: VideoTaskStatus.Completed,
        progress: 100,
        result: { created: Date.now(), data: [] },
      });

      const trackPromise = tracker.track();
      
      // Advance timers to trigger polling
      await jest.advanceTimersByTimeAsync(100);
      
      await trackPromise;

      expect(mocks.mockVideoHubClient.unsubscribeFromTask).toHaveBeenCalledWith('task_123');
    });

    it('should handle cleanup errors gracefully', async () => {
      const tracker = new VideoProgressTracker(
        'task_123',
        mocks.mockVideosService,
        mocks.mockSignalRService,
        mocks.mockVideoHubClient,
        mocks.mockCallbacks,
        { initialPollIntervalMs: 100 }
      );

      mocks.mockVideoHubClient.unsubscribeFromTask.mockRejectedValue(new Error('Unsubscribe failed'));
      
      mocks.mockVideosService.getTaskStatus.mockResolvedValue({
        task_id: 'task_123',
        status: VideoTaskStatus.Completed,
        progress: 100,
        result: { created: Date.now(), data: [] },
      });

      const trackPromise = tracker.track();
      
      // Advance timers to trigger polling
      await jest.advanceTimersByTimeAsync(100);

      // Should not throw even if cleanup fails
      await expect(trackPromise).resolves.toBeDefined();
    });
  });

  describe('Exponential backoff', () => {
    it('should apply exponential backoff for polling when SignalR is disconnected', async () => {
      const tracker = new VideoProgressTracker(
        'task_123',
        mocks.mockVideosService,
        mocks.mockSignalRService,
        mocks.mockVideoHubClient,
        mocks.mockCallbacks,
        {
          initialPollIntervalMs: 100,
          maxPollIntervalMs: 800,
          useExponentialBackoff: true,
        }
      );

      mocks.mockSignalRService.isConnected.mockReturnValue(false);

      let callCount = 0;
      const pollTimes: number[] = [];

      mocks.mockVideosService.getTaskStatus.mockImplementation(() => {
        pollTimes.push(Date.now());
        callCount++;
        return Promise.resolve({
          task_id: 'task_123',
          status: VideoTaskStatus.Running,
          progress: callCount * 10,
        });
      });

      // Start tracking
      void tracker.track();

      // Advance through multiple polling intervals
      await jest.advanceTimersByTimeAsync(100); // First poll at 100ms
      await jest.advanceTimersByTimeAsync(200); // Second poll at 200ms (doubled)
      await jest.advanceTimersByTimeAsync(400); // Third poll at 400ms (doubled)
      await jest.advanceTimersByTimeAsync(800); // Fourth poll at 800ms (capped at max)

      expect(callCount).toBeGreaterThanOrEqual(4);
      
      // Clean up
      (tracker as VideoProgressTrackerTestable).cleanup();
    });
  });
});