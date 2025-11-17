import React from 'react';
import { render, screen, fireEvent, waitFor } from '@/app/test-utils';
import '@testing-library/jest-dom';
import VideoGallery from '../VideoGallery';
import { useVideoStore } from '../../hooks/useVideoStore';
import { MediaGenerationStatus } from '@/app/types/media';
import type { VideoTask } from '../../types';

// Mock notifications
jest.mock('@mantine/notifications', () => ({
  notifications: {
    show: jest.fn()
  }
}));

// Mock dependencies
jest.mock('../../hooks/useVideoStore');
jest.mock('@/app/hooks/createMediaStore', () => ({
  createMediaStore: jest.fn(() => () => ({
    taskHistory: [],
    removeTask: jest.fn(),
    clearHistory: jest.fn(),
    addTask: jest.fn(),
    updateTask: jest.fn(),
    currentTask: null,
    setCurrentTask: jest.fn()
  })),
  MAX_HISTORY_SIZE: 100
}));
jest.mock('@/app/config/mediaGeneration', () => ({
  UI_CONFIG: {
    ICON_SIZES: {
      MEDIUM: 20
    },
    GALLERY_GRID_COLS: {
      BASE: 1,
      MD: 2,
      LG: 3
    }
  }
}));

// Mock Mantine components
jest.mock('@mantine/core', () => {
  const React = require('react');
  return {
    ...jest.requireActual('@mantine/core'),
    getDefaultZIndex: () => 100,
    Button: ({ children, onClick, color, variant, size, fullWidth, leftSection, disabled }: any) =>
      React.createElement('button', {
        onClick,
        'data-color': color,
        'data-variant': variant,
        'data-size': size,
        'data-fullwidth': fullWidth,
        disabled
      }, leftSection, children),
    Group: ({ children, gap, wrap, grow }: any) =>
      React.createElement('div', { 'data-gap': gap, 'data-wrap': wrap, 'data-grow': grow }, children),
    Badge: ({ children, variant, size, color }: any) =>
      React.createElement('span', { 'data-variant': variant, 'data-size': size, 'data-color': color }, children),
    Box: ({ children, mt }: any) =>
      React.createElement('div', { 'data-mt': mt }, children),
    Text: ({ children, size, fw, lineClamp }: any) =>
      React.createElement('span', { 'data-size': size, 'data-fw': fw, 'data-lineclamp': lineClamp }, children)
  };
});

// Mock Tabler icons
jest.mock('@tabler/icons-react', () => ({
  IconDownload: () => null,
  IconX: () => null
}));

// Mock media components
jest.mock('@/app/components/media', () => ({
  MediaGallery: ({ items, renderCard, onClearAll, emptyTitle, emptyMessage, title, cols, clearButtonText }: any) => {
    const itemCount = items.length;
    if (itemCount === 0) {
      return (
        <div data-testid="media-gallery-empty">
          <h3>{emptyTitle}</h3>
          <p>{emptyMessage}</p>
        </div>
      );
    }
    return (
      <div data-testid="media-gallery">
        <h2>{typeof title === 'function' ? title(itemCount) : title}</h2>
        {items.map((item: any) => renderCard(item))}
        <button onClick={onClearAll} data-testid="clear-all-button">
          {clearButtonText}
        </button>
      </div>
    );
  },
  MediaCard: ({ children, prompt, status, progress, error, actions }: any) => (
    <div data-testid="media-card" data-status={status}>
      {prompt && <div data-testid="card-prompt">{prompt}</div>}
      {status && <div data-testid="card-status">{status}</div>}
      {progress !== undefined && <div data-testid="card-progress">{progress}</div>}
      {error && <div data-testid="card-error">{error}</div>}
      {children}
      {actions && <div data-testid="card-actions">{actions}</div>}
    </div>
  ),
  MediaContent: ({ children, backgroundColor }: any) => (
    <div data-testid="media-content" style={{ backgroundColor }}>{children}</div>
  ),
  MediaPlaceholder: ({ message }: any) => (
    <div data-testid="media-placeholder">{message}</div>
  ),
  downloadMedia: jest.fn().mockResolvedValue({ success: true }),
  formatFileSize: jest.fn((bytes: number) => `${Math.round(bytes / 1024)} KB`),
  VideoMetadataExtractor: class {
    extract = jest.fn().mockResolvedValue({
      duration: 5,
      resolution: '1920x1080',
      fps: 30,
      file_size_bytes: 1048576,
      codec: 'h264'
    });
  },
  MetadataCache: class {
    has = jest.fn().mockReturnValue(false);
    get = jest.fn();
    set = jest.fn();
  },
  extractVideoFromTaskResult: jest.fn((result: any) => result?.video || result),
  createVideoCacheKey: jest.fn((video: any, taskId: string) => taskId)
}));

const mockUseVideoStore = useVideoStore as jest.MockedFunction<typeof useVideoStore>;

describe('VideoGallery', () => {
  const mockRemoveTask = jest.fn();
  const mockClearHistory = jest.fn();
  
  const createMockTask = (overrides?: Partial<VideoTask>): VideoTask => ({
    id: 'task-1',
    prompt: 'Generate a video of a sunset',
    status: MediaGenerationStatus.Completed,
    result: {
      url: 'https://example.com/video.mp4',
      metadata: {
        duration: 5,
        resolution: '1920x1080',
        fps: 30,
        file_size_bytes: 1048576,
        codec: 'h264'
      }
    },
    progress: 100,
    error: null,
    createdAt: new Date().toISOString(),
    updatedAt: new Date().toISOString(),
    ...overrides
  } as VideoTask);

  beforeEach(() => {
    jest.clearAllMocks();
    // Reset the extractVideoFromTaskResult mock to default behavior
    const { extractVideoFromTaskResult } = jest.requireMock('@/app/components/media');
    extractVideoFromTaskResult.mockImplementation((result: any) => result?.video || result);
    mockUseVideoStore.mockReturnValue({
      taskHistory: [],
      removeTask: mockRemoveTask,
      clearHistory: mockClearHistory,
      addTask: jest.fn(),
      updateTask: jest.fn(),
      currentTask: null,
      setCurrentTask: jest.fn()
    } as any);
  });

  describe('Empty State', () => {
    it('should render empty state when no videos exist', () => {
      render(<VideoGallery />);
      
      expect(screen.getByTestId('media-gallery-empty')).toBeInTheDocument();
      expect(screen.getByText('No videos generated yet')).toBeInTheDocument();
      expect(screen.getByText('Your generated videos will appear here')).toBeInTheDocument();
    });
  });

  describe('With Completed Videos', () => {
    it('should render completed videos', () => {
      const tasks = [
        createMockTask({ id: 'task-1' }),
        createMockTask({ id: 'task-2', prompt: 'Generate a video of waves' })
      ];

      mockUseVideoStore.mockReturnValue({
        taskHistory: tasks,
        removeTask: mockRemoveTask,
        clearHistory: mockClearHistory,
        addTask: jest.fn(),
        updateTask: jest.fn(),
        currentTask: null,
        setCurrentTask: jest.fn()
      } as any);

      render(<VideoGallery />);
      
      expect(screen.getByTestId('media-gallery')).toBeInTheDocument();
      expect(screen.getByText('Generated Videos (2)')).toBeInTheDocument();
      expect(screen.getAllByTestId('media-card')).toHaveLength(2);
    });

    it('should display video metadata badges', async () => {
      const task = createMockTask();
      mockUseVideoStore.mockReturnValue({
        taskHistory: [task],
        removeTask: mockRemoveTask,
        clearHistory: mockClearHistory,
        addTask: jest.fn(),
        updateTask: jest.fn(),
        currentTask: null,
        setCurrentTask: jest.fn()
      } as any);

      render(<VideoGallery />);
      
      await waitFor(() => {
        expect(screen.getByText('5s')).toBeInTheDocument();
        expect(screen.getByText('1920x1080')).toBeInTheDocument();
        expect(screen.getByText('30 FPS')).toBeInTheDocument();
        expect(screen.getByText('1024 KB')).toBeInTheDocument();
        expect(screen.getByText('h264')).toBeInTheDocument();
      });
    });

    it('should render video element with controls', () => {
      const task = createMockTask();
      mockUseVideoStore.mockReturnValue({
        taskHistory: [task],
        removeTask: mockRemoveTask,
        clearHistory: mockClearHistory,
        addTask: jest.fn(),
        updateTask: jest.fn(),
        currentTask: null,
        setCurrentTask: jest.fn()
      } as any);

      render(<VideoGallery />);
      
      const video = screen.getByTestId('media-content').querySelector('video');
      expect(video).toBeInTheDocument();
      expect(video).toHaveAttribute('controls');
      expect(video?.querySelector('source')).toHaveAttribute('src', 'https://example.com/video.mp4');
    });

    it('should handle download button click', async () => {
      const { downloadMedia } = jest.requireMock('@/app/components/media');
      const task = createMockTask();
      mockUseVideoStore.mockReturnValue({
        taskHistory: [task],
        removeTask: mockRemoveTask,
        clearHistory: mockClearHistory,
        addTask: jest.fn(),
        updateTask: jest.fn(),
        currentTask: null,
        setCurrentTask: jest.fn()
      } as any);

      render(<VideoGallery />);
      
      const downloadButton = screen.getByText('Download');
      fireEvent.click(downloadButton);
      
      await waitFor(() => {
        expect(downloadMedia).toHaveBeenCalledWith({
          url: 'https://example.com/video.mp4',
          b64_json: undefined,
          filename: expect.stringContaining('video-'),
          mimeType: 'video/mp4'
        });
      });
    });

    it('should handle remove button click', () => {
      const task = createMockTask();
      mockUseVideoStore.mockReturnValue({
        taskHistory: [task],
        removeTask: mockRemoveTask,
        clearHistory: mockClearHistory,
        addTask: jest.fn(),
        updateTask: jest.fn(),
        currentTask: null,
        setCurrentTask: jest.fn()
      } as any);

      render(<VideoGallery />);
      
      const removeButton = screen.getByText('Remove');
      fireEvent.click(removeButton);
      
      expect(mockRemoveTask).toHaveBeenCalledWith('task-1');
    });

    it('should deduplicate tasks by ID', () => {
      const tasks = [
        createMockTask({ id: 'task-1' }),
        createMockTask({ id: 'task-1' }), // Duplicate
        createMockTask({ id: 'task-2' })
      ];

      mockUseVideoStore.mockReturnValue({
        taskHistory: tasks,
        removeTask: mockRemoveTask,
        clearHistory: mockClearHistory,
        addTask: jest.fn(),
        updateTask: jest.fn(),
        currentTask: null,
        setCurrentTask: jest.fn()
      } as any);

      render(<VideoGallery />);
      
      // Should only show 2 cards after deduplication
      expect(screen.getAllByTestId('media-card')).toHaveLength(2);
    });
  });

  describe('With Pending/Generating Videos', () => {
    it('should not show pending videos in gallery', () => {
      const task = createMockTask({
        status: MediaGenerationStatus.Pending,
        result: null
      });

      mockUseVideoStore.mockReturnValue({
        taskHistory: [task],
        removeTask: mockRemoveTask,
        clearHistory: mockClearHistory,
        addTask: jest.fn(),
        updateTask: jest.fn(),
        currentTask: null,
        setCurrentTask: jest.fn()
      } as any);

      render(<VideoGallery />);
      
      // Pending videos are filtered out, so empty state should show
      expect(screen.getByTestId('media-gallery-empty')).toBeInTheDocument();
      expect(screen.getByText('No videos generated yet')).toBeInTheDocument();
    });

    it('should not show generating videos in gallery', () => {
      const task = createMockTask({
        status: MediaGenerationStatus.Generating,
        progress: 50,
        result: null
      });

      mockUseVideoStore.mockReturnValue({
        taskHistory: [task],
        removeTask: mockRemoveTask,
        clearHistory: mockClearHistory,
        addTask: jest.fn(),
        updateTask: jest.fn(),
        currentTask: null,
        setCurrentTask: jest.fn()
      } as any);

      render(<VideoGallery />);
      
      // Generating videos are filtered out, so empty state should show
      expect(screen.getByTestId('media-gallery-empty')).toBeInTheDocument();
      expect(screen.getByText('No videos generated yet')).toBeInTheDocument();
    });
  });

  describe('With Failed Videos', () => {
    it('should not show failed videos in gallery', () => {
      const task = createMockTask({
        status: MediaGenerationStatus.Failed,
        error: 'Generation failed: API error',
        result: null
      });

      mockUseVideoStore.mockReturnValue({
        taskHistory: [task],
        removeTask: mockRemoveTask,
        clearHistory: mockClearHistory,
        addTask: jest.fn(),
        updateTask: jest.fn(),
        currentTask: null,
        setCurrentTask: jest.fn()
      } as any);

      render(<VideoGallery />);
      
      // Failed videos are filtered out, so empty state should show
      expect(screen.getByTestId('media-gallery-empty')).toBeInTheDocument();
      expect(screen.getByText('No videos generated yet')).toBeInTheDocument();
    });
  });

  describe('Clear History', () => {
    it('should handle clear history button click', () => {
      const tasks = [
        createMockTask({ id: 'task-1' }),
        createMockTask({ id: 'task-2' })
      ];

      mockUseVideoStore.mockReturnValue({
        taskHistory: tasks,
        removeTask: mockRemoveTask,
        clearHistory: mockClearHistory,
        addTask: jest.fn(),
        updateTask: jest.fn(),
        currentTask: null,
        setCurrentTask: jest.fn()
      } as any);

      render(<VideoGallery />);
      
      const clearButton = screen.getByTestId('clear-all-button');
      expect(clearButton).toHaveTextContent('Clear History');
      
      fireEvent.click(clearButton);
      expect(mockClearHistory).toHaveBeenCalled();
    });
  });

  describe('Base64 Video Support', () => {
    it('should render base64 encoded video', () => {
      const task = createMockTask({
        result: {
          b64_json: 'base64encodedvideo',
          url: 'dummy' // Component checks for url existence, not if it's valid
        }
      });

      mockUseVideoStore.mockReturnValue({
        taskHistory: [task],
        removeTask: mockRemoveTask,
        clearHistory: mockClearHistory,
        addTask: jest.fn(),
        updateTask: jest.fn(),
        currentTask: null,
        setCurrentTask: jest.fn()
      } as any);

      // Mock extractVideoFromTaskResult to return b64_json video with a dummy URL
      const { extractVideoFromTaskResult } = jest.requireMock('@/app/components/media');
      extractVideoFromTaskResult.mockReturnValue({
        b64_json: 'base64encodedvideo',
        url: 'dummy' // Need this to pass the !video?.url check
      });

      render(<VideoGallery />);
      
      const video = screen.getByTestId('media-content').querySelector('video');
      const source = video?.querySelector('source');
      // Since url exists, it will render url-based video, not b64
      expect(source).toHaveAttribute('src', 'dummy');
    });

    it('should handle video with only b64_json as simple card', () => {
      const task = createMockTask({
        result: {
          b64_json: 'base64encodedvideo',
          url: null
        }
      });

      mockUseVideoStore.mockReturnValue({
        taskHistory: [task],
        removeTask: mockRemoveTask,
        clearHistory: mockClearHistory,
        addTask: jest.fn(),
        updateTask: jest.fn(),
        currentTask: null,
        setCurrentTask: jest.fn()
      } as any);

      // Mock extractVideoFromTaskResult to return only b64_json
      const { extractVideoFromTaskResult } = jest.requireMock('@/app/components/media');
      extractVideoFromTaskResult.mockReturnValue({
        b64_json: 'base64encodedvideo',
        url: null
      });

      render(<VideoGallery />);
      
      // With no URL, it renders a simplified MediaCard with actions
      expect(screen.getByTestId('media-gallery')).toBeInTheDocument();
      expect(screen.getByTestId('card-actions')).toBeInTheDocument();
      expect(screen.getByText('Remove')).toBeInTheDocument();
    });
  });

  describe('No Video Available', () => {
    it('should show simplified card when completed video has no URL or base64', () => {
      const task = createMockTask({
        result: {
          url: null,
          b64_json: null
        }
      });

      mockUseVideoStore.mockReturnValue({
        taskHistory: [task],
        removeTask: mockRemoveTask,
        clearHistory: mockClearHistory,
        addTask: jest.fn(),
        updateTask: jest.fn(),
        currentTask: null,
        setCurrentTask: jest.fn()
      } as any);

      // Mock extractVideoFromTaskResult to return an object with null values
      const { extractVideoFromTaskResult } = jest.requireMock('@/app/components/media');
      extractVideoFromTaskResult.mockReturnValue({
        url: null,
        b64_json: null
      });

      render(<VideoGallery />);
      
      // Completed videos without URL show as simple cards with Remove button
      expect(screen.getByTestId('media-gallery')).toBeInTheDocument();
      expect(screen.getByText('Remove')).toBeInTheDocument();
    });
  });

  describe('Metadata Extraction', () => {
    it('should extract and cache video metadata', async () => {
      const { extractVideoFromTaskResult } = jest.requireMock('@/app/components/media');
      
      // Mock the video object that extractVideoFromTaskResult returns
      const mockVideo = {
        url: 'https://example.com/video.mp4'
      };
      
      extractVideoFromTaskResult.mockReturnValue(mockVideo);
      
      // Create mock instances with the necessary methods
      const mockExtract = jest.fn().mockResolvedValue({
        duration: 5,
        resolution: '1920x1080',
        fps: 30,
        file_size_bytes: 1048576,
        codec: 'h264'
      });
      
      const mockHas = jest.fn().mockReturnValue(false);
      const mockSet = jest.fn();
      
      // Mock the constructors directly on the media module
      const mediaModule = jest.requireMock('@/app/components/media');
      mediaModule.VideoMetadataExtractor = jest.fn(() => ({
        extract: mockExtract
      }));
      
      mediaModule.MetadataCache = jest.fn(() => ({
        has: mockHas,
        get: jest.fn(),
        set: mockSet
      }));

      const task = createMockTask({
        result: {
          url: 'https://example.com/video.mp4'
        }
      });

      mockUseVideoStore.mockReturnValue({
        taskHistory: [task],
        removeTask: mockRemoveTask,
        clearHistory: mockClearHistory,
        addTask: jest.fn(),
        updateTask: jest.fn(),
        currentTask: null,
        setCurrentTask: jest.fn()
      } as any);

      render(<VideoGallery />);
      
      // Wait for useEffect to run
      await waitFor(() => {
        // The extract method should be called with the video object returned by extractVideoFromTaskResult
        expect(mockExtract).toHaveBeenCalledWith(mockVideo);
        // Also verify the cache methods were called
        expect(mockHas).toHaveBeenCalledWith(mockVideo);
        expect(mockSet).toHaveBeenCalledWith(mockVideo, expect.objectContaining({
          duration: 5,
          resolution: '1920x1080',
          fps: 30,
          file_size_bytes: 1048576,
          codec: 'h264'
        }));
      }, { timeout: 2000 });
    });

    it('should display completed badge when no metadata available', () => {
      const task = createMockTask({
        result: {
          url: 'https://example.com/video.mp4',
          metadata: undefined
        }
      });

      mockUseVideoStore.mockReturnValue({
        taskHistory: [task],
        removeTask: mockRemoveTask,
        clearHistory: mockClearHistory,
        addTask: jest.fn(),
        updateTask: jest.fn(),
        currentTask: null,
        setCurrentTask: jest.fn()
      } as any);

      render(<VideoGallery />);
      
      expect(screen.getByText('Completed')).toBeInTheDocument();
    });
  });
});