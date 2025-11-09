import React from 'react';
import { render, screen, fireEvent, waitFor } from '@/app/test-utils';
import '@testing-library/jest-dom';
import ImageSettings from '../ImageSettings';
import { useImageStore } from '../../hooks/useImageStore';
import { useModelMetadata } from '../../hooks/useModelMetadata';
import type { DiscoveryModel } from '@/app/chat/hooks/useDiscoveryModels';

// Mock Mantine notifications first
jest.mock('@mantine/notifications', () => ({
  notifications: {
    show: jest.fn()
  }
}));

// Mock dependencies
jest.mock('../../hooks/useImageStore');
jest.mock('../../hooks/useModelMetadata');

// Mock core library
jest.mock('@mantine/core', () => {
  const React = require('react');
  return {
    ...jest.requireActual('@mantine/core'),
    getDefaultZIndex: () => 100,
    Select: ({ label, value, onChange, data, required }: {
    label: string;
    value?: string;
    onChange: (value: string | null) => void;
    data: Array<{ value: string; label: string }>;
    required?: boolean;
  }) => React.createElement('div', {},
    React.createElement('span', {}, label),
    required && React.createElement('span', {}, ' *'),
    React.createElement('select', {
      value: value ?? '',
      onChange: (e: any) => onChange(e.target.value || null),
      'data-testid': `select-${label.toLowerCase()}`
    },
      // Add empty option if value is empty
      (value === '' || value === undefined || value === null) ? 
        [React.createElement('option', { key: '', value: '' }, '')].concat(
          data.map((item) => 
            React.createElement('option', { key: item.value, value: item.value }, item.label)
          )
        ) :
        data.map((item) => 
          React.createElement('option', { key: item.value, value: item.value }, item.label)
        )
    )
  ),
  Grid: Object.assign(
    ({ children }: { children: React.ReactNode }) => <div data-testid="grid">{children}</div>,
    {
      Col: ({ children, span }: { children: React.ReactNode; span?: unknown }) => (
        <div data-testid="grid-col" data-span={JSON.stringify(span)}>{children}</div>
      ),
    }
  ),
  Text: ({ children, fw, mb }: { children: React.ReactNode; fw?: number; mb?: string }) => (
    <span data-fw={fw} data-mb={mb}>{children}</span>
  ),
  };
});

const mockUseImageStore = useImageStore as jest.MockedFunction<typeof useImageStore>;
const mockUseModelMetadata = useModelMetadata as jest.MockedFunction<typeof useModelMetadata>;

describe('ImageSettings', () => {
  const mockUpdateSettings = jest.fn();
  const mockModels: DiscoveryModel[] = [
    {
      id: 'dall-e-3',
      display_name: 'DALL-E 3',
      provider: 'openai',
      category: 'image',
      capabilities: [],
      context_window: 0,
      max_output_tokens: 0,
      input_cost_per_million: 0,
      output_cost_per_million: 0,
      cache_write_cost_per_million: 0,
      cache_read_cost_per_million: 0
    },
    {
      id: 'dall-e-2',
      display_name: 'DALL-E 2',
      provider: 'openai',
      category: 'image',
      capabilities: [],
      context_window: 0,
      max_output_tokens: 0,
      input_cost_per_million: 0,
      output_cost_per_million: 0,
      cache_write_cost_per_million: 0,
      cache_read_cost_per_million: 0
    },
    {
      id: 'stable-diffusion',
      display_name: 'Stable Diffusion',
      provider: 'stability',
      category: 'image',
      capabilities: [],
      context_window: 0,
      max_output_tokens: 0,
      input_cost_per_million: 0,
      output_cost_per_million: 0,
      cache_write_cost_per_million: 0,
      cache_read_cost_per_million: 0
    }
  ];

  const defaultSettings = {
    model: 'dall-e-3',
    quality: 'standard' as const,
    style: 'vivid' as const,
    size: '1024x1024',
    n: 1,
    response_format: 'url' as const
  };

  beforeEach(() => {
    jest.clearAllMocks();
    // Don't use jest.resetAllMocks() as it removes mock implementations
    mockUpdateSettings.mockClear();
    mockUseImageStore.mockReturnValue({
      settings: defaultSettings,
      updateSettings: mockUpdateSettings,
      prompt: '',
      currentResults: [],
      status: 'idle' as const,
      error: null,
      generateImages: jest.fn(),
      clearResults: jest.fn(),
      setPrompt: jest.fn()
    });
    mockUseModelMetadata.mockReturnValue({
      data: null,
      isLoading: false,
      error: null,
      refetch: jest.fn()
    } as any);
  });

  describe('Basic Rendering', () => {
    it('should render settings title', () => {
      render(<ImageSettings models={mockModels} />);
      
      expect(screen.getByText('Settings')).toBeInTheDocument();
    });

    it('should render model selection dropdown', () => {
      render(<ImageSettings models={mockModels} />);
      
      expect(screen.getByText('Model')).toBeInTheDocument();
      expect(screen.getByTestId('select-model')).toBeInTheDocument();
    });

    it('should display all model options', () => {
      render(<ImageSettings models={mockModels} />);
      
      const select = screen.getByTestId('select-model');
      const options = select.querySelectorAll('option');
      
      expect(options).toHaveLength(3);
      expect(options[0]).toHaveTextContent('DALL-E 3');
      expect(options[1]).toHaveTextContent('DALL-E 2');
      expect(options[2]).toHaveTextContent('Stable Diffusion');
    });

    it('should show current model selection', () => {
      render(<ImageSettings models={mockModels} />);
      
      const select = screen.getByTestId('select-model') as HTMLSelectElement;
      expect(select.value).toBe('dall-e-3');
    });
  });

  describe('Model Selection', () => {
    it('should update settings when model is changed', () => {
      render(<ImageSettings models={mockModels} />);
      
      const select = screen.getByTestId('select-model');
      fireEvent.change(select, { target: { value: 'dall-e-2' } });
      
      expect(mockUpdateSettings).toHaveBeenCalledWith({ model: 'dall-e-2' });
    });

    it('should not update settings when null value is selected', () => {
      render(<ImageSettings models={mockModels} />);
      
      const select = screen.getByTestId('select-model');
      fireEvent.change(select, { target: { value: '' } });
      
      expect(mockUpdateSettings).not.toHaveBeenCalled();
    });

    it('should handle models with no display name', () => {
      const modelsWithoutDisplayName = [
        { ...mockModels[0], display_name: undefined }
      ];
      
      render(<ImageSettings models={modelsWithoutDisplayName} />);
      
      const select = screen.getByTestId('select-model');
      const option = select.querySelector('option');
      expect(option).toHaveTextContent('dall-e-3');
    });
  });

  describe('Quality Settings', () => {
    it('should show quality dropdown when metadata indicates support', () => {
      mockUseModelMetadata.mockReturnValue({
        data: {
          metadata: {
            image: {
              qualityOptions: ['standard', 'hd']
            }
          }
        },
        isLoading: false,
        error: null,
        refetch: jest.fn()
      } as any);

      render(<ImageSettings models={mockModels} />);
      
      expect(screen.getByText('Quality')).toBeInTheDocument();
      expect(screen.getByTestId('select-quality')).toBeInTheDocument();
    });

    it('should not show quality dropdown when not supported', () => {
      mockUseModelMetadata.mockReturnValue({
        data: {
          metadata: {
            image: {
              qualityOptions: []
            }
          }
        },
        isLoading: false,
        error: null,
        refetch: jest.fn()
      } as any);

      render(<ImageSettings models={mockModels} />);
      
      expect(screen.queryByText('Quality')).not.toBeInTheDocument();
    });

    it('should update quality setting when changed', () => {
      mockUseModelMetadata.mockReturnValue({
        data: {
          metadata: {
            image: {
              qualityOptions: ['standard', 'hd']
            }
          }
        },
        isLoading: false,
        error: null,
        refetch: jest.fn()
      } as any);

      render(<ImageSettings models={mockModels} />);
      
      const select = screen.getByTestId('select-quality');
      fireEvent.change(select, { target: { value: 'hd' } });
      
      expect(mockUpdateSettings).toHaveBeenCalledWith({ quality: 'hd' });
    });

    it('should capitalize quality option labels', () => {
      mockUseModelMetadata.mockReturnValue({
        data: {
          metadata: {
            image: {
              qualityOptions: ['standard', 'hd']
            }
          }
        },
        isLoading: false,
        error: null,
        refetch: jest.fn()
      } as any);

      render(<ImageSettings models={mockModels} />);
      
      const select = screen.getByTestId('select-quality');
      const options = select.querySelectorAll('option');
      
      expect(options[0]).toHaveTextContent('Standard');
      expect(options[1]).toHaveTextContent('Hd');
    });
  });

  describe('Style Settings', () => {
    it('should show style dropdown when metadata indicates support', () => {
      mockUseModelMetadata.mockReturnValue({
        data: {
          metadata: {
            image: {
              styleOptions: ['vivid', 'natural']
            }
          }
        },
        isLoading: false,
        error: null,
        refetch: jest.fn()
      } as any);

      render(<ImageSettings models={mockModels} />);
      
      expect(screen.getByText('Style')).toBeInTheDocument();
      expect(screen.getByTestId('select-style')).toBeInTheDocument();
    });

    it('should not show style dropdown when not supported', () => {
      mockUseModelMetadata.mockReturnValue({
        data: {
          metadata: {
            image: {
              styleOptions: []
            }
          }
        },
        isLoading: false,
        error: null,
        refetch: jest.fn()
      } as any);

      render(<ImageSettings models={mockModels} />);
      
      expect(screen.queryByText('Style')).not.toBeInTheDocument();
    });

    it('should update style setting when changed', () => {
      mockUseModelMetadata.mockReturnValue({
        data: {
          metadata: {
            image: {
              styleOptions: ['vivid', 'natural']
            }
          }
        },
        isLoading: false,
        error: null,
        refetch: jest.fn()
      } as any);

      render(<ImageSettings models={mockModels} />);
      
      const select = screen.getByTestId('select-style');
      fireEvent.change(select, { target: { value: 'natural' } });
      
      expect(mockUpdateSettings).toHaveBeenCalledWith({ style: 'natural' });
    });
  });

  describe('Metadata Loading States', () => {
    it('should handle null metadata response', () => {
      mockUseModelMetadata.mockReturnValue({
        data: null,
        isLoading: false,
        error: null,
        refetch: jest.fn()
      } as any);

      render(<ImageSettings models={mockModels} />);
      
      expect(screen.queryByText('Quality')).not.toBeInTheDocument();
      expect(screen.queryByText('Style')).not.toBeInTheDocument();
    });

    it('should handle undefined metadata', () => {
      mockUseModelMetadata.mockReturnValue({
        data: {},
        isLoading: false,
        error: null,
        refetch: jest.fn()
      } as any);

      render(<ImageSettings models={mockModels} />);
      
      expect(screen.queryByText('Quality')).not.toBeInTheDocument();
      expect(screen.queryByText('Style')).not.toBeInTheDocument();
    });

    it('should handle loading state', () => {
      mockUseModelMetadata.mockReturnValue({
        data: null,
        isLoading: true,
        error: null,
        refetch: jest.fn()
      } as any);

      render(<ImageSettings models={mockModels} />);
      
      // Should still render model selector during loading
      expect(screen.getByText('Model')).toBeInTheDocument();
    });

    it('should handle error state', () => {
      mockUseModelMetadata.mockReturnValue({
        data: null,
        isLoading: false,
        error: new Error('Failed to fetch metadata'),
        refetch: jest.fn()
      } as any);

      render(<ImageSettings models={mockModels} />);
      
      // Should still render model selector even with error
      expect(screen.getByText('Model')).toBeInTheDocument();
      expect(screen.queryByText('Quality')).not.toBeInTheDocument();
      expect(screen.queryByText('Style')).not.toBeInTheDocument();
    });
  });

  describe('Edge Cases', () => {
    it('should handle empty models array', () => {
      render(<ImageSettings models={[]} />);
      
      expect(screen.getByText('Settings')).toBeInTheDocument();
      expect(screen.getByTestId('select-model')).toBeInTheDocument();
    });

    it('should handle null model in settings', () => {
      // Completely override the mock for this test
      mockUseImageStore.mockReset();
      mockUseImageStore.mockImplementation(() => ({
        settings: { 
          model: '', // Use empty string for null/undefined model
          quality: 'standard' as const,
          style: 'vivid' as const,
          size: '1024x1024',
          n: 1,
          response_format: 'url' as const
        },
        updateSettings: mockUpdateSettings,
        prompt: '',
        currentResults: [],
        status: 'idle' as const,
        error: null,
        generateImages: jest.fn(),
        clearResults: jest.fn(),
        setPrompt: jest.fn()
      }));

      mockUseModelMetadata.mockReset();
      mockUseModelMetadata.mockImplementation(() => ({
        data: null,
        isLoading: false,
        error: null,
        refetch: jest.fn()
      } as any));

      render(<ImageSettings models={mockModels} />);
      
      const select = screen.getByTestId('select-model') as HTMLSelectElement;
      expect(select.value).toBe('');
    });

    it('should handle both quality and style options together', () => {
      mockUseModelMetadata.mockReturnValue({
        data: {
          metadata: {
            image: {
              qualityOptions: ['standard', 'hd'],
              styleOptions: ['vivid', 'natural']
            }
          }
        },
        isLoading: false,
        error: null,
        refetch: jest.fn()
      } as any);

      render(<ImageSettings models={mockModels} />);
      
      expect(screen.getByText('Quality')).toBeInTheDocument();
      expect(screen.getByText('Style')).toBeInTheDocument();
      expect(screen.getByTestId('select-quality')).toBeInTheDocument();
      expect(screen.getByTestId('select-style')).toBeInTheDocument();
    });

    it('should handle metadata with partial data', () => {
      mockUseModelMetadata.mockReturnValue({
        data: {
          metadata: {
            image: {
              sizes: ['1024x1024', '512x512'],
              maxImages: 4
              // No qualityOptions or styleOptions
            }
          }
        },
        isLoading: false,
        error: null,
        refetch: jest.fn()
      } as any);

      render(<ImageSettings models={mockModels} />);
      
      expect(screen.queryByText('Quality')).not.toBeInTheDocument();
      expect(screen.queryByText('Style')).not.toBeInTheDocument();
    });
  });

  describe('Responsive Layout', () => {
    it('should render with grid layout', () => {
      render(<ImageSettings models={mockModels} />);
      
      expect(screen.getByTestId('grid')).toBeInTheDocument();
      
      const gridCols = screen.getAllByTestId('grid-col');
      expect(gridCols.length).toBeGreaterThanOrEqual(1);
    });

    it('should apply responsive column spans', () => {
      mockUseModelMetadata.mockReturnValue({
        data: {
          metadata: {
            image: {
              qualityOptions: ['standard', 'hd'],
              styleOptions: ['vivid', 'natural']
            }
          }
        },
        isLoading: false,
        error: null,
        refetch: jest.fn()
      } as any);

      render(<ImageSettings models={mockModels} />);
      
      const gridCols = screen.getAllByTestId('grid-col');
      gridCols.forEach(col => {
        const span = col.getAttribute('data-span');
        expect(span).toContain('base');
        expect(span).toContain('sm');
        expect(span).toContain('md');
      });
    });
  });
});