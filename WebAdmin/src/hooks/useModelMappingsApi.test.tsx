import type { PropsWithChildren } from 'react';
import { act, renderHook } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { withAdminClient } from '@/lib/client/adminClient';
import { useBulkCreateMappings, useBulkDiscoverModels } from './useModelMappingsApi';

jest.mock('@/lib/client/adminClient', () => ({ withAdminClient: jest.fn() }));
jest.mock('@/lib/notifications', () => ({
  notify: {
    error: jest.fn(),
    success: jest.fn(),
    warning: jest.fn(),
  },
}));

const getByProvider = jest.fn();
const previewBulk = jest.fn();
const bulkCreate = jest.fn<Promise<unknown>, [unknown]>();
const adminClient = {
  models: { getByProvider },
  modelMappings: { previewBulk, bulkCreate },
};

const mockedWithAdminClient = jest.mocked(withAdminClient);

beforeEach(() => {
  jest.clearAllMocks();
  mockedWithAdminClient.mockImplementation(operation => operation(adminClient as never));
});

describe('useBulkDiscoverModels', () => {
  it('uses backend resolution results for conflicts and association IDs', async () => {
    getByProvider.mockResolvedValue([
      { id: 10, name: 'available', providerModelId: 'provider/available' },
      { id: 11, name: 'missing', providerModelId: 'provider/missing' },
    ]);
    previewBulk.mockResolvedValue({
      items: [
        {
          index: 0,
          modelAlias: 'provider/available',
          providerId: 9,
          providerModelId: 'provider/available',
          modelProviderTypeAssociationId: 42,
          hasConflict: false,
        },
        {
          index: 1,
          modelAlias: 'provider/missing',
          providerId: 9,
          providerModelId: 'provider/missing',
          hasConflict: true,
          errorMessage: "No model association exists for provider identifier 'provider/missing'.",
        },
      ],
      totalProcessed: 2,
      conflictCount: 1,
    });
    const { result } = renderHook(() => useBulkDiscoverModels());

    let discovery: Awaited<ReturnType<typeof result.current.discoverModels>> | undefined;
    await act(async () => {
      discovery = await result.current.discoverModels('9', 'OpenAI');
    });

    expect(getByProvider).toHaveBeenCalledWith('openai');
    expect(previewBulk).toHaveBeenCalledWith({
      mappings: [
        { modelAlias: 'provider/available', providerId: 9, providerModelId: 'provider/available' },
        { modelAlias: 'provider/missing', providerId: 9, providerModelId: 'provider/missing' },
      ],
    });
    expect(discovery?.conflictCount).toBe(1);
    expect(discovery?.models[0]).toEqual(expect.objectContaining({
      hasConflict: false,
      modelProviderTypeAssociationId: 42,
    }));
    expect(discovery?.models[1]?.hasConflict).toBe(true);
    expect(discovery?.models[1]?.conflictReason).toContain('No model association');
  });

  it('returns an empty result without submitting an invalid preview request', async () => {
    getByProvider.mockResolvedValue([]);
    const { result } = renderHook(() => useBulkDiscoverModels());

    let discovery: Awaited<ReturnType<typeof result.current.discoverModels>> | undefined;
    await act(async () => {
      discovery = await result.current.discoverModels('9', 'OpenAI');
    });

    expect(discovery).toEqual({
      providerId: '9',
      providerName: 'OpenAI',
      models: [],
      totalModels: 0,
      conflictCount: 0,
    });
    expect(previewBulk).not.toHaveBeenCalled();
  });
});

describe('useBulkCreateMappings', () => {
  it('submits provider identifiers without association foreign keys and preserves per-item failures', async () => {
    bulkCreate.mockResolvedValue({
      created: [{ id: 1 }],
      existing: [{ id: 2 }],
      failed: [{
        index: 1,
        modelAlias: 'provider/missing',
        providerId: 9,
        providerModelId: 'provider/missing',
        hasConflict: true,
        errorMessage: 'Association missing',
      }],
      totalProcessed: 3,
      createdCount: 1,
      existingCount: 1,
      successCount: 2,
      failureCount: 1,
      isSuccess: false,
      isPartialSuccess: true,
    });
    const queryClient = new QueryClient({
      defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
    });
    const invalidateQueries = jest.spyOn(queryClient, 'invalidateQueries');
    const wrapper = ({ children }: PropsWithChildren) => (
      <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
    );
    const { result } = renderHook(() => useBulkCreateMappings(), { wrapper });

    let creation: Awaited<ReturnType<typeof result.current.createMappings>> | undefined;
    await act(async () => {
      creation = await result.current.createMappings({
        models: [
          Model('1', 'provider/available'),
          Model('2', 'provider/missing'),
          Model('3', 'provider/existing'),
        ],
        defaultPriority: 25,
        enableByDefault: false,
      });
    });

    const request: unknown = bulkCreate.mock.calls[0]?.[0];
    expect(request).toEqual({
      mappings: [
        { modelAlias: 'provider/available', providerId: 9, providerModelId: 'provider/available' },
        { modelAlias: 'provider/missing', providerId: 9, providerModelId: 'provider/missing' },
        { modelAlias: 'provider/existing', providerId: 9, providerModelId: 'provider/existing' },
      ],
      isEnabled: false,
      priority: 25,
      weight: 1,
    });
    expect(JSON.stringify(request)).not.toContain('modelProviderTypeAssociationId');
    expect(creation).toEqual(expect.objectContaining({ created: 1, existing: 1, failed: 1 }));
    expect(creation?.details.failed).toEqual([{ modelId: '2', error: 'Association missing' }]);
    expect(invalidateQueries).toHaveBeenCalledWith({ queryKey: ['model-mappings'] });
  });
});

function Model(modelId: string, providerModelId: string) {
  return {
    modelId,
    displayName: providerModelId,
    providerId: '9',
    providerModelId,
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
