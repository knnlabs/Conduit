import { VideosService } from '../VideosService';
import type { FetchBasedClient } from '../../client/FetchBasedClient';
import type { IFetchBasedClientAdapter } from '../../client/ClientAdapter';
import type { SignalRService } from '../SignalRService';
import type { VideoGenerationHubClient } from '../../signalr/VideoGenerationHubClient';
import { VideoTaskStatus } from '../../models/videos';

// Mock the ClientAdapter
jest.mock('../../client/ClientAdapter', () => ({
  createClientAdapter: jest.fn(() => mockClientAdapter)
}));

// Mock the VideoProgressTracker
jest.mock('../../tracking/VideoProgressTracker', () => ({
  VideoProgressTracker: jest.fn().mockImplementation(() => ({
    track: jest.fn().mockResolvedValue({
      created: Date.now(),
      data: [{ url: 'https://example.com/video.mp4' }],
    })
  }))
}));

const mockClientAdapter: jest.Mocked<IFetchBasedClientAdapter> = {
  get: jest.fn(),
  post: jest.fn(),
  put: jest.fn(),
  patch: jest.fn(),
  delete: jest.fn(),
};

const mockClient = {} as FetchBasedClient;

describe('VideosService', () => {
  let service: VideosService;

  beforeEach(() => {
    jest.clearAllMocks();
    service = new VideosService(mockClient);
  });

  describe('generateAsync', () => {
    it('should successfully generate video asynchronously', async () => {
      const request = {
        prompt: 'A beautiful sunset',
        model: 'minimax-video',
      };

      const expectedResponse = {
        task_id: 'task_123',
        status: VideoTaskStatus.Pending,
        progress: 0,
      };

      mockClientAdapter.post.mockResolvedValueOnce(expectedResponse);

      const result = await service.generateAsync(request);

      expect(mockClientAdapter.post).toHaveBeenCalledWith(
        '/v1/videos/generations/async',
        expect.objectContaining({
          prompt: request.prompt,
          model: request.model,
          n: 1,
        }),
        undefined
      );
      expect(result).toEqual(expectedResponse);
    });

    it('should throw error for invalid request', async () => {
      const request = {
        prompt: '', // Invalid empty prompt
        model: 'minimax-video',
      };

      await expect(service.generateAsync(request)).rejects.toThrow('Prompt is required');
    });
  });

  describe('generateWithProgress', () => {
    let serviceWithSignalR: VideosService;
    
    const mockSignalRService = {
      isConnected: jest.fn(),
      connect: jest.fn(),
    } as unknown as SignalRService;

    const mockVideoHubClient = {
      subscribeToTask: jest.fn(),
      unsubscribeFromTask: jest.fn(),
    } as unknown as VideoGenerationHubClient;

    beforeEach(() => {
      serviceWithSignalR = new VideosService(mockClient, mockSignalRService, mockVideoHubClient);
    });

    it('should generate video with progress callbacks', async () => {
      const request = {
        prompt: 'A futuristic city',
      };

      const mockCallbacks = {
        onProgress: jest.fn(),
        onStarted: jest.fn(),
        onCompleted: jest.fn(),
        onFailed: jest.fn(),
      };

      const taskResponse = {
        task_id: 'task_456',
        status: VideoTaskStatus.Pending,
        progress: 0,
        estimated_time_to_completion: 30,
      };

      mockClientAdapter.post.mockResolvedValueOnce(taskResponse);

      const { taskId, result } = await serviceWithSignalR.generateWithProgress(
        request,
        mockCallbacks
      );

      expect(taskId).toBe('task_456');
      expect(mockCallbacks.onStarted).toHaveBeenCalledWith('task_456', 30);
      expect(result).toBeInstanceOf(Promise);
    });

    it('should handle errors during generation', async () => {
      const request = {
        prompt: 'Test prompt',
      };

      const mockCallbacks = {
        onFailed: jest.fn(),
      };

      const error = new Error('Network error');
      mockClientAdapter.post.mockRejectedValueOnce(error);

      await expect(
        serviceWithSignalR.generateWithProgress(request, mockCallbacks)
      ).rejects.toThrow('Async video generation failed: Network error');

      expect(mockCallbacks.onFailed).toHaveBeenCalledWith('Async video generation failed: Network error', false);
    });
  });

  describe('pollTaskUntilCompletion', () => {
    it('should poll until task completes', async () => {
      const taskId = 'task_789';

      const statusResponses = [
        {
          task_id: taskId,
          status: VideoTaskStatus.Pending,
          progress: 0,
        },
        {
          task_id: taskId,
          status: VideoTaskStatus.Running,
          progress: 50,
        },
        {
          task_id: taskId,
          status: VideoTaskStatus.Completed,
          progress: 100,
          result: {
            created: Date.now(),
            data: [{ url: 'https://example.com/video.mp4' }],
          },
        },
      ];

      let callCount = 0;
      mockClientAdapter.get.mockImplementation(() => {
        return Promise.resolve(statusResponses[callCount++]);
      });

      const result = await service.pollTaskUntilCompletion(taskId, {
        intervalMs: 10, // Fast polling for tests
      });

      expect(result).toEqual(statusResponses[2].result);
      expect(mockClientAdapter.get).toHaveBeenCalledTimes(3);
    });

    it('should call progress callbacks during polling', async () => {
      const taskId = 'task_abc';
      const onProgress = jest.fn();
      const onStarted = jest.fn();
      const onCompleted = jest.fn();

      const statusResponses = [
        {
          task_id: taskId,
          status: VideoTaskStatus.Pending,
          progress: 0,
        },
        {
          task_id: taskId,
          status: VideoTaskStatus.Running,
          progress: 30,
          estimated_time_to_completion: 20,
        },
        {
          task_id: taskId,
          status: VideoTaskStatus.Completed,
          progress: 100,
          result: {
            created: Date.now(),
            data: [{ url: 'https://example.com/video.mp4' }],
          },
        },
      ];

      let callCount = 0;
      mockClientAdapter.get.mockImplementation(() => {
        return Promise.resolve(statusResponses[callCount++]);
      });

      const result = await service.pollTaskUntilCompletion(
        taskId,
        {
          intervalMs: 10,
          onProgress,
          onStarted,
          onCompleted,
        }
      );

      expect(onStarted).toHaveBeenCalledWith(20);
      expect(onProgress).toHaveBeenCalledWith(0, VideoTaskStatus.Pending, undefined);
      expect(onProgress).toHaveBeenCalledWith(30, VideoTaskStatus.Running, undefined);
      expect(onProgress).toHaveBeenCalledWith(100, VideoTaskStatus.Completed, undefined);
      expect(onCompleted).toHaveBeenCalledWith(statusResponses[2].result);
      expect(result).toEqual(statusResponses[2].result);
    });

    it('should handle task failure', async () => {
      const taskId = 'task_fail';
      const onFailed = jest.fn();

      mockClientAdapter.get.mockResolvedValueOnce({
        task_id: taskId,
        status: VideoTaskStatus.Failed,
        progress: 0,
        error: 'Generation failed',
      });

      await expect(
        service.pollTaskUntilCompletion(taskId, {
          intervalMs: 10,
          onFailed,
        })
      ).rejects.toThrow('Task failed: Generation failed');

      expect(onFailed).toHaveBeenCalledWith('Generation failed', false);
    });

    it('should handle timeout', async () => {
      const taskId = 'task_timeout';

      mockClientAdapter.get.mockResolvedValue({
        task_id: taskId,
        status: VideoTaskStatus.Running,
        progress: 50,
      });

      await expect(
        service.pollTaskUntilCompletion(taskId, {
          intervalMs: 10,
          timeoutMs: 50, // Very short timeout
        })
      ).rejects.toThrow('Task polling timed out');
    });

    it('should apply exponential backoff', async () => {
      const taskId = 'task_backoff';

      const statusResponses = [
        { task_id: taskId, status: VideoTaskStatus.Running, progress: 10 },
        { task_id: taskId, status: VideoTaskStatus.Running, progress: 30 },
        { task_id: taskId, status: VideoTaskStatus.Running, progress: 60 },
        {
          task_id: taskId,
          status: VideoTaskStatus.Completed,
          progress: 100,
          result: { created: Date.now(), data: [] },
        },
      ];

      let callCount = 0;
      const delays: number[] = [];
      let lastCallTime = Date.now();

      mockClientAdapter.get.mockImplementation(() => {
        const now = Date.now();
        if (callCount > 0) {
          delays.push(now - lastCallTime);
        }
        lastCallTime = now;
        return Promise.resolve(statusResponses[callCount++]);
      });

      await service.pollTaskUntilCompletion(taskId, {
        intervalMs: 10,
        useExponentialBackoff: true,
        maxIntervalMs: 100,
      });

      // Verify exponential backoff pattern (allowing for timing variations)
      expect(delays.length).toBe(3);
      expect(delays[1]).toBeGreaterThan(delays[0]); // Second delay > first
      expect(delays[2]).toBeGreaterThan(delays[1]); // Third delay > second
    });
  });

  describe('getTaskStatus', () => {
    it('should get task status successfully', async () => {
      const taskId = 'task_status';
      const expectedResponse = {
        task_id: taskId,
        status: VideoTaskStatus.Running,
        progress: 75,
      };

      mockClientAdapter.get.mockResolvedValueOnce(expectedResponse);

      const result = await service.getTaskStatus(taskId);

      expect(mockClientAdapter.get).toHaveBeenCalledWith(
        `/v1/videos/generations/tasks/${taskId}`,
        undefined
      );
      expect(result).toEqual(expectedResponse);
    });

    it('should handle invalid task ID', async () => {
      await expect(service.getTaskStatus('')).rejects.toThrow('Task ID is required');
    });
  });

  describe('cancelTask', () => {
    it('should cancel task successfully', async () => {
      const taskId = 'task_cancel';

      mockClientAdapter.delete.mockResolvedValueOnce(undefined);

      await service.cancelTask(taskId);

      expect(mockClientAdapter.delete).toHaveBeenCalledWith(
        `/v1/videos/generations/${taskId}`,
        undefined
      );
    });

    it('should handle cancellation error', async () => {
      const taskId = 'task_cancel_error';
      const error = new Error('Cannot cancel completed task');

      mockClientAdapter.delete.mockRejectedValueOnce(error);

      await expect(service.cancelTask(taskId)).rejects.toThrow(
        'Failed to cancel task: Cannot cancel completed task'
      );
    });
  });

  describe('getModelCapabilities', () => {
    it('should return capabilities for known models', () => {
      const capabilities = service.getModelCapabilities('minimax-video');

      expect(capabilities).toEqual({
        maxDuration: 6,
        supportedResolutions: expect.arrayContaining(['1280x720', '1920x1080']),
        supportedFps: [24, 30],
        supportsCustomStyles: true,
        supportsSeed: true,
        maxVideos: 1,
      });
    });

    it('should return default capabilities for unknown models', () => {
      const capabilities = service.getModelCapabilities('unknown-model');

      expect(capabilities).toEqual({
        maxDuration: 60,
        supportedResolutions: expect.arrayContaining(['1280x720', '1920x1080', '720x720']),
        supportedFps: [24, 30, 60],
        supportsCustomStyles: true,
        supportsSeed: true,
        maxVideos: 10,
      });
    });
  });
});