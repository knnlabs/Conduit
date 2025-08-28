import { useVideoStore } from '../useVideoStore';
import * as clientCore from '@/lib/client/coreClient';
import type { VideoStoreState } from '../../types';

// Local VideoProgress interface to avoid broken SDK imports
export interface VideoProgress {
  percentage?: number;
  status?: string;
  message?: string;
}

// Mock dependencies
jest.mock('../useVideoStore');
jest.mock('@/lib/client/coreClient');

export const mockUseVideoStore = jest.mocked(useVideoStore);
export const mockGenerateVideoWithProgress = jest.mocked(clientCore.generateVideoWithProgress);

export const createMockStore = () => {
  const mockAddTask = jest.fn();
  const mockUpdateTask = jest.fn();
  const mockSetError = jest.fn();

  const mockStore: VideoStoreState = {
    addTask: mockAddTask,
    updateTask: mockUpdateTask,
    setError: mockSetError,
    taskHistory: [],
    currentTask: null,
    error: null,
    settings: {
      model: 'minimax-video',
      duration: 5,
      size: '1280x720',
      fps: 30,
      style: 'natural',
      responseFormat: 'url',
    },
    settingsVisible: false,
    updateSettings: jest.fn(),
    toggleSettings: jest.fn(),
    removeTask: jest.fn(),
    clearHistory: jest.fn(),
  };

  return {
    mockStore,
    mockAddTask,
    mockUpdateTask,
    mockSetError,
  };
};

export const setupMocks = () => {
  const storeMocks = createMockStore();
  
  // Add logging to mock functions (for debugging)
  const originalAddTask = storeMocks.mockAddTask;
  storeMocks.mockStore.addTask = (...args: unknown[]) => {
    return originalAddTask(...args);
  };
  
  // Ensure the mock is properly set up every time
  mockUseVideoStore.mockClear();
  mockUseVideoStore.mockReturnValue(storeMocks.mockStore);
  
  // Mock window.fetch for the actual API calls the implementation uses
  (global.fetch as jest.Mock).mockClear();
  (global.fetch as jest.Mock).mockImplementation(() => 
    Promise.resolve({
      ok: true,
      json: () => Promise.resolve({
        task_id: 'mock_task_id',
        status: 'pending',
        progress: 0,
        message: 'Video generation started',
        estimated_time_to_completion: 30,
        created_at: new Date().toISOString(),
        updated_at: new Date().toISOString()
      }),
      headers: {
        entries: () => [['content-type', 'application/json']],
        get: (name: string) => name === 'content-type' ? 'application/json' : null,
      },
      status: 200,
      statusText: 'OK'
    })
  );
  
  // Mock the SDK method (although it's not actually used in current implementation)
  mockGenerateVideoWithProgress.mockResolvedValue({
    taskId: 'mock_task_id',
  });

  // Mock the video SignalR client to always fail connection so it falls back to polling
  jest.doMock('@/lib/client/videoSignalRClient', () => ({
    videoSignalRClient: {
      connect: jest.fn().mockRejectedValue(new Error('Mocked SignalR connection failure')),
      disconnect: jest.fn(),
    },
  }));
  
  return storeMocks;
};