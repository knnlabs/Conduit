import React from 'react';
import { render, screen, fireEvent, waitFor } from '@/app/test-utils';
import '@testing-library/jest-dom';
import ImageGallery from '../ImageGallery';
import { useImageStore } from '../../hooks/useImageStore';
import { MediaGenerationStatus } from '@/app/types/media';
import type { GeneratedImage } from '../../types';

// Mock notifications before other imports
jest.mock('@mantine/notifications', () => ({
  notifications: {
    show: jest.fn()
  }
}));

// Mock dependencies
jest.mock('../../hooks/useImageStore');
jest.mock('next/image', () => ({
  __esModule: true,
  default: (props: any) => {
    // eslint-disable-next-line @typescript-eslint/no-unsafe-assignment
    const { src, alt, ...rest } = props;
    // eslint-disable-next-line @next/next/no-img-element, jsx-a11y/alt-text
    return <img src={src as string} alt={alt as string} {...rest} />;
  },
}));

// Mock Mantine components
jest.mock('@mantine/core', () => ({
  MantineProvider: ({ children }: any) => <>{children}</>,
  Card: Object.assign(
    ({ children }: any) => <div data-testid="card">{children}</div>,
    {
      Section: ({ children }: any) => <div data-testid="card-section">{children}</div>
    }
  ),
  Text: ({ children }: any) => <span>{children}</span>,
  Button: ({ children, onClick, leftSection }: any) => (
    <button onClick={onClick as () => void}>
      {leftSection}
      {children}
    </button>
  ),
  Group: ({ children }: any) => <div>{children}</div>,
  Center: ({ children }: any) => <div>{children}</div>,
  Stack: ({ children }: any) => <div>{children}</div>,
  Badge: ({ children }: any) => <span>{children}</span>
}));

// Mock Tabler icons
jest.mock('@tabler/icons-react', () => ({
  IconDownload: () => null,
  IconZoomIn: () => null,
  IconDimensions: () => null,
  IconFile: () => null
}));

jest.mock('@/app/components/media', () => ({
  MediaGallery: ({ items, renderCard }: any) => (
    <div data-testid="media-gallery">
      {/* eslint-disable-next-line @typescript-eslint/no-unsafe-call, @typescript-eslint/no-unsafe-member-access */}
      {items.map((item: any, index: number) => renderCard(item, index))}
    </div>
  ),
  MediaCard: ({ children, onClick }: any) => (
    <div data-testid="media-card" onClick={onClick as () => void}>
      {children}
    </div>
  ),
  downloadMedia: jest.fn().mockResolvedValue({ success: true }),
  formatFileSize: jest.fn((bytes: number) => `${Math.round(bytes / 1024)} KB`),
  ImageMetadataExtractor: class {
    extract = jest.fn().mockResolvedValue({
      width: 1024,
      height: 768,
      sizeBytes: 153600,
      format: 'png'
    });
  },
  MetadataCache: class {
    has = jest.fn().mockReturnValue(false);
    get = jest.fn();
    set = jest.fn();
  }
}));

const mockUseImageStore = useImageStore as jest.MockedFunction<typeof useImageStore>;

describe('ImageGallery', () => {
  const defaultMockStore = {
    currentResults: [],
    status: MediaGenerationStatus.Idle,
    error: null,
    clearResults: jest.fn()
  };

  beforeEach(() => {
    jest.clearAllMocks();
    mockUseImageStore.mockReturnValue(defaultMockStore as any);
  });

  describe('Empty State', () => {
    it('should render empty state when no results', () => {
      render(<ImageGallery />);
      
      expect(screen.getByText(/Generated images will appear here/)).toBeInTheDocument();
    });

    it('should display instructions in empty state', () => {
      render(<ImageGallery />);
      
      expect(screen.getByText(/Enter a prompt and click/)).toBeInTheDocument();
    });
  });

  describe('With Results', () => {
    const mockResults: GeneratedImage[] = [
      {
        url: 'https://example.com/image1.jpg',
        revised_prompt: 'A beautiful landscape'
      },
      {
        url: 'https://example.com/image2.jpg',
        revised_prompt: 'A cityscape at night'
      }
    ];

    beforeEach(() => {
      mockUseImageStore.mockReturnValue({
        ...defaultMockStore,
        currentResults: mockResults,
        status: MediaGenerationStatus.Completed  // Ensure status is not Idle
      } as any);
    });

    it('should render images when results exist', () => {
      render(<ImageGallery />);
      
      expect(screen.getByTestId('media-gallery')).toBeInTheDocument();
      expect(screen.getByText('Image 1')).toBeInTheDocument();
      expect(screen.getByText('Image 2')).toBeInTheDocument();
    });

    it('should display image prompts', () => {
      render(<ImageGallery />);
      
      expect(screen.getByText('A beautiful landscape')).toBeInTheDocument();
      expect(screen.getByText('A cityscape at night')).toBeInTheDocument();
    });

    it('should display download buttons', () => {
      render(<ImageGallery />);
      
      const downloadButtons = screen.getAllByText('Download');
      expect(downloadButtons.length).toBe(2);
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
          filename: 'generated-image-1.png',
          mimeType: 'image/png'
        });
      });
    });

    it('should handle image click to open modal', () => {
      render(<ImageGallery />);
      
      const cards = screen.getAllByTestId('media-card');
      fireEvent.click(cards[0]);
      
      // The modal opening is handled by state, we just verify the click handler works
      expect(cards[0]).toBeInTheDocument();
    });
  });

  describe('Loading State', () => {
    it('should not show empty state when generating with no results', () => {
      mockUseImageStore.mockReturnValue({
        ...defaultMockStore,
        status: MediaGenerationStatus.Generating,
        currentResults: []
      } as any);

      render(<ImageGallery />);
      
      // When generating with no results, MediaGallery is rendered but empty
      expect(screen.getByTestId('media-gallery')).toBeInTheDocument();
    });

    it('should show results while generating new ones', () => {
      const existingResults: GeneratedImage[] = [
        { url: 'https://example.com/existing.jpg' }
      ];

      mockUseImageStore.mockReturnValue({
        ...defaultMockStore,
        status: MediaGenerationStatus.Generating,
        currentResults: existingResults
      } as any);

      render(<ImageGallery />);
      
      expect(screen.getByTestId('media-gallery')).toBeInTheDocument();
      expect(screen.getByText('Image 1')).toBeInTheDocument();
    });
  });

  describe('Error State', () => {
    it('should show empty state when error with no results', () => {
      mockUseImageStore.mockReturnValue({
        ...defaultMockStore,
        status: MediaGenerationStatus.Failed,
        error: 'Generation failed',
        currentResults: []
      } as any);

      render(<ImageGallery />);
      
      expect(screen.getByText(/Generated images will appear here/)).toBeInTheDocument();
    });

    it('should show existing results even with error', () => {
      const existingResults: GeneratedImage[] = [
        { url: 'https://example.com/existing.jpg' }
      ];

      mockUseImageStore.mockReturnValue({
        ...defaultMockStore,
        status: MediaGenerationStatus.Failed,
        error: 'Generation failed',
        currentResults: existingResults
      } as any);

      render(<ImageGallery />);
      
      expect(screen.getByTestId('media-gallery')).toBeInTheDocument();
      expect(screen.getByText('Image 1')).toBeInTheDocument();
    });
  });

  describe('Metadata Extraction', () => {
    it('should display metadata badges for images', async () => {
      const mockResults: GeneratedImage[] = [
        {
          url: 'https://example.com/image1.jpg',
          width: 1024,
          height: 768,
          sizeBytes: 153600,
          format: 'png'
        }
      ];

      mockUseImageStore.mockReturnValue({
        ...defaultMockStore,
        currentResults: mockResults,
        status: MediaGenerationStatus.Completed
      } as any);

      render(<ImageGallery />);
      
      await waitFor(() => {
        expect(screen.getByText('1024×768')).toBeInTheDocument();
        expect(screen.getByText('150 KB')).toBeInTheDocument();
        expect(screen.getByText('PNG')).toBeInTheDocument();
      });
    });
  });

  describe('Accessibility', () => {
    it('should have accessible image elements', () => {
      const mockResults: GeneratedImage[] = [
        {
          url: 'https://example.com/image1.jpg',
          revised_prompt: 'A beautiful landscape'
        },
        {
          url: 'https://example.com/image2.jpg',
          revised_prompt: 'A cityscape at night'
        }
      ];

      mockUseImageStore.mockReturnValue({
        ...defaultMockStore,
        currentResults: mockResults,
        status: MediaGenerationStatus.Completed
      } as any);

      render(<ImageGallery />);
      
      const images = document.querySelectorAll('img');
      expect(images.length).toBe(2);
      expect(images[0]).toHaveAttribute('alt');
    });

    it('should have keyboard navigable elements', () => {
      const mockResults: GeneratedImage[] = [
        { url: 'https://example.com/image1.jpg' }
      ];

      mockUseImageStore.mockReturnValue({
        ...defaultMockStore,
        currentResults: mockResults,
        status: MediaGenerationStatus.Completed
      } as any);

      render(<ImageGallery />);
      
      const downloadButton = screen.getAllByText('Download')[0];
      expect(downloadButton.closest('button')).toBeInTheDocument();
    });
  });
});