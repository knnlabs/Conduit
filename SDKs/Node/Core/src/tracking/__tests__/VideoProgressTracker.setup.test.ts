import { VideoProgressTracker } from '../VideoProgressTracker';
import type { VideosService, VideoProgressCallbacks } from '../../services/VideosService';
import type { SignalRService } from '../../services/SignalRService';
import type { VideoGenerationHubClient } from '../../signalr/VideoGenerationHubClient';
import { VideoTaskStatus } from '../../models/videos';

// Type for accessing private methods in tests
type VideoProgressTrackerTestable = VideoProgressTracker & {
  cleanup(): void;
};

export const createMocks = () => {
  const mockVideosService = {
    getTaskStatus: jest.fn(),
  } as unknown as jest.Mocked<VideosService>;

  const mockSignalRService = {
    isConnected: jest.fn().mockReturnValue(false),
    connect: jest.fn().mockResolvedValue(undefined),
  } as unknown as jest.Mocked<SignalRService>;

  const mockVideoHubClient = {
    subscribeToTask: jest.fn().mockResolvedValue(undefined),
    unsubscribeFromTask: jest.fn().mockResolvedValue(undefined),
    onVideoGenerationProgress: undefined,
    onVideoGenerationCompleted: undefined,
    onVideoGenerationFailed: undefined,
  } as unknown as jest.Mocked<VideoGenerationHubClient>;

  const mockCallbacks: jest.Mocked<VideoProgressCallbacks> = {
    onProgress: jest.fn(),
    onStarted: jest.fn(),
    onCompleted: jest.fn(),
    onFailed: jest.fn(),
  };

  return {
    mockVideosService,
    mockSignalRService,
    mockVideoHubClient,
    mockCallbacks
  };
};

export type { VideoProgressTrackerTestable };