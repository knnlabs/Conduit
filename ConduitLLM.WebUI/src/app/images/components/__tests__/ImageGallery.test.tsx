import React from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import '@testing-library/jest-dom';
import ImageGallery from '../ImageGallery';
import { useImageStore } from '../../hooks/useImageStore';
import { MediaGenerationStatus } from '@/app/types/media';

// Mock the dependencies
jest.mock('../../hooks/useImageStore');
jest.mock('@/app/components/media', () => ({
  MediaGallery: ({ items, renderCard, onClearAll, emptyTitle, emptyMessage, title }: any) => (
    <div data-testid="media-gallery">
      <h2>{typeof title === 'function' ? title(items.length) : title}</h2>
      {items.length === 0 ? (
        <div>
          <p>{emptyTitle}</p>
          <p>{emptyMessage}</p>
        </div>
      ) : (
        items.map((item: any) => (
          <div key={item.url || Math.random()}>
            {renderCard(item)}
          </div>
        ))
      )}
      {items.length > 0 && (
        <button onClick={onClearAll}>Clear All</button>
      )}
    </div>
  ),
  MediaCard: ({ children, ...props }: any) => (
    <div data-testid="media-card" {...props}>{children}</div>
  ),
  downloadMedia: jest.fn().mockResolvedValue(undefined),
  formatFileSize: jest.fn((bytes: number) => `${bytes} bytes`),
  ImageMetadataExtractor: jest.fn().mockImplementation(() => ({
    extract: jest.fn().mockResolvedValue({
      width: 1024,
      height: 768,
      sizeBytes: 1024000,
      format: 'jpeg'
    })
  })),
  MetadataCache: jest.fn().mockImplementation(() => ({
    has: jest.fn().mockReturnValue(false),
    get: jest.fn().mockReturnValue(null),
    set: jest.fn()
  }))
}));

const mockUseImageStore = useImageStore as jest.MockedFunction<typeof useImageStore>;

describe('ImageGallery', () => {
  const defaultMockStore = {
    currentResults: [],
    status: MediaGenerationStatus.Idle,
    clearResults: jest.fn(),
    taskHistory: [],
    removeTask: jest.fn(),
    clearHistory: jest.fn()
  };

  beforeEach(() => {
    jest.clearAllMocks();
    mockUseImageStore.mockReturnValue(defaultMockStore as any);
  });

  describe('Empty State', () => {
    it('should render empty state when no results', () => {
      render(<ImageGallery />);
      
      expect(screen.getByText('No images generated yet')).toBeInTheDocument();
      expect(screen.getByText('Your generated images will appear here')).toBeInTheDocument();
    });

    it('should display correct title with count', () => {
      render(<ImageGallery />);
      
      expect(screen.getByText('Generated Images (0)')).toBeInTheDocument();
    });
  });

  describe('With Results', () => {
    const mockResults = [
      {
        url: 'https://example.com/image1.jpg',
        revised_prompt: 'A beautiful landscape'
      },
      {
        url: 'https://example.com/image2.jpg',
        revised_prompt: 'A stunning sunset'
      }
    ];

    beforeEach(() => {
      mockUseImageStore.mockReturnValue({
        ...defaultMockStore,
        currentResults: mockResults
      } as any);
    });

    it('should render images when results exist', () => {
      render(<ImageGallery />);
      
      expect(screen.getByText('Generated Images (2)')).toBeInTheDocument();
      expect(screen.getAllByTestId('media-card')).toHaveLength(2);
    });

    it('should display image prompts', () => {
      render(<ImageGallery />);
      
      expect(screen.getByText('A beautiful landscape')).toBeInTheDocument();
      expect(screen.getByText('A stunning sunset')).toBeInTheDocument();
    });

    it('should handle download button click', async () => {
      const { downloadMedia } = require('@/app/components/media');
      render(<ImageGallery />);
      
      const downloadButtons = screen.getAllByText('Download');
      fireEvent.click(downloadButtons[0]);
      
      await waitFor(() => {
        expect(downloadMedia).toHaveBeenCalledWith({
          url: 'https://example.com/image1.jpg',
          b64_json: undefined,
          filename: expect.stringContaining('image-'),
          mimeType: 'image/jpeg'
        });
      });
    });

    it('should handle remove button click', () => {
      render(<ImageGallery />);
      
      const removeButtons = screen.getAllByText('Remove');
      fireEvent.click(removeButtons[0]);
      
      expect(defaultMockStore.clearResults).toHaveBeenCalled();
    });

    it('should handle clear all button', () => {
      render(<ImageGallery />);
      
      const clearButton = screen.getByText('Clear All');
      fireEvent.click(clearButton);
      
      expect(defaultMockStore.clearResults).toHaveBeenCalled();
    });
  });

  describe('Loading State', () => {
    it('should display loading state when generating', () => {
      mockUseImageStore.mockReturnValue({
        ...defaultMockStore,
        status: MediaGenerationStatus.Generating
      } as any);
      
      render(<ImageGallery />);
      
      expect(screen.getByText('Generating images...')).toBeInTheDocument();
    });
  });

  describe('Error State', () => {
    it('should not show specific error UI in gallery', () => {
      mockUseImageStore.mockReturnValue({
        ...defaultMockStore,
        status: MediaGenerationStatus.Failed
      } as any);
      
      render(<ImageGallery />);
      
      // Gallery should still render normally, error handling is in other components
      expect(screen.getByText('No images generated yet')).toBeInTheDocument();
    });
  });

  describe('Metadata Extraction', () => {
    it('should extract and display metadata for images', async () => {
      const mockResultsWithMetadata = [
        {
          url: 'https://example.com/image1.jpg',
          revised_prompt: 'A beautiful landscape'
        }
      ];

      mockUseImageStore.mockReturnValue({
        ...defaultMockStore,
        currentResults: mockResultsWithMetadata
      } as any);

      render(<ImageGallery />);

      await waitFor(() => {
        expect(screen.getByText('1024 x 768')).toBeInTheDocument();
        expect(screen.getByText('1024000 bytes')).toBeInTheDocument();
      });
    });
  });

  describe('Accessibility', () => {
    it('should have accessible image elements', () => {
      const mockResults = [
        {
          url: 'https://example.com/image1.jpg',
          revised_prompt: 'A beautiful landscape'
        }
      ];

      mockUseImageStore.mockReturnValue({
        ...defaultMockStore,
        currentResults: mockResults
      } as any);

      render(<ImageGallery />);
      
      const images = screen.getAllByRole('img');
      expect(images[0]).toHaveAttribute('alt', 'A beautiful landscape');
    });
  });
});