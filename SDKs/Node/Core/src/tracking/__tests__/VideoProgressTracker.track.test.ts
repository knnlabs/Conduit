import { VideoProgressTracker } from '../VideoProgressTracker';
import { VideoTaskStatus } from '../../models/videos';
import { createMocks, type VideoProgressTrackerTestable } from './VideoProgressTracker.setup.test';

describe('VideoProgressTracker - track', () => {
  let mocks: ReturnType<typeof createMocks>;

  beforeEach(() => {
    jest.clearAllMocks();
    jest.useFakeTimers();
    mocks = createMocks();
  });

  afterEach(() => {
    jest.useRealTimers();
  });

  describe('track', () => {
    it('should set up SignalR connection when available', async () => {
      const tracker = new VideoProgressTracker(
        'task_123',
        mocks.mockVideosService,
        mocks.mockSignalRService,
        mocks.mockVideoHubClient,
        mocks.mockCallbacks
      );

      mocks.mockSignalRService.isConnected.mockReturnValue(false);

      // Start tracking in background
      void tracker.track();

      // Allow setup to complete
      await jest.runOnlyPendingTimersAsync();

      expect(mocks.mockSignalRService.connect).toHaveBeenCalled();
      expect(mocks.mockVideoHubClient.subscribeToTask).toHaveBeenCalledWith('task_123');

      // Clean up
      (tracker as VideoProgressTrackerTestable).cleanup();
    });

    it('should handle SignalR connection failure gracefully', async () => {
      const tracker = new VideoProgressTracker(
        'task_123',
        mocks.mockVideosService,
        mocks.mockSignalRService,
        mocks.mockVideoHubClient,
        mocks.mockCallbacks,
        { initialPollIntervalMs: 100 }
      );

      mocks.mockSignalRService.connect.mockRejectedValue(new Error('Connection failed'));

      // Mock task status for polling
      mocks.mockVideosService.getTaskStatus.mockResolvedValue({
        task_id: 'task_123',
        status: VideoTaskStatus.Completed,
        progress: 100,
        result: {
          created: Date.now(),
          data: [{ url: 'https://example.com/video.mp4' }],
        },
      });

      const trackPromise = tracker.track();
      
      // Advance timers to trigger polling
      await jest.advanceTimersByTimeAsync(100);

      const result = await trackPromise;

      expect(result).toEqual({
        created: expect.any(Number),
        data: [{ url: 'https://example.com/video.mp4' }],
      });
    });

    it('should start polling as fallback', async () => {
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
        {
          task_id: 'task_123',
          status: VideoTaskStatus.Completed,
          progress: 100,
          result: { created: Date.now(), data: [] },
        },
      ];

      let callCount = 0;
      mocks.mockVideosService.getTaskStatus.mockImplementation(() => {
        if (callCount < statusResponses.length) {
          return Promise.resolve(statusResponses[callCount++]);
        }
        return Promise.resolve(statusResponses[statusResponses.length - 1]);
      });

      const trackPromise = tracker.track();

      // Advance timers to trigger polling
      await jest.advanceTimersByTimeAsync(100);
      await jest.advanceTimersByTimeAsync(100);
      await jest.advanceTimersByTimeAsync(100);

      const result = await trackPromise;

      expect(mocks.mockCallbacks.onProgress).toHaveBeenCalledWith({
        percentage: 30,
        status: VideoTaskStatus.Running,
        message: undefined,
      });
      expect(mocks.mockCallbacks.onProgress).toHaveBeenCalledWith({
        percentage: 60,
        status: VideoTaskStatus.Running,
        message: undefined,
      });
      expect(result).toEqual(statusResponses[2].result);
    });

    it('should handle timeout correctly', async () => {
      const tracker = new VideoProgressTracker(
        'task_123',
        mocks.mockVideosService,
        mocks.mockSignalRService,
        mocks.mockVideoHubClient,
        mocks.mockCallbacks,
        { timeoutMs: 1000, initialPollIntervalMs: 100 }
      );

      mocks.mockVideosService.getTaskStatus.mockResolvedValue({
        task_id: 'task_123',
        status: VideoTaskStatus.Running,
        progress: 50,
      });

      const trackPromise = tracker.track();

      // Advance past timeout
      jest.advanceTimersByTime(1100);

      await expect(trackPromise).rejects.toThrow('Video generation tracking timed out');
    });
  });
});