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
  mockUseVideoStore.mockReturnValue(storeMocks.mockStore);
  
  // Mock window.fetch for fallback polling
  global.fetch = jest.fn();
  
  return storeMocks;
};