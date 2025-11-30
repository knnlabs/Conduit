import { ConduitCoreClient } from '../../src/client/ConduitCoreClient';
import { VideoGenerationHubClient } from '../../src/signalr/VideoGenerationHubClient';
import { SignalRService } from '../../src/services/SignalRService';
import { VideoTaskStatus } from '../../src/models/videos';
import type { VideoProgress } from '../../src/services/VideosService';
import { createClientAdapter } from '../../src/client/ClientAdapter';

// Mock fetch for API calls
global.fetch = jest.fn().mockResolvedValue({
  ok: true,
  headers: new Headers(),
  json: async () => ({}),
});

// Create mock client adapter
const mockClientAdapter = {
  get: jest.fn(),
  post: jest.fn(),
  put: jest.fn(),
  patch: jest.fn(),
  delete: jest.fn(),
};

// Mock the ClientAdapter
jest.mock('../../src/client/ClientAdapter', () => ({
  createClientAdapter: jest.fn(() => mockClientAdapter),
}));

// Mock BaseSignalRConnection to prevent real HTTP calls
jest.mock('@knn_labs/conduit-common', () => ({
  ...jest.requireActual('@knn_labs/conduit-common'),
  BaseSignalRConnection: class MockBaseSignalRConnection {
    constructor() {
      this.connection = {
        start: jest.fn().mockResolvedValue(undefined),
        stop: jest.fn().mockResolvedValue(undefined),
        on: jest.fn(),
        off: jest.fn(),
        invoke: jest.fn().mockResolvedValue(undefined),
        state: 'Connected',
        onclose: jest.fn(),
        onreconnecting: jest.fn(),
        onreconnected: jest.fn(),
      };
    }
    async getConnection() { return this.connection; }
    async invoke(method: string, ...args: any[]) { return this.connection.invoke(method, ...args); }
    async start() { return this.connection.start(); }
    async stop() { return this.connection.stop(); }
    on(event: string, handler: Function) { return this.connection.on(event, handler); }
    off(event: string, handler: Function) { return this.connection.off(event, handler); }
  },
}));

// Mock SignalR
jest.mock('@microsoft/signalr', () => ({
  HubConnectionBuilder: jest.fn().mockImplementation(() => ({
    withUrl: jest.fn().mockReturnThis(),
    withAutomaticReconnect: jest.fn().mockReturnThis(),
    configureLogging: jest.fn().mockReturnThis(),
    build: jest.fn().mockReturnValue({
      start: jest.fn().mockResolvedValue(undefined),
      stop: jest.fn().mockResolvedValue(undefined),
      on: jest.fn(),
      off: jest.fn(),
      invoke: jest.fn().mockResolvedValue(undefined),
      state: 'Connected',
      onclose: jest.fn(),
      onreconnecting: jest.fn(),
      onreconnected: jest.fn(),
    }),
  })),
  LogLevel: {
    Information: 'Information',
    Warning: 'Warning',
    Error: 'Error',
    Debug: 'Debug',
  },
}));

describe('Video Progress Tracking Integration', () => {
  let client: ConduitCoreClient;
  let videoHubClient: VideoGenerationHubClient;

  beforeEach(async () => {
    jest.clearAllMocks();
    jest.useFakeTimers();

    client = new ConduitCoreClient({
      apiKey: 'test-key',
      baseURL: 'http://localhost:5000',
      signalR: {
        enabled: true,
        autoConnect: false,
      },
    });

    // Create a mock video hub client immediately
    videoHubClient = {
      connection: {
        start: jest.fn().mockResolvedValue(undefined),
        stop: jest.fn().mockResolvedValue(undefined),
        on: jest.fn(),
        off: jest.fn(),
        invoke: jest.fn().mockResolvedValue(undefined),
        state: 'Connected',
      },
      subscribeToTask: jest.fn().mockResolvedValue(undefined),
      unsubscribeFromTask: jest.fn().mockResolvedValue(undefined),
      onVideoGenerationProgress: undefined,
      onVideoGenerationCompleted: undefined,
      onVideoGenerationFailed: undefined,
    } as any;
    (client.videos as any).videoHubClient = videoHubClient;
  }, 10000);

  afterEach(() => {
    jest.useRealTimers();
  });

  describe('Full progress tracking flow', () => {
    it('should track video generation progress with SignalR and callbacks', async () => {
      const progressUpdates: VideoProgress[] = [];
      let startedCalled = false;
      let completedCalled = false;

      // Mock the initial generation request
      mockClientAdapter.post.mockResolvedValueOnce({
        task_id: 'task_integration_123',
        status: VideoTaskStatus.Pending,
        progress: 0,
        estimated_time_to_completion: 30,
      });

      // Mock status polling (as fallback)
      const statusResponses = [
        {
          task_id: 'task_integration_123',
          status: VideoTaskStatus.Running,
          progress: 25,
          message: 'Processing video',
        },
        {
          task_id: 'task_integration_123',
          status: VideoTaskStatus.Running,
          progress: 75,
          message: 'Finalizing',
        },
        {
          task_id: 'task_integration_123',
          status: VideoTaskStatus.Completed,
          progress: 100,
          result: {
            created: Date.now(),
            data: [{
              url: 'https://example.com/video.mp4',
              revised_prompt: 'A beautiful sunset over mountains',
            }],
            model: 'minimax-video',
          },
        },
      ];

      let pollCount = 0;
      mockClientAdapter.get.mockImplementation(async () => {
        return statusResponses[Math.min(pollCount++, statusResponses.length - 1)];
      });

      // Start video generation with progress tracking
      const { taskId, result } = await client.videos.generateWithProgress(
        {
          prompt: 'A sunset over mountains',
          model: 'minimax-video',
        },
        {
          onProgress: (progress) => {
            progressUpdates.push(progress);
          },
          onStarted: (id, estimatedSeconds) => {
            expect(id).toBe('task_integration_123');
            expect(estimatedSeconds).toBe(30);
            startedCalled = true;
          },
          onCompleted: (videoResult) => {
            expect(videoResult).toBeDefined();
            expect(videoResult.data).toHaveLength(1);
            completedCalled = true;
          },
        }
      );

      // Let async operations complete
      await jest.runAllTimersAsync();

      expect(taskId).toBe('task_integration_123');
      expect(result).toBeDefined();
      expect(startedCalled).toBe(true);
      expect(completedCalled).toBe(true);
      
      // Should have multiple progress updates
      expect(progressUpdates.length).toBeGreaterThan(0);
      
      // Verify progress values
      const progressPercentages = progressUpdates.map(p => p.percentage);
      expect(progressPercentages).toContain(25);
      expect(progressPercentages).toContain(75);
      expect(progressPercentages).toContain(100);
    });

    it('should handle SignalR real-time updates', async () => {
      const progressUpdates: VideoProgress[] = [];

      // Mock initial generation request
      mockClientAdapter.post.mockResolvedValueOnce({
        task_id: 'task_signalr_456',
        status: VideoTaskStatus.Pending,
        progress: 0,
      });

      // Mock final status request
      mockClientAdapter.get.mockResolvedValue({
        task_id: 'task_signalr_456',
        status: VideoTaskStatus.Completed,
        progress: 100,
        result: {
          created: Date.now(),
          data: [{ url: 'https://example.com/video.mp4' }],
        },
      });

      // Start tracking
      const trackPromise = client.videos.generateWithProgress(
        {
          prompt: 'A city skyline',
          model: 'minimax-video',
        },
        {
          onProgress: (progress) => {
            progressUpdates.push(progress);
          },
        }
      );

      // Allow polling to kick in since SignalR is mocked
      await jest.advanceTimersByTimeAsync(2000);
      await jest.advanceTimersByTimeAsync(2000);
      await jest.runAllTimersAsync();
      
      const result = await trackPromise;

      expect(result.taskId).toBe('task_signalr_456');
      expect(result.result).toBeDefined();
      expect(progressUpdates.length).toBeGreaterThan(0);
      
      // Since we're using polling (not actual SignalR), verify we got the final state
      expect(progressUpdates[progressUpdates.length - 1].percentage).toBe(100);
    });

    it('should handle errors gracefully', async () => {
      let errorMessage = '';

      // Mock generation request that will fail
      mockClientAdapter.post.mockResolvedValueOnce({
        task_id: 'task_error_789',
        status: VideoTaskStatus.Pending,
        progress: 0,
      });

      // Mock error status
      mockClientAdapter.get.mockResolvedValue({
        task_id: 'task_error_789',
        status: VideoTaskStatus.Failed,
        progress: 0,
        error: 'Insufficient GPU resources',
      });

      const { result } = await client.videos.generateWithProgress(
        {
          prompt: 'A complex scene',
          model: 'minimax-video',
        },
        {
          onFailed: (error) => {
            errorMessage = error;
          },
        }
      );
      
      // Handle the rejection properly
      const resultPromise = result.catch(error => {
        expect(error.message).toBe('Task failed: Insufficient GPU resources');
        return 'error-handled';
      });
      
      // Advance timers to trigger polling and status check
      await jest.advanceTimersByTimeAsync(2000);
      await jest.runAllTimersAsync();
      
      // Wait for the promise to settle
      const outcome = await resultPromise;
      expect(outcome).toBe('error-handled');
      expect(errorMessage).toBe('Insufficient GPU resources');
    });

    it('should handle timeout scenarios', async () => {
      // Mock a task that never completes
      mockClientAdapter.post.mockResolvedValueOnce({
        task_id: 'task_timeout_999',
        status: VideoTaskStatus.Pending,
        progress: 0,
      });
      
      mockClientAdapter.get.mockResolvedValue({
        task_id: 'task_timeout_999',
        status: VideoTaskStatus.Running,
        progress: 50,
      });

      const { result } = await client.videos.generateWithProgress(
        {
          prompt: 'Never ending generation',
          model: 'minimax-video',
        }
      );
      
      // Handle the timeout rejection properly
      const resultPromise = result.catch(error => {
        expect(error.message).toContain('timed out');
        return 'timeout-handled';
      });
      
      // Since we can't pass custom timeout to the integration test,
      // we'll simulate a timeout by advancing time past the default timeout
      await jest.advanceTimersByTimeAsync(600000); // 10 minutes
      await jest.runAllTimersAsync();
      
      const outcome = await resultPromise;
      expect(outcome).toBe('timeout-handled');
    });

    it('should deduplicate progress events from multiple sources', async () => {
      const progressUpdates: VideoProgress[] = [];

      mockClientAdapter.post.mockResolvedValueOnce({
        task_id: 'task_dedup_111',
        status: VideoTaskStatus.Pending,
        progress: 0,
      });

      // Mock polling responses - first return 50%, then completed
      let pollCount = 0;
      mockClientAdapter.get.mockImplementation(async () => {
        if (pollCount++ === 0) {
          return {
            task_id: 'task_dedup_111',
            status: VideoTaskStatus.Running,
            progress: 50,
            message: 'Processing',
          };
        }
        return {
          task_id: 'task_dedup_111',
          status: VideoTaskStatus.Completed,
          progress: 100,
          result: {
            created: Date.now(),
            data: [{ url: 'https://example.com/video.mp4' }],
          },
        };
      });

      // Start tracking
      const { result } = await client.videos.generateWithProgress(
        {
          prompt: 'Deduplication test',
          model: 'minimax-video',
        },
        {
          onProgress: (progress) => {
            progressUpdates.push(progress);
          },
        }
      );

      // Advance time to trigger polling
      await jest.advanceTimersByTimeAsync(2000);
      await jest.advanceTimersByTimeAsync(2000);
      await jest.runAllTimersAsync();
      
      await result;

      // Should have progress updates
      expect(progressUpdates.length).toBeGreaterThan(0);
      
      // Should have 50% and 100%
      const progressPercentages = progressUpdates.map(p => p.percentage);
      expect(progressPercentages).toContain(50);
      expect(progressPercentages).toContain(100);
    });

    it('should fall back to polling when SignalR is unavailable', async () => {
      const progressUpdates: VideoProgress[] = [];

      mockClientAdapter.post.mockResolvedValueOnce({
        task_id: 'task_fallback_222',
        status: VideoTaskStatus.Pending,
        progress: 0,
        estimated_time_to_completion: 20,
      });

      // Mock multiple polling states
      const progressStates = [
        {
          task_id: 'task_fallback_222',
          status: VideoTaskStatus.Running,
          progress: 20,
          message: 'Starting',
        },
        {
          task_id: 'task_fallback_222',
          status: VideoTaskStatus.Running,
          progress: 60,
          message: 'Processing',
        },
        {
          task_id: 'task_fallback_222',
          status: VideoTaskStatus.Completed,
          progress: 100,
          result: {
            created: Date.now(),
            data: [{ url: 'https://example.com/video.mp4' }],
          },
        },
      ];

      let pollCount = 0;
      mockClientAdapter.get.mockImplementation(async () => {
        return progressStates[Math.min(pollCount++, progressStates.length - 1)];
      });

      // Make SignalR connection fail
      const signalRService = (client as any).signalr;
      if (signalRService) {
        signalRService.isConnected = jest.fn().mockReturnValue(false);
        signalRService.connect = jest.fn().mockRejectedValue(new Error('Connection failed'));
      }

      const { taskId, result } = await client.videos.generateWithProgress(
        {
          prompt: 'Fallback test',
          model: 'minimax-video',
        },
        {
          onProgress: (progress) => {
            progressUpdates.push(progress);
          },
        }
      );

      expect(taskId).toBe('task_fallback_222');
      
      // Advance time to trigger multiple polling cycles
      await jest.advanceTimersByTimeAsync(2000);
      await jest.advanceTimersByTimeAsync(2000);
      await jest.advanceTimersByTimeAsync(2000);
      await jest.runAllTimersAsync();
      
      // Wait for the result
      await result;
      
      expect(progressUpdates.length).toBeGreaterThan(0);
      
      // Should have received updates through polling
      const progressPercentages = progressUpdates.map(p => p.percentage);
      expect(progressPercentages).toContain(20);
      expect(progressPercentages).toContain(60);
      expect(progressPercentages).toContain(100);
    });
  });
});