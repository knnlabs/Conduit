import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { MantineProvider } from '@mantine/core';

import { useProviders } from '@/hooks/useProviderApi';
import { useBulkCreateMappings, useBulkDiscoverModels } from '@/hooks/useModelMappingsApi';
import { notify } from '@/lib/notifications';
import { BulkMappingModal } from './BulkMappingModal';

jest.mock('@/hooks/useProviderApi', () => ({ useProviders: jest.fn() }));
jest.mock('@/hooks/useModelMappingsApi', () => ({
  useBulkDiscoverModels: jest.fn(),
  useBulkCreateMappings: jest.fn(),
}));
jest.mock('@/lib/utils/providerTypeUtils', () => ({
  getProviderTypeFromDto: jest.fn(() => 1),
  providerTypeToName: jest.fn(() => 'OpenAI'),
}));
jest.mock('@/lib/notifications', () => ({
  notify: {
    error: jest.fn(),
    success: jest.fn(),
    warning: jest.fn(),
  },
}));

const discoverModels = jest.fn();
const createMappings = jest.fn();

beforeEach(() => {
  jest.clearAllMocks();
  jest.mocked(useProviders).mockReturnValue({
    providers: [{ id: 9, providerName: 'Primary OpenAI', providerType: 1 }],
    isLoading: false,
  } as ReturnType<typeof useProviders>);
  jest.mocked(useBulkDiscoverModels).mockReturnValue({
    discoverModels,
    isDiscovering: false,
  });
  jest.mocked(useBulkCreateMappings).mockReturnValue({
    createMappings,
    isCreating: false,
  });
  discoverModels.mockResolvedValue({
    providerId: '9',
    providerName: 'OpenAI',
    models: [
      DiscoveredModel('available', false, null),
      DiscoveredModel('unresolved', true, "No model association exists for 'provider/unresolved'."),
    ],
    totalModels: 2,
    conflictCount: 1,
  });
});

it('selects only server-resolved models and explains unresolved conflicts', async () => {
  render(
    <MantineProvider>
      <BulkMappingModal isOpen onClose={jest.fn()} onSuccess={jest.fn()} />
    </MantineProvider>,
  );

  fireEvent.click(screen.getByPlaceholderText('Choose a provider to discover models'));
  fireEvent.click(await screen.findByText('Primary OpenAI'));

  expect(await screen.findByText(/1 models have conflicts or unresolved associations/)).toBeInTheDocument();
  expect(discoverModels).toHaveBeenCalledWith('9', 'OpenAI');
  expect(notify.warning).toHaveBeenCalledWith(
    '1 models have conflicts or unresolved associations',
    'Conflicts Detected',
  );

  const availableRow = screen.getByText('available').closest('tr');
  const unresolvedRow = screen.getByText('unresolved').closest('tr');
  expect(availableRow).not.toBeNull();
  expect(unresolvedRow).not.toBeNull();
  if (!availableRow || !unresolvedRow) {
    throw new Error('Expected discovered model rows to render');
  }

  const availableCheckbox = within(availableRow).getByRole('checkbox');
  const unresolvedCheckbox = within(unresolvedRow).getByRole('checkbox');
  expect(availableCheckbox).toBeChecked();
  expect(availableCheckbox).toBeEnabled();
  expect(unresolvedCheckbox).not.toBeChecked();
  expect(unresolvedCheckbox).toBeDisabled();
  expect(within(unresolvedRow).getByText('Unavailable')).toBeInTheDocument();
  expect(screen.getByRole('button', { name: 'Create 1 Mappings' })).toBeEnabled();

  fireEvent.click(screen.getByRole('button', { name: 'Select None' }));
  await waitFor(() => expect(availableCheckbox).not.toBeChecked());
  fireEvent.click(screen.getByRole('button', { name: 'Select All Available' }));
  await waitFor(() => expect(availableCheckbox).toBeChecked());
  expect(unresolvedCheckbox).not.toBeChecked();
});

function DiscoveredModel(modelId: string, hasConflict: boolean, conflictReason: string | null) {
  return {
    modelId,
    displayName: modelId[0].toUpperCase() + modelId.slice(1),
    providerId: '9',
    providerModelId: `provider/${modelId}`,
    hasConflict,
    existingMapping: null,
    conflictReason,
    modelProviderTypeAssociationId: hasConflict ? null : 42,
    capabilities: {
      supportsVision: false,
      supportsImageGeneration: false,
      supportsAudioTranscription: false,
      supportsTextToSpeech: false,
      supportsRealtimeAudio: false,
      supportsFunctionCalling: false,
      supportsStreaming: true,
      supportsVideoGeneration: false,
      supportsEmbeddings: false,
      supportsChat: true,
    },
  };
}
