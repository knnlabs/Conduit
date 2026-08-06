import React from 'react';
import { fireEvent, render, screen } from '@/app/test-utils';
import ImageSettings from '../ImageSettings';
import { useImageStore } from '../../hooks/useImageStore';
import type { DiscoveryModel } from '@/app/chat/hooks/useDiscoveryModels';
import { ProviderType } from '@/lib/admin-api';

jest.mock('../../hooks/useImageStore');

jest.mock('@mantine/core', () => {
  const ReactActual = jest.requireActual<typeof import('react')>('react');
  const mantineCore = jest.requireActual<typeof import('@mantine/core')>('@mantine/core');
  return {
    ...mantineCore,
    Select: ({ label, value, onChange, data }: {
      label: string;
      value?: string;
      onChange: (value: string | null) => void;
      data: Array<{ value: string; label: string }>;
    }) => ReactActual.createElement(
      'label',
      {},
      label,
      ReactActual.createElement(
        'select',
        {
          value: value ?? '',
          onChange: (event: React.ChangeEvent<HTMLSelectElement>) => onChange(event.target.value || null),
          ['aria-label']: label,
        },
        data.map(item => ReactActual.createElement('option', { key: item.value, value: item.value }, item.label)),
      ),
    ),
  };
});

const mockUseImageStore = jest.mocked(useImageStore);
const updateSettings = jest.fn();

const capabilities = {
  chat: false,
  chat_stream: false,
  embeddings: false,
  image_generation: true,
  vision: false,
  video_generation: false,
  video_understanding: false,
  function_calling: false,
  tool_use: false,
  json_mode: false,
};

const models: DiscoveryModel[] = [
  {
    id: 'image-a',
    display_name: 'Image A',
    provider: ProviderType.OpenAI,
    capabilities,
    parameters: JSON.stringify({
      quality: { type: 'select', label: 'Quality', options: [{ value: 'hd', label: 'HD' }] },
      style: { type: 'select', label: 'Style', options: [{ value: 'vivid', label: 'Vivid' }] },
    }),
    last_verified: new Date().toISOString(),
  },
  {
    id: 'image-b',
    provider: ProviderType.Replicate,
    capabilities,
    last_verified: new Date().toISOString(),
  },
];

describe('ImageSettings', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    mockUseImageStore.mockReturnValue({
      settings: { model: 'image-a' },
      updateSettings,
    } as ReturnType<typeof useImageStore>);
  });

  it('renders the discovered model choices', () => {
    render(<ImageSettings models={models} />);

    expect(screen.getByText('Settings')).toBeInTheDocument();
    expect(screen.getByRole('option', { name: 'Image A' })).toBeInTheDocument();
    expect(screen.getByRole('option', { name: 'image-b' })).toBeInTheDocument();
  });

  it('updates the selected model', () => {
    render(<ImageSettings models={models} />);
    fireEvent.change(screen.getByLabelText('Model'), { target: { value: 'image-b' } });
    expect(updateSettings).toHaveBeenCalledWith({ model: 'image-b' });
  });

  it('does not duplicate discovery-driven quality or style controls', () => {
    render(<ImageSettings models={models} />);
    expect(screen.queryByText('Quality')).not.toBeInTheDocument();
    expect(screen.queryByText('Style')).not.toBeInTheDocument();
  });
});
